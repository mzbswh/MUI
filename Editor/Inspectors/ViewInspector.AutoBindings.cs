using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using UnityEditor;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using System.Xml.Linq;
using MUI.UGUI;

namespace MUI.Editor
{
    public sealed partial class ViewInspector
    {
        private static void ValidateAutoBindingOwner(string prefabPath, Type model)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == prefabPath) continue;
                foreach (var line in File.ReadLines(path))
                {
                    const string marker = "muiAutoBindings: ";
                    var text = line.Trim();
                    if (!text.StartsWith(marker, StringComparison.Ordinal)) continue;
                    var encoded = text.Substring(marker.Length).Trim().Trim('"', '\'');
                    if (encoded.Length == 0) continue;
                    var definition = XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
                    if ((string)definition.Attribute("model") == model.FullName && (string)definition.Attribute("assembly") == model.Assembly.GetName().Name)
                        throw new InvalidOperationException(model.FullName + " 已由另一个 Prefab 生成绑定：" + path);
                }
            }
        }

        private XElement BuildAutoBindingMetadata(List<ControlRow> rows, BindingManifest manifest, bool captureInitialValues = true)
        {
            var model = manifest.ViewModelType;
            if (model.IsNested || model.IsGenericType) throw new InvalidOperationException("自动绑定暂不支持嵌套或泛型 ViewModel。");
            var result = new XElement("bindings", new XAttribute("assembly", model.Assembly.GetName().Name), new XAttribute("model", model.FullName));
            var generated = new HashSet<string>(manifest.Entries.Where(entry => IsAutoBinding(entry)).Select(entry => entry.Source));
            var names = new HashSet<string>(StringComparer.Ordinal);
            var writes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                foreach (var choice in GetBindingChoices(row))
                {
                    if (manifest.Entries.Any(entry => entry.ElementName == row.Name && CanonicalBindingProperty(entry.TargetProperty) == CanonicalBindingProperty(choice.Property) && !IsAutoBinding(entry))) continue;
                    using (var adapter = NativeControl.Create(row.Name, row.Target))
                    {
                        var extension = ExtensionType(choice.Extension);
                        if (!string.IsNullOrEmpty(choice.Extension) && (extension == null || ExtensionComponent(extension) == null || !ExtensionComponent(extension).IsInstanceOfType(row.Target) || extension.IsNested || extension.IsAbstract || !extension.IsVisible || extension.ContainsGenericParameters || extension.GetConstructor(new[] { typeof(NativeControl) }) == null))
                            throw new InvalidOperationException("扩展类型不可用：" + choice.Extension);
                        var type = extension ?? (choice.Native ? row.Target.GetType() : adapter.GetType());
                        var property = choice.Property;
                        var mode = choice.Mode;
                        var targetProperty = type.GetProperty(property);
                        var targetEvent = type.GetEvent(property);
                        var command = targetEvent != null;
                        var readOnly = targetProperty != null && (targetProperty.SetMethod == null || !targetProperty.SetMethod.IsPublic);
                        if (command && targetEvent.EventHandlerType != typeof(Action))
                            throw new InvalidOperationException(row.Name + "：自动命令需要 Action 事件。");
                        if (!command && (targetProperty == null || (readOnly && (targetProperty.GetMethod == null || !targetProperty.GetMethod.IsPublic)) || targetProperty.GetIndexParameters().Length != 0))
                            throw new InvalidOperationException(row.Name + "：找不到公开可读写属性 " + property);
                        if (command)
                        {
                            var interactable = type.GetProperty("Interactable");
                            if (interactable == null || interactable.PropertyType != typeof(bool) || interactable.GetMethod == null || interactable.SetMethod == null)
                                throw new InvalidOperationException(row.Name + "：命令适配器需要公开的 bool Interactable 属性。");
                        }
                        if (choice.Native && !readOnly && (command || (mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource) && NativeNotificationProperty(row.Target.GetType(), property) == null))
                            throw new InvalidOperationException(row.Name + "." + property + "：原生属性没有变更通知，只支持单向或首次赋值。");
                        if (extension != null && (command || !readOnly && (mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource) && !targetProperty.IsDefined(typeof(NotifiesChangeAttribute), true)))
                            throw new InvalidOperationException(row.Name + "." + property + "：扩展回写需要 NotifiesChange 和真实变化通知。");
                        if (targetProperty != null && targetProperty.IsDefined(typeof(ObsoleteAttribute), true))
                            throw new InvalidOperationException(row.Name + "." + property + "：属性已弃用，请选择替代属性。");
                        var contract = choice.Native || extension != null ? typeof(NativeControl) : type.GetInterfaces().Where(i => !i.IsGenericType && typeof(IElement).IsAssignableFrom(i) &&
                            (command ? i.GetEvent(property) != null : i.GetProperty(property) != null)).OrderBy(i => i.FullName, StringComparer.Ordinal).FirstOrDefault() ?? type;
                        if (!contract.IsVisible || contract.IsGenericType) throw new InvalidOperationException(row.Name + "：绑定适配器必须是公开的非泛型类型或接口。");
                        if (!writes.Add(row.Name + "\0" + CanonicalBindingProperty(property))) throw new InvalidOperationException("重复绑定目标：" + row.Name + "." + property);
                        var source = choice.Source;
                        if (!PageSourceTemplate.IsIdentifier(source) || !names.Add(source) || source == model.Name) throw new InvalidOperationException("生成成员名称重复：" + source);
                        if (!generated.Contains(source) && model.GetMember(source, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length != 0)
                            throw new InvalidOperationException(source + " 与已有代码成员重名。请为已有成员添加 Bind 声明，或调整控件名称。");
                        var declaration = new XElement("binding", new XAttribute("name", row.Name), new XAttribute("contract", contract.FullName),
                            new XAttribute("source", source), new XAttribute("property", property), new XAttribute("mode", mode), new XAttribute("command", command ? "true" : "false"));
                        if (readOnly) declaration.SetAttributeValue("readOnly", "true");
                        if (extension != null)
                        {
                            declaration.SetAttributeValue("extension", extension.FullName);
                            declaration.SetAttributeValue("component", ExtensionComponent(extension).FullName);
                        }
                        if (choice.Native)
                        {
                            declaration.SetAttributeValue("component", type.FullName);
                            var notification = NativeNotification(row.Target.GetType(), property);
                            if (notification != null)
                            {
                                declaration.SetAttributeValue("notificationEvent", notification.Event);
                                if (!string.IsNullOrEmpty(notification.Setter)) declaration.SetAttributeValue("notificationSetter", notification.Setter);
                            }
                        }
                        if (captureInitialValues && extension == null && !command && !readOnly && targetProperty.GetMethod != null && targetProperty.GetMethod.IsPublic)
                        {
                            var valueType = targetProperty.PropertyType;
                            if (!choice.Native || valueType == typeof(string) || valueType == typeof(bool) || valueType == typeof(int) || valueType == typeof(float) || valueType == typeof(Color) || valueType == typeof(Vector2))
                                CaptureInitialValue(declaration, targetProperty.GetValue(choice.Native ? (object)row.Target : adapter));
                        }
                        result.Add(declaration);
                    }
                }
            }
            return result;
        }

        private static string CanonicalBindingProperty(string property)
        {
            if (property == "Content") return "text";
            return property.ToLowerInvariant();
        }

        private static void CaptureInitialValue(XElement entry, object value)
        {
            if (value is Color color)
                entry.SetAttributeValue("initialColor", string.Join(",", new[] { color.r, color.g, color.b, color.a }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
            else if (value is Vector2 vector)
                entry.SetAttributeValue("initialVector2", string.Join(",", new[] { vector.x, vector.y }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
            else if (value is string || value is bool || value is int || value is float)
                entry.SetAttributeValue("initial", Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static bool IsAutoBinding(BindingEntry entry) => !string.IsNullOrEmpty(entry.SourcePath) && entry.SourcePath.EndsWith(".AutoBindings.g.cs", StringComparison.Ordinal);
    }
}
