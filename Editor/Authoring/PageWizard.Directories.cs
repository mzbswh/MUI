using System;
using System.Collections.Generic;
using System.IO;
using MUI.UGUI;
using UnityEditor;

namespace MUI.Editor
{
    public sealed partial class PageWizard
    {
        private string ScriptDirectory => MUISettings.ResolvePageDirectory(
            settings == null ? MUISettings.DefaultPageScriptDirectory : settings.PageScriptDirectory, pageName);
        private string PrefabDirectory => MUISettings.ResolvePageDirectory(
            settings == null ? MUISettings.DefaultPagePrefabDirectory : settings.PagePrefabDirectory, pageName);
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

        private IReadOnlyDictionary<string, string> CreatePageSources() => PageSourceTemplate.Create(
            pageName, targetNamespace, presetName, ReceivesInput, createPresenter, typedContracts,
            ExistingTitleBindingType,
            existingPrefab == null ? "Title" : titleElementName,
            existingPrefab == null ? "Close" : closeElementName, AssetDatabase.AssetPathToGUID(PrefabPath), PageResourceKey);

        private Type ExistingTitleBindingType => existingPrefab != null &&
            existingPrefab.GetComponent<View>() != null && existingPrefab.GetComponent<View>().NativeBindings.Count == 0
                ? PageTextBackend.Find(textBackendId).LegacyElementType
                : PageTextBackend.Find(textBackendId).ElementType;

        private static bool PathOccupied(string path) => File.Exists(path) || Directory.Exists(path) ||
            File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;

        private static string DirectoryConflict(string path)
        {
            while (!string.IsNullOrEmpty(path) && path != "Assets")
            {
                if (!AssetDatabase.IsValidFolder(path) && PathOccupied(path)) return path;
                path = Path.GetDirectoryName(path).Replace('\\', '/');
            }
            return null;
        }

        private static void EnsurePageDirectory(string path, List<string> createdDirectories)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (PathOccupied(path)) throw new IOException("Page directory is occupied: " + path);
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsurePageDirectory(parent, createdDirectories);
            var guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            if (string.IsNullOrEmpty(guid)) throw new IOException("Cannot create page directory: " + path);
            var actual = AssetDatabase.GUIDToAssetPath(guid);
            createdDirectories.Add(actual);
            if (actual != path) throw new IOException("Page directory changed during creation: " + path);
        }

        private static bool RollbackPageFiles(List<string> files, List<string> directories)
        {
            var removed = true;
            for (var i = files.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (File.Exists(files[i])) File.Delete(files[i]);
                    if (File.Exists(files[i] + ".meta")) File.Delete(files[i] + ".meta");
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
