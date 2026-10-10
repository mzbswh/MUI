using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MUI.Editor.Localization
{
    /// <summary>从编辑器语言资源取词；不修改项目配置、序列化值或运行时语言。</summary>
    [InitializeOnLoad]
    public static class MUIEditorLocalization
    {
        private const string PreferenceKey = "MUI.Editor.Language";
        private const string DefaultLanguageId = "zh-CN";
        internal const string ResourceSuffix = ".mui-language.json";
        private static readonly Dictionary<string, Language> languages = new Dictionary<string, Language>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> diagnosticKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<DiagnosticTemplate> diagnosticTemplates = new List<DiagnosticTemplate>();
        private static string requestedLanguage = EditorPrefs.GetString(PreferenceKey, DefaultLanguageId);
        private static bool loaded;
        private static bool reloadScheduled;

        static MUIEditorLocalization() => ScheduleReload();

        public static event Action Changed;

        public static IReadOnlyList<Language> Languages
        {
            get
            {
                EnsureLoaded();
                return languages.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
            }
        }

        public static string LanguageId
        {
            get
            {
                EnsureLoaded();
                return languages.ContainsKey(requestedLanguage) ? requestedLanguage : DefaultLanguageId;
            }
            set
            {
                EnsureLoaded();
                if (string.IsNullOrEmpty(value) || !languages.ContainsKey(value) || value == LanguageId)
                {
                    return;
                }
                EditorPrefs.SetString(PreferenceKey, value);
                requestedLanguage = value;
                NotifyChanged();
            }
        }

        public static string Get(string key) => Get(key, "[" + key + "]");

        public static string Get(string key, string defaultText)
        {
            EnsureLoaded();
            return TryGet(LanguageId, key, new HashSet<string>(StringComparer.Ordinal), out var text) ||
                TryGet(DefaultLanguageId, key, new HashSet<string>(StringComparer.Ordinal), out text)
                ? text : defaultText;
        }

        public static string Format(string key, params object[] arguments)
        {
            var values = Array.ConvertAll(arguments ?? Array.Empty<object>(), DisplayValue);
            EnsureLoaded();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            if (TryFormat(LanguageId, key, values, visited, out var text) ||
                TryFormat(DefaultLanguageId, key, values, visited, out text))
            {
                return text;
            }
            return Get(key);
        }

        public static string Value(object value) => Convert.ToString(DisplayValue(value), CultureInfo.CurrentCulture);

        public static T EnumPopup<T>(string label, T value) where T : Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var names = Array.ConvertAll(values, item => EnumName(typeof(T), item.ToString()));
            var index = Array.IndexOf(values, value);
            var next = EditorGUILayout.Popup(label, index, names);
            return next >= 0 && next < values.Length ? values[next] : value;
        }

        public static string Diagnostic(string source) => TranslateDiagnostic(source, 0);

        private static string TranslateDiagnostic(string source, int depth)
        {
            if (string.IsNullOrEmpty(source) || depth >= 8)
            {
                return source;
            }
            EnsureLoaded();
            if (diagnosticKeys.TryGetValue(source, out var key) && (depth == 0 || key.StartsWith("diagnostic.", StringComparison.Ordinal)))
            {
                return Get(key);
            }
            foreach (var template in diagnosticTemplates)
            {
                if (depth > 0 && !template.Key.StartsWith("diagnostic.", StringComparison.Ordinal))
                {
                    continue;
                }
                var match = template.Pattern.Match(source);
                if (!match.Success)
                {
                    continue;
                }
                var arguments = new object[template.ArgumentCount];
                for (var i = 0; i < arguments.Length; ++i)
                {
                    var value = match.Groups["arg" + i].Value;
                    arguments[i] = bool.TryParse(value, out var flag) ? (object)flag :
                        value.Length < source.Length ? TranslateDiagnostic(value, depth + 1) : value;
                }
                return Format(template.Key, arguments);
            }
            return source;
        }

        private static object DisplayValue(object value)
        {
            if (value is bool flag)
            {
                return Get(flag ? "value.true" : "value.false");
            }
            if (value is Enum enumeration)
            {
                var type = enumeration.GetType();
                return string.Join(Get("value.listSeparator"), enumeration.ToString().Split(',')
                    .Select(item => EnumName(type, item.Trim())));
            }
            return value;
        }

        public static string EnumName(Type type, string name)
        {
            var key = "enum." + type.FullName + "." + name;
            var text = Get(key);
            return text == "[" + key + "]" ? ObjectNames.NicifyVariableName(name) : text;
        }

        public static string PropertyName(SerializedProperty property)
        {
            if (property.propertyPath.EndsWith("]", StringComparison.Ordinal) &&
                property.propertyPath.LastIndexOf(".Array.data[", StringComparison.Ordinal) >= 0)
            {
                var start = property.propertyPath.LastIndexOf('[') + 1;
                return Format("property.arrayElement", property.propertyPath.Substring(start, property.propertyPath.Length - start - 1));
            }
            var key = "property." + property.name;
            var text = Get(key);
            return text == "[" + key + "]" ? property.displayName : text;
        }

        public static void BindTree(VisualElement root)
        {
            var bindings = root.Query<TextElement>().ToList().Where(item => item.text != null && item.text.StartsWith("@", StringComparison.Ordinal))
                .Select(item => (element: item, key: item.text.Substring(1))).ToArray();
            void Refresh()
            {
                foreach (var item in bindings)
                {
                    item.element.text = Get(item.key);
                }
            }
            Refresh();
            Track(root, Refresh);
        }

        public static void Track(VisualElement root, Action refresh)
        {
            if (root.panel != null)
            {
                Changed -= refresh;
                Changed += refresh;
            }
            root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                if (evt.target != root)
                {
                    return;
                }
                Changed -= refresh;
                Changed += refresh;
                refresh();
            });
            root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target == root)
                {
                    Changed -= refresh;
                }
            });
        }

        public static void ReloadLanguages()
        {
            languages.Clear();
            diagnosticKeys.Clear();
            diagnosticTemplates.Clear();
            loaded = true;
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset").OrderBy(item => item, StringComparer.Ordinal))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                try
                {
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset == null)
                    {
                        continue;
                    }
                    var resource = JsonUtility.FromJson<LanguageResource>(asset.text);
                    if (resource == null || string.IsNullOrWhiteSpace(resource.id) || string.IsNullOrWhiteSpace(resource.displayName) || resource.entries == null)
                    {
                        throw new InvalidDataException("Language resources require id, displayName and entries.");
                    }
                    var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var entry in resource.entries)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entry.text == null || entries.ContainsKey(entry.key))
                        {
                            throw new InvalidDataException("Language entries require unique keys and text.");
                        }
                        entries.Add(entry.key, entry.text);
                    }
                    if (languages.ContainsKey(resource.id))
                    {
                        throw new InvalidDataException("Duplicate language id: " + resource.id);
                    }
                    languages.Add(resource.id, new Language(resource.id, resource.displayName, resource.fallbackLanguageId, entries));
                    foreach (var entry in resource.entries)
                    {
                        AddDiagnostic(entry.key, entry.source);
                        if (entry.key.StartsWith("editor.", StringComparison.Ordinal) || entry.key.StartsWith("diagnostic.", StringComparison.Ordinal))
                        {
                            AddDiagnostic(entry.key, entry.text);
                        }
                    }
                }
                catch (Exception error)
                {
                    Debug.LogWarning("MUI language resource " + path + ": " + error.Message);
                }
            }
            diagnosticTemplates.Sort((left, right) => right.LiteralLength.CompareTo(left.LiteralLength));
            NotifyChanged();
        }

        private static void AddDiagnostic(string key, string source)
        {
            if (string.IsNullOrEmpty(source) || diagnosticKeys.ContainsKey(source))
            {
                return;
            }
            diagnosticKeys.Add(source, key);
            var placeholders = Regex.Matches(source, @"\{(\d+)(?:[^{}]*)\}");
            if (placeholders.Count == 0)
            {
                return;
            }
            var pattern = new StringBuilder("\\A");
            var offset = 0;
            var count = 0;
            var captured = new HashSet<int>();
            foreach (Match placeholder in placeholders)
            {
                pattern.Append(Regex.Escape(source.Substring(offset, placeholder.Index - offset)));
                var index = int.Parse(placeholder.Groups[1].Value, CultureInfo.InvariantCulture);
                pattern.Append(captured.Add(index) ? "(?<arg" + index + ">.*?)" : "\\k<arg" + index + ">");
                count = Math.Max(count, index + 1);
                offset = placeholder.Index + placeholder.Length;
            }
            pattern.Append(Regex.Escape(source.Substring(offset))).Append("\\z");
            diagnosticTemplates.Add(new DiagnosticTemplate
            {
                Key = key,
                ArgumentCount = count,
                LiteralLength = source.Length - placeholders.Cast<Match>().Sum(item => item.Length),
                Pattern = new Regex(pattern.ToString(), RegexOptions.Singleline)
            });
        }

        internal static void ScheduleReload()
        {
            if (reloadScheduled)
            {
                return;
            }
            reloadScheduled = true;
            EditorApplication.delayCall += () =>
            {
                reloadScheduled = false;
                ReloadLanguages();
            };
        }

        private static void EnsureLoaded()
        {
            if (!loaded)
            {
                ReloadLanguages();
            }
        }

        private static bool TryGet(string id, string key, HashSet<string> visited, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(id) || !visited.Add(id) || !languages.TryGetValue(id, out var language))
            {
                return false;
            }
            return language.Entries.TryGetValue(key, out text) || TryGet(language.FallbackLanguageId, key, visited, out text);
        }

        private static bool TryFormat(string id, string key, object[] arguments, HashSet<string> visited, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(id) || !visited.Add(id) || !languages.TryGetValue(id, out var language))
            {
                return false;
            }
            if (language.Entries.TryGetValue(key, out var format))
            {
                try
                {
                    text = string.Format(CultureInfo.CurrentCulture, format, arguments);
                    return true;
                }
                catch (FormatException)
                {
                    // 损坏的翻译只影响当前词条，继续沿资源声明的回退语言查找。
                }
            }
            return TryFormat(language.FallbackLanguageId, key, arguments, visited, out text);
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        public sealed class Language
        {
            internal Language(string id, string displayName, string fallbackLanguageId, Dictionary<string, string> entries)
            {
                Id = id;
                DisplayName = displayName;
                FallbackLanguageId = fallbackLanguageId;
                Entries = entries;
            }

            public string Id { get; }

            public string DisplayName { get; }

            internal string FallbackLanguageId { get; }

            internal Dictionary<string, string> Entries { get; }
        }

        [Serializable]
        private sealed class LanguageResource
        {
            public string id = string.Empty;
            public string displayName = string.Empty;
            public string fallbackLanguageId = string.Empty;
            public Entry[] entries = Array.Empty<Entry>();
        }

        [Serializable]
        private sealed class Entry
        {
            public string key = string.Empty;
            public string text = string.Empty;
            public string source = string.Empty;
        }

        private sealed class DiagnosticTemplate
        {
            public string Key;
            public int ArgumentCount;
            public int LiteralLength;
            public Regex Pattern;
        }
    }

    internal sealed class MUIEditorLanguagePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(path => path.EndsWith(MUIEditorLocalization.ResourceSuffix, StringComparison.OrdinalIgnoreCase)))
            {
                MUIEditorLocalization.ScheduleReload();
            }
        }
    }
}
