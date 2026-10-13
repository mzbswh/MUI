using System;
using UnityEngine;
using UnityEngine.UI;

namespace MUI.Examples
{
    /// <summary>工作台教学面板。说明来自 JSON；界面引用在场景中配置。</summary>
    public sealed class ExampleLessonGuide : MonoBehaviour
    {
        [SerializeField] private TextAsset content = null;
        [SerializeField] private Text heading = null;
        [SerializeField] private Text body = null;
        [SerializeField] private Button[] sections = null;
        [SerializeField] private Button sourceButton = null;
        [SerializeField] private Button prefabButton = null;
        [SerializeField] private Button readmeButton = null;
        private Catalog catalog;

        [SerializeField] private ScrollRect lessonScroll = null;
        private int lessonIndex;
        private int sectionIndex;

        private void Awake()
        {
            catalog = JsonUtility.FromJson<Catalog>(content.text);
            for (var i = 0; i < sections.Length; i++)
            {
                var section = i;
                sections[i].onClick.AddListener(() => SelectSection(section));
            }
            sourceButton.onClick.AddListener(() => OpenAsset(catalog.lessons[lessonIndex].source));
            prefabButton.onClick.AddListener(() => OpenAsset(catalog.lessons[lessonIndex].prefab));
            readmeButton.onClick.AddListener(() => OpenAsset(catalog.lessons[lessonIndex].readme));
#if !UNITY_EDITOR
            sourceButton.interactable = false;
            prefabButton.interactable = false;
            readmeButton.interactable = false;
#endif
        }

        public void ShowLesson(int index)
        {
            if (index < 0 || index >= catalog.lessons.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            lessonIndex = index;
            if (lessonScroll != null)
            {
                lessonScroll.StopMovement();
                lessonScroll.verticalNormalizedPosition = 1;
            }
            Render();
        }

        public void SelectSection(int index)
        {
            if (index < 0 || index >= sections.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            sectionIndex = index;
            Render();
        }

        private void Render()
        {
            var lesson = catalog.lessons[lessonIndex];
            heading.text = lesson.title;
            body.text = sectionIndex == 0 ? lesson.overview : sectionIndex == 1 ? lesson.setup : lesson.code;
            for (var i = 0; i < sections.Length; i++)
            {
                var colors = sections[i].colors;
                colors.normalColor = i == sectionIndex ? new Color(0.88f, 0.85f, 0.99f) : new Color(0.96f, 0.96f, 0.98f);
                sections[i].colors = colors;
            }
        }

        private static void OpenAsset(string relativePath)
        {
#if UNITY_EDITOR
            // 用稳定脚本 GUID 定位导入根目录，兼容用户移动示例文件夹。
            var script = UnityEditor.AssetDatabase.GUIDToAssetPath("017e24f6abae42198a6ab15730f7ddc1");
            var root = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(script)).Replace('\\', '/');
            var asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(root + "/" + relativePath);
            if (asset == null)
            {
                Debug.LogError("示例文件不存在：" + relativePath);
                return;
            }
            UnityEditor.EditorGUIUtility.PingObject(asset);
            UnityEditor.AssetDatabase.OpenAsset(asset);
#endif
        }

        [Serializable]
        private sealed class Catalog
        {
            public Lesson[] lessons = null;
        }

        [Serializable]
        private sealed class Lesson
        {
            public string title = null;
            public string overview = null;
            public string setup = null;
            public string code = null;
            public string source = null;
            public string prefab = null;
            public string readme = null;
        }
    }
}
