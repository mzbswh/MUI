using MUI.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private string nativeBindingMessage;
        private readonly List<string> nativeRuleErrors = new List<string>();
        private bool validationPending;
        private double validationDue;

        private string CurrentPrefabPath()
        {
            var view = (View)target;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject);
            if (stage != null)
                return stage.assetPath;
            var source = EditorUtility.IsPersistent(view) ? view : PrefabUtility.GetCorrespondingObjectFromSource(view);
            return source == null ? null : AssetDatabase.GetAssetPath(source);
        }

        private List<ControlRow> BuildControlRows()
        {
            nativeRuleErrors.Clear();
            var view = (View)target;
            var rows = new List<ControlRow>();
            ControlRow Add(string name, Type adapter, BindingEntry entry = null, Type componentType = null)
            {
                if (string.IsNullOrWhiteSpace(name) || adapter == null || !typeof(IElement).IsAssignableFrom(adapter))
                    throw new InvalidOperationException(L.Get("nativeRules.invalidName"));
                var row = rows.Find(item => item.Name == name && item.ComponentType == componentType);
                if (row == null && componentType != null)
                {
                    var compatible = rows.Where(item => item.Name == name && !item.IsElementBinding &&
                        (item.ComponentType == null || item.ComponentType.IsAssignableFrom(componentType)) &&
                        item.RequiredAdapters.All(required => required.IsAssignableFrom(adapter))).ToList();
                    if (compatible.Count == 1) row = compatible[0];
                }
                if (row != null)
                {
                    if (!row.RequiredAdapters.Contains(adapter))
                        row.RequiredAdapters.Add(adapter);
                    if (row.ComponentType == null)
                        row.ComponentType = componentType;
                    return row;
                }
                row = new ControlRow
                {
                    Name = name,
                    Adapter = adapter,
                    Entry = entry,
                    ComponentType = componentType,
                    IsElementBinding = typeof(Element).IsAssignableFrom(adapter)
                };
                row.RequiredAdapters.Add(adapter);
                rows.Add(row);
                return row;
            }
            if (selected >= 0)
                foreach (var entry in manifests[selected].Entries)
                    if (entry.ElementType != typeof(WidgetReference))
                        Add(entry.ElementName, entry.ElementType, entry, entry.ComponentType);
            var path = CurrentPrefabPath();
            var guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
                foreach (var type in TypeCache.GetTypesWithAttribute<ViewControlAttribute>())
                    foreach (var declaration in type.GetCustomAttributes<ViewControlAttribute>())
                        if (declaration.PrefabGuid == guid && (!declaration.Generated || view.NativeBindings.Any(b => b != null && b.Name == declaration.Name &&
                            ((declaration.TargetType ?? declaration.ComponentType) == null ||
                                b.Target != null && (declaration.TargetType ?? declaration.ComponentType).IsInstanceOfType(b.Target)))))
                            Add(declaration.Name, declaration.AdapterType, componentType: declaration.TargetType ?? declaration.ComponentType).DeclaredInCode = true;

            var settings = MUIEditorSettings.instance;
            var rules = settings == null ? MUIEditorSettings.CreateControlNamingRules() : settings.ControlNamingRules;
            foreach (var node in view.GetComponentsInChildren<Transform>(true))
            {
                if (!view.OwnsNativeControl(node) || node.GetComponent<Element>() != null)
                    continue;
                var remaining = node.name;
                var tokens = new List<ControlNamingRule>();
                while (!string.IsNullOrEmpty(remaining))
                {
                    ControlNamingRule rule = null;
                    string remainder = null;
                    if (rules != null)
                        foreach (var candidate in rules)
                        {
                            if (candidate == null || candidate.ComponentTypes == null || string.IsNullOrEmpty(candidate.Expression))
                                continue;
                            try
                            {
                                if (ControlRuleMatcher.TryMatch(candidate, remaining, out var matched) &&
                                    (rule == null || candidate.Expression.Length > rule.Expression.Length))
                                {
                                    rule = candidate;
                                    remainder = matched;
                                }
                            }
                            catch (Exception error) { nativeRuleErrors.Add(candidate.Expression + ": " + error.Message); }
                        }
                    if (rule == null)
                        break;
                    tokens.Add(rule);
                    remaining = remainder;
                    if (rule.Match == ControlNameMatch.Regex)
                        break;
                }
                if (tokens.Count == 0)
                    continue;
                if (!PageSourceTemplate.IsIdentifier(remaining))
                {
                    nativeRuleErrors.Add(node.name + ": " + L.Get("nativeRules.invalidName"));
                    continue;
                }
                var matches = new List<Component>();
                foreach (var rule in tokens)
                {
                    var resolvedTypes = rule.ComponentTypes.Select(value => Type.GetType(value, false)).Where(type => type != null).ToList();
                    var ruleMatches = node.GetComponents<Component>().Where(component => component != null &&
                        resolvedTypes.Any(type => type.IsInstanceOfType(component))).ToList();
                    if (ruleMatches.Count == 0)
                        nativeRuleErrors.Add(node.name + " / " + rule.Expression + ": " + L.Get("nativeRules.missingComponent"));
                    foreach (var component in ruleMatches)
                        if (!matches.Contains(component))
                            matches.Add(component);
                }
                foreach (var component in matches)
                {
                    var name = remaining;
                    using (var adapter = NativeControl.Create(name, component))
                    {
                        var row = Add(name, adapter.GetType(), componentType: component.GetType());
                        var matchingRules = tokens.Where(rule => rule.ComponentTypes.Any(value => { var t = Type.GetType(value, false); return t != null && t.IsInstanceOfType(component); })).ToArray();
                        row.Rule = matchingRules.FirstOrDefault();
                        if (matchingRules.Any(rule => rule.OverrideDefaultProperties != row.Rule.OverrideDefaultProperties || (rule.OverrideDefaultProperties && !(rule.DefaultBindingProperties ?? new List<string>()).SequenceEqual(row.Rule.DefaultBindingProperties ?? new List<string>())) || rule.OverrideBindingDirection != row.Rule.OverrideBindingDirection || rule.BindingDirection != row.Rule.BindingDirection))
                            row.Error = "同一组件匹配到不同的绑定规则，请统一属性及方向。";
                        if (!PageSourceTemplate.IsIdentifier(name))
                            row.Error = L.Get("nativeRules.invalidName");
                        if (!row.Candidates.Contains(component))
                            row.Candidates.Add(component);
                    }
                }
            }
            foreach (var row in rows)
            {
                if (row.Entry != null && !row.IsElementBinding)
                {
                    var existingTargets = ViewContractValidator.FindBindingTargets(view, row.Entry);
                    if (existingTargets.Count == 1 && existingTargets[0] is Element)
                        row.IsElementBinding = true;
                }
                if (row.IsElementBinding)
                {
                    if (row.Entry != null)
                    {
                        var matches = ViewContractValidator.FindBindingTargets(view, row.Entry);
                        if (matches.Count == 1)
                            row.Target = matches[0];
                        else
                            row.Error = L.Get("nativeRules.missingOrMultiple");
                    }
                    continue;
                }
                foreach (var binding in view.NativeBindings)
                    if (MatchesReference(row, binding) && Compatible(row, binding.Target))
                    {
                        if (row.Target != null)
                            row.Error = L.Get("nativeRules.missingOrMultiple");
                        row.Target = binding.Target;
                    }
                // 已存引用优先；代码声明还可匹配未加前缀的同名原生组件。
                if (row.Target != null)
                    continue;
                foreach (var component in view.GetComponentsInChildren<Component>(true))
                    if (component != null && component.name == row.Name && Compatible(row, component) && !row.Candidates.Contains(component))
                        row.Candidates.Add(component);
                row.Candidates.RemoveAll(component => !Compatible(row, component));
                if (row.Candidates.Count == 1)
                    row.Target = row.Candidates[0];
                else if (row.Candidates.Count > 1 && row.Error == null)
                    row.Error = L.Get("nativeRules.missingOrMultiple");
            }
            foreach (var group in rows.Where(row => !row.IsElementBinding && row.Target != null).GroupBy(row => row.Target))
                if (group.Count() > 1)
                    foreach (var row in group)
                        row.Error = L.Get("nativeRules.fixErrors");
            foreach (var row in rows) row.SharedName = rows.Count(other => other.Name == row.Name) > 1;
            return rows;
        }

        private static Type ReferenceType(SerializedProperty reference)
        {
            var typeName = reference.FindPropertyRelative("ComponentType").stringValue;
            var component = reference.FindPropertyRelative("Target").objectReferenceValue as Component;
            return string.IsNullOrEmpty(typeName) ? (component == null ? null : component.GetType()) : Type.GetType(typeName, false);
        }

        private static bool MatchesReference(ControlRow row, NativeControlBinding binding) => binding != null && binding.Name == row.Name &&
            (row.ComponentType == null || binding.BindingType == row.ComponentType ||
                string.IsNullOrEmpty(binding.ComponentType) && binding.Target != null && row.ComponentType.IsInstanceOfType(binding.Target));

        private static bool MatchesReference(ControlRow row, SerializedProperty binding) => binding.FindPropertyRelative("Name").stringValue == row.Name &&
            (row.ComponentType == null || ReferenceType(binding) == row.ComponentType ||
                string.IsNullOrEmpty(binding.FindPropertyRelative("ComponentType").stringValue) &&
                row.ComponentType.IsInstanceOfType(binding.FindPropertyRelative("Target").objectReferenceValue));

        private static bool MatchesEntry(ControlRow row, BindingEntry entry) => entry.ElementName == row.Name &&
            (entry.ComponentType != null ? entry.ComponentType == row.ComponentType : row.RequiredAdapters.Any(type => entry.ElementType.IsAssignableFrom(type)));

        private bool Compatible(ControlRow row, Component component)
        {
            if (component == null || row.ComponentType != null && !row.ComponentType.IsInstanceOfType(component) || !((View)target).OwnsNativeControl(component) || component.GetComponent<Element>() != null)
                return false;
            try
            {
                using (var adapter = NativeControl.Create(row.Name, component))
                    return row.RequiredAdapters.All(type => type.IsInstanceOfType(adapter));
            }
            catch (ArgumentException) { return false; }
        }

        private void SaveControlReference(ControlRow control, Component component)
        {
            var name = control.Name;
            var componentType = control.ComponentType ?? (component == null ? null : component.GetType());
            SaveControlReference(name, componentType, component);
        }

        private void SaveControlReference(string name, Type componentType, Component component)
        {
            serializedObject.Update();
            var rows = serializedObject.FindProperty("nativeBindings");
            var index = -1;
            for (var i = 0; i < rows.arraySize; i++)
                if (rows.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue == name &&
                    (ReferenceType(rows.GetArrayElementAtIndex(i)) == componentType ||
                        string.IsNullOrEmpty(rows.GetArrayElementAtIndex(i).FindPropertyRelative("ComponentType").stringValue) &&
                        componentType != null && componentType.IsInstanceOfType(rows.GetArrayElementAtIndex(i).FindPropertyRelative("Target").objectReferenceValue)))
                {
                    index = i;
                    break;
                }
            if (index < 0)
                index = rows.arraySize++;
            var row = rows.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("Name").stringValue = name;
            row.FindPropertyRelative("ComponentType").stringValue = componentType?.AssemblyQualifiedName ?? "";
            row.FindPropertyRelative("Target").objectReferenceValue = component;
            serializedObject.ApplyModifiedProperties();
            ClearBindingTargets();
            results = null;
            QueueBindingValidation();
        }

        private void QueueCodeBindingSync()
        {
            EditorApplication.delayCall -= SyncCodeBindingReferences;
            EditorApplication.delayCall += SyncCodeBindingReferences;
        }

        // 只在检查器初始化或切换契约时同步，避免 Repaint/Undo/手动清理触发重复写入。
        private void SyncCodeBindingReferences()
        {
            if (this == null || target == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            try
            {
                var view = (View)target;
                var pending = BuildControlRows().Where(row => !row.IsElementBinding && (row.Entry != null || row.DeclaredInCode) &&
                    row.Target != null && row.Error == null &&
                    !view.NativeBindings.Any(binding => binding != null &&
                        (MatchesReference(row, binding) && binding.Target != null || binding.Target == row.Target))).ToList();
                if (pending.Count == 0)
                    return;
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(L.Get("nativeRules.autoSync"));
                try
                {
                    foreach (var row in pending)
                        SaveControlReference(row, row.Target);
                    nativeBindingMessage = L.Format("nativeRules.autoSynced", pending.Count);
                }
                finally { Undo.CollapseUndoOperations(group); }
                Repaint();
            }
            catch (Exception error) { nativeBindingMessage = error.Message; Repaint(); }
        }

        private void DrawNativeBindings()
        {
            List<ControlRow> rows;
            try
            {
                rows = BuildControlRows();
            }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); return; }
            using (new EditorGUILayout.HorizontalScope())
            {
                var generateHelp = selected < 0 ? "尚未关联 ViewModel，无法生成绑定。" : EditorApplication.isCompiling ? "Unity 正在编译，请等待编译完成。" : "校验当前配置，保存控件引用和属性选择到 Prefab，并请求编译器生成代码。已有手写绑定优先；不在 Assets 创建 C# 文件。";
                if (BindingActionButton(L.Get("nativeRules.generate"), generateHelp, 100, selected >= 0 && !EditorApplication.isCompiling))
                    SyncAndValidateBindings(rows);
                if (GUILayout.Button(new GUIContent(L.Get("nativeRules.settings"), L.Get("nativeRules.settings.help")), EditorStyles.miniButton, GUILayout.Width(76)))
                    RuntimeSettingsAsset.OpenOrCreate<UGUIHostSettings>();
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent((validationPending || results == null ? "待检查" : results.Count == 0 ? (selected < 0 ? "结构通过" : "检查通过") : results.Count + " 个问题") + " · " + rows.Count + " 个控件",
                    "配置变化后自动校验并同步 Prefab 声明，随后请求编译；编译器诊断以 Unity Console 为准。"), EditorStyles.miniLabel);
            }
            var unused = ((View)target).NativeBindings.Count(binding => binding != null && !rows.Any(row => MatchesReference(row, binding)));
            if (unused > 0 && GUILayout.Button(new GUIContent(L.Get("nativeRules.removeUnused") + " (" + unused + ")", L.Get("nativeRules.removeUnused.help")), EditorStyles.miniButton))
            {
                serializedObject.Update();
                var references = serializedObject.FindProperty("nativeBindings");
                for (var i = references.arraySize - 1; i >= 0; i--)
                    if (!rows.Any(row => MatchesReference(row, references.GetArrayElementAtIndex(i))))
                        references.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                ClearBindingTargets();
                results = null;
                nativeBindingMessage = L.Format("nativeRules.removed", unused);
            }
            if (rows.Count == 0)
                EditorGUILayout.HelpBox(L.Get("nativeRules.empty"), MessageType.Info);
            foreach (var row in rows)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var expanded = IsBindingExpanded(row.Key);
                    var nextExpanded = EditorGUI.Foldout(GUILayoutUtility.GetRect(100, EditorGUIUtility.singleLineHeight, GUILayout.Width(row.SharedName ? 200 : 100)), expanded, new GUIContent(row.Name + (row.SharedName ? " (" + (row.ComponentType?.Name ?? row.Adapter.Name) + ")" : ""), "组件类型：" + (row.ComponentType?.FullName ?? row.Adapter.FullName)), true);
                    if (nextExpanded != expanded)
                        SetBindingExpanded(row.Key, nextExpanded);
                    using (new EditorGUI.DisabledScope(row.IsElementBinding))
                    {
                        var next = EditorGUILayout.ObjectField(row.Target, row.ComponentType ?? typeof(Component), true);
                        if (next != row.Target)
                        {
                            if (next == null || Compatible(row, next as Component))
                                SaveControlReference(row, next as Component);
                            else
                                nativeBindingMessage = L.Get("nativeRules.wrongTarget");
                        }
                    }
                    if (GUILayout.Button(new GUIContent(L.Get("bindingRow.locate"), L.Get("bindingRow.locate.help")), EditorStyles.miniButton, GUILayout.Width(44)) && row.Target != null)
                        EditorGUIUtility.PingObject(row.Target.gameObject);
                    var canOpenSource = row.Entry != null && !string.IsNullOrEmpty(row.Entry.SourcePath);
                    var sourceHelp = row.Entry == null ? "此控件还没有代码声明；选择属性并生成绑定后可查看生成成员。" : IsAutoBinding(row.Entry) ? "打开编译器输出的生成源码；由全局设置中的“输出生成源码供 IDE 使用”控制。文件位于 Library，请勿手动修改。" : string.IsNullOrEmpty(row.Entry.SourcePath) ? "没有可定位的源码信息。" : "打开绑定声明：" + row.Entry.SourcePath + ":" + row.Entry.SourceLine;
                    if (BindingActionButton(L.Get("bindingRow.source"), sourceHelp, 44, canOpenSource))
                        OpenBindingSource(row.Entry);
                    using (new EditorGUI.DisabledScope(row.IsElementBinding))
                        if (GUILayout.Button(new GUIContent("×", L.Get("nativeRules.removeOne.help")), EditorStyles.miniButton, GUILayout.Width(22)))
                        {
                            serializedObject.Update();
                            var saved = serializedObject.FindProperty("nativeBindings");
                            for (var i = saved.arraySize - 1; i >= 0; i--)
                                if (MatchesReference(row, saved.GetArrayElementAtIndex(i)))
                                    saved.DeleteArrayElementAtIndex(i);
                            serializedObject.ApplyModifiedProperties();
                            ClearBindingTargets();
                            results = null;
                        }
                }
                DrawBindingProperties(row);
                var status = row.Error ?? (row.IsElementBinding ? null : row.Target == null ? L.Get("nativeRules.missing") :
                    ((View)target).NativeBindings.Any(b => b != null && b.Name == row.Name && b.Target == row.Target) ? null : L.Get("nativeRules.matched"));
                if (row.Error != null)
                    EditorGUILayout.HelpBox(row.Error, MessageType.Error);
                else if (!string.IsNullOrEmpty(status))
                    EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
            }
            foreach (var error in nativeRuleErrors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            if (!string.IsNullOrEmpty(nativeBindingMessage))
                EditorGUILayout.HelpBox(nativeBindingMessage, MessageType.Info);
            if (!string.IsNullOrEmpty(sourceOpenError))
                EditorGUILayout.HelpBox(sourceOpenError, MessageType.Warning);
        }

        private void QueueBindingValidation()
        {
            if (this == null || !inspectorReady || target == null)
                return;
            validationPending = true;
            results = null;
            validationDue = EditorApplication.timeSinceStartup + 0.3;
            EditorApplication.update -= RunPendingBindingValidation;
            EditorApplication.update += RunPendingBindingValidation;
        }

        private UndoPropertyModification[] OnBindingModifications(UndoPropertyModification[] modifications)
        {
            QueueBindingValidation();
            return modifications;
        }

        private void RunPendingBindingValidation()
        {
            if (this == null || !inspectorReady || target == null)
            {
                EditorApplication.update -= RunPendingBindingValidation;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < validationDue)
                return;
            EditorApplication.update -= RunPendingBindingValidation;
            validationPending = false;
            try
            {
                var rows = BuildControlRows();
                var view = (View)target;
                if (!EditorApplication.isPlayingOrWillChangePlaymode && selected >= 0 &&
                    !string.IsNullOrEmpty(view.AutoBindingsMetadata) &&
                    (EditorUtility.IsPersistent(view) || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject) != null))
                    SyncAndValidateBindings(rows, automatic: true);
                CheckCurrentBindings(rows);
            }
            catch (Exception error) { results = new[] { error.Message }; Repaint(); }
        }

        private static bool BindingActionButton(string label, string tooltip, float width, bool enabled)
        {
            var content = new GUIContent(label, tooltip);
            var rect = GUILayoutUtility.GetRect(content, EditorStyles.miniButton, GUILayout.Width(width));
            bool clicked;
            using (new EditorGUI.DisabledScope(!enabled))
                clicked = GUI.Button(rect, content, EditorStyles.miniButton);
            GUI.Label(rect, new GUIContent("", tooltip), GUIStyle.none);
            return clicked;
        }

        private void CheckCurrentBindings(List<ControlRow> rows)
        {
            var errors = new List<string>();
            validationKind = selected >= 0 ? ValidationKind.Contract : ValidationKind.Structure;
            try
            {
                errors.AddRange(selected >= 0
                    ? ViewContractValidator.Validate((View)target, manifests[selected])
                    : ViewContractValidator.ValidateStructure((View)target));
                errors.AddRange(nativeRuleErrors);
                foreach (var row in rows)
                {
                    if (!string.IsNullOrEmpty(row.Error))
                        errors.Add(row.Name + "：" + row.Error);
                    else if (!row.IsElementBinding && row.Target == null)
                        errors.Add(row.Name + "：缺少控件引用。");
                }
                if (selected >= 0 && errors.Count == 0)
                    BuildAutoBindingMetadata(rows.Where(row => !row.IsElementBinding).ToList(), manifests[selected], captureInitialValues: false);
            }
            catch (Exception error) { errors.Add(error.Message); }
            results = errors.Distinct().ToList();
            Repaint();
        }

        private void SyncAndValidateBindings(List<ControlRow> rows, bool automatic = false)
        {
            var undoGroup = -1;
            var prefabSaved = false;
            try
            {
                if (nativeRuleErrors.Count != 0)
                    throw new InvalidOperationException(L.Get("nativeRules.fixErrors"));
                var bindings = rows.Where(row => !row.IsElementBinding).ToList();
                if (bindings.Count == 0 && FindChildWidgets().Count == 0 &&
                    (selected < 0 || !manifests[selected].Entries.Any(entry => entry.ElementType == typeof(WidgetReference) && IsAutoBinding(entry))))
                    throw new InvalidOperationException(L.Get("nativeRules.empty"));
                if (bindings.Any(row => row.Error != null || row.Target == null || !PageSourceTemplate.IsIdentifier(row.Name)))
                    throw new InvalidOperationException(L.Get("nativeRules.fixErrors"));
                if (bindings.Select(row => row.Target).Distinct().Count() != bindings.Count)
                    throw new InvalidOperationException(L.Get("nativeRules.fixErrors"));
                if (((View)target).NativeBindings.Any(b => b != null && b.Target != null &&
                    bindings.Any(row => row.Target == b.Target && row.Name != b.Name)))
                    throw new InvalidOperationException(L.Get("nativeRules.removeUnused"));
                var prefabPath = CurrentPrefabPath();
                if (string.IsNullOrEmpty(prefabPath))
                    throw new InvalidOperationException(L.Get("nativeRules.savePrefab"));
                if (selected < 0)
                    throw new InvalidOperationException("请先关联页面的 ViewModel，再生成绑定。");
                var children = FindChildWidgets();
                var savedChildren = ((View)target).ChildWidgets;
                var childrenUnchanged = children.Count == savedChildren.Count && children.Select((child, index) =>
                    child != null && savedChildren[index] != null && child.Id == savedChildren[index].Id &&
                    child.Name == savedChildren[index].Name && child.Source == savedChildren[index].Source &&
                    child.Target == savedChildren[index].Target && child.CreateVM == savedChildren[index].CreateVM).All(equal => equal);
                var metadata = BuildAutoBindingMetadata(bindings, manifests[selected]);
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(metadata.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)));
                if (automatic && childrenUnchanged && ((View)target).AutoBindingsMetadata == encoded &&
                    bindings.All(row => ((View)target).NativeBindings.Any(binding => binding != null &&
                        binding.Name == row.Name && binding.Target == row.Target && binding.BindingType == row.ComponentType)))
                    return;
                ValidateAutoBindingOwner(prefabPath, manifests[selected].ViewModelType);
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(L.Get("nativeRules.generate"));
                if (!childrenUnchanged)
                    SaveChildWidgets(children);
                foreach (var row in bindings)
                {
                    var choices = GetBindingChoices(row);
                    SaveControlReference(row, row.Target);
                    SaveBindingChoices(row, choices, separateUndo: false);
                }
                serializedObject.Update();
                serializedObject.FindProperty("muiAutoBindings").stringValue = encoded;
                serializedObject.ApplyModifiedProperties();
                var view = (View)target;
                // Prefab 保存声明元数据；编译器输出属性和绑定，不创建 Assets C# 文件。
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(view.gameObject);
                if (stage != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, prefabPath, out var saved);
                    if (!saved)
                        throw new IOException("Cannot save binding metadata: " + prefabPath);
                }
                else if (EditorUtility.IsPersistent(view))
                {
                    PrefabUtility.SavePrefabAsset(view.transform.root.gameObject, out var saved);
                    if (!saved)
                        throw new IOException("Cannot save binding metadata: " + prefabPath);
                }
                else
                    PrefabUtility.ApplyObjectOverride(view, prefabPath, InteractionMode.UserAction);
                prefabSaved = true;
                PrefabBindingCompilation.Request();
                var hint = CompilerGeneratedSources.AssemblyPrefix(manifests[selected].ViewModelType.Assembly.GetName().Name) + "_" + string.Concat(manifests[selected].ViewModelType.FullName.Select(c => char.IsLetterOrDigit(c) ? c.ToString() : "_" + ((int)c).ToString("X4")));
                var validation = ViewContractValidator.ValidateStructure(view);
                nativeBindingMessage = L.Format("nativeRules.referencesSynced", bindings.Count) +
                    (validation.Count == 0 ? "" : "\n" + string.Join("\n", validation));
                if (!automatic)
                    Debug.LogFormat(validation.Count == 0 ? LogType.Log : LogType.Warning, LogOption.NoStacktrace, view,
                    "{0}\n{1}", nativeBindingMessage, "ViewModel: " + manifests[selected].ViewModelType.FullName + "\n编译器文档: " + hint + ".AutoBindings.g.cs / " + hint + ".Bindings.g.cs\n" + string.Join("\n", metadata.Elements("binding").Select(entry => (string)entry.Attribute("name") + " → " + (string)entry.Attribute("property") + ((string)entry.Attribute("command") == "true" ? "；业务入口 partial void On" + (((string)entry.Attribute("source")).EndsWith("Command", StringComparison.Ordinal) ? ((string)entry.Attribute("source")).Substring(0, ((string)entry.Attribute("source")).Length - 7) : (string)entry.Attribute("source")) + "(CommandContext context)" : ((string)entry.Attribute("readOnly") == "true" ? "；实时读取 ViewModel." : "；赋值 ViewModel.") + (string)entry.Attribute("source")))));
            }
            catch (Exception error)
            {
                if (undoGroup >= 0 && !prefabSaved)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    serializedObject.Update();
                    ClearBindingTargets();
                }
                nativeBindingMessage = error.Message;
                if (!automatic)
                    Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, target, "{0}\n{1}",
                    L.Get("nativeRules.logRejected"), error.Message);
            }
            finally
            {
                if (undoGroup >= 0 && prefabSaved)
                    Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private sealed class ControlRow
        {
            public string Name;
            public bool SharedName;
            public string Key => Name + "|" + (ComponentType?.AssemblyQualifiedName ?? Adapter?.AssemblyQualifiedName);
            public Type Adapter;
            public readonly List<Type> RequiredAdapters = new List<Type>();
            public ControlNamingRule Rule;
            public Type ComponentType;
            public Component Target;
            public BindingEntry Entry;
            public string Error;
            public bool IsElementBinding;
            public bool DeclaredInCode;
            public readonly List<Component> Candidates = new List<Component>();
        }
    }
}
