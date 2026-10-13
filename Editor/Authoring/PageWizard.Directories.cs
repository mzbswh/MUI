using System;
using System.Collections.Generic;
using System.IO;
using MUI.UGUI;
using UnityEditor;

namespace MUI.Editor
{
    public sealed partial class PageWizard
    {
        private string ScriptDirectory => MUIEditorSettings.ResolvePageDirectory(
            MUIEditorSettings.instance.PageScriptDirectory, pageName);

        private string PrefabDirectory => MUIEditorSettings.ResolvePageDirectory(
            MUIEditorSettings.instance.PagePrefabDirectory, pageName);

        private string PrefabPath => existingPrefab == null ? PrefabDirectory + "/" + pageName + "View.prefab"
            : AssetDatabase.GetAssetPath(existingPrefab);

        private string PageResourceKey
        {
            get
            {
                var path = PrefabPath;
                var marker = path.LastIndexOf("/Resources/", StringComparison.Ordinal);
                return marker < 0 ? pageName + "View" : path.Substring(marker + "/Resources/".Length, path.Length - marker - "/Resources/".Length - ".prefab".Length);
            }
        }

        private bool UsesAutomaticPageBindings => existingPrefab == null;

        private IReadOnlyDictionary<string, string> CreatePageSources() => createWidget ? CreateWidgetSources() : PageSourceTemplate.Create(
            pageName, targetNamespace, presetName, ReceivesInput, createPresenter, typedContracts,
            PageTextBackend.Find(textBackendId).ComponentType,
            existingPrefab == null ? "Title" : titleElementName,
            existingPrefab == null ? "Close" : closeElementName, PageResourceKey, UsesAutomaticPageBindings, settings == null ? "Default" : settings.DefaultPreset);

        private void SaveExistingPageAssociation(string path, string assemblyName)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<View>();
                if (view == null)
                    throw new InvalidOperationException("Prefab 根节点没有 View。");
                var metadata = string.IsNullOrEmpty(view.AutoBindingsMetadata) ? new System.Xml.Linq.XElement("bindings") :
                    System.Xml.Linq.XElement.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(view.AutoBindingsMetadata)));
                metadata.SetAttributeValue("assembly", assemblyName);
                metadata.SetAttributeValue("model", targetNamespace + "." + pageName + "ViewModel");
                var serialized = new SerializedObject(view);
                serialized.FindProperty("muiAutoBindings").stringValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(metadata.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new IOException("保存 Prefab 关联失败：" + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static bool PathOccupied(string path) => File.Exists(path) || Directory.Exists(path) ||
            File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;

        private static string DirectoryConflict(string path)
        {
            while (!string.IsNullOrEmpty(path) && path != "Assets")
            {
                if (!AssetDatabase.IsValidFolder(path) && PathOccupied(path))
                    return path;
                path = Path.GetDirectoryName(path).Replace('\\', '/');
            }
            return null;
        }

        private static void EnsurePageDirectory(string path, List<string> createdDirectories)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            if (PathOccupied(path))
                throw new IOException("Page directory is occupied: " + path);
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsurePageDirectory(parent, createdDirectories);
            var guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            if (string.IsNullOrEmpty(guid))
                throw new IOException("Cannot create page directory: " + path);
            var actual = AssetDatabase.GUIDToAssetPath(guid);
            createdDirectories.Add(actual);
            if (actual != path)
                throw new IOException("Page directory changed during creation: " + path);
        }

        private static bool RollbackPageFiles(List<string> files, List<string> directories)
        {
            var removed = true;
            for (var i = files.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (File.Exists(files[i]))
                        File.Delete(files[i]);
                    if (File.Exists(files[i] + ".meta"))
                        File.Delete(files[i] + ".meta");
                }
                catch (Exception) { removed = false; }
            }
            for (var i = directories.Count - 1; i >= 0; i--)
            {
                try
                {
                    // 不删除被其他操作写入内容的目录，也不触碰创建前已有的共享目录。
                    if (Directory.Exists(directories[i]) && Directory.GetFileSystemEntries(directories[i]).Length == 0)
                        removed &= AssetDatabase.DeleteAsset(directories[i]);
                }
                catch (Exception) { removed = false; }
            }
            return removed;
        }
    }
}
