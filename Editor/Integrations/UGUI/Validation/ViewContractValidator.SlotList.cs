using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    public static partial class ViewContractValidator
    {
        /// <summary>采用运行时相同的固定容量、挂点与容器归属规则，不初始化原生控件。</summary>
        private static void ValidateSlotList(Element element, Transform root, List<string> errors)
        {
            var serialized = new SerializedObject(element);
            var slots = serialized.FindProperty("slots");
            var mounts = serialized.FindProperty("mounts");
            var template = serialized.FindProperty("template").objectReferenceValue as NestedViewElement;
            var path = Path(element.transform, root);
            var nodes = new List<Transform>();
            if (template == null)
            {
                if (slots.arraySize == 0 || mounts.arraySize != 0)
                {
                    errors.Add(L.Format("editor.ViewContractValidator.SlotList.57b1711cb8", path));
                }

                for (var index = 0; index < slots.arraySize; ++index)
                {
                    var slot = slots.GetArrayElementAtIndex(index).objectReferenceValue as NestedViewElement;
                    if (slot == null || !IsInsideListBoundary(element.transform, slot.transform, true))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.SlotList.8f516c4dbd", path, index));
                        continue;
                    }

                    nodes.Add(slot.transform);
                    ValidateNestedListChild(slot, path + "/" + index, errors);
                }
            }
            else
            {
                if (slots.arraySize != 0 || mounts.arraySize == 0 || template.gameObject.activeSelf ||
                    !IsInsideListBoundary(element.transform, template.transform, true))
                {
                    errors.Add(L.Format("editor.ViewContractValidator.SlotList.8befb07d44", path));
                }

                ValidateNestedListChild(template, path, errors);
                for (var index = 0; index < mounts.arraySize; ++index)
                {
                    var mount = mounts.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    if (mount == null || mount.childCount != 0 || !IsInsideListBoundary(element.transform, mount, false) ||
                        mount.IsChildOf(template.transform) || template.transform.IsChildOf(mount))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.SlotList.001b23e3b7", path, index));
                        continue;
                    }

                    nodes.Add(mount);
                }
            }

            for (var index = 0; index < nodes.Count; ++index)
            {
                for (var previous = 0; previous < index; ++previous)
                {
                    if (nodes[index] == nodes[previous] || nodes[index].IsChildOf(nodes[previous]) ||
                        nodes[previous].IsChildOf(nodes[index]))
                    {
                        errors.Add(L.Format("editor.ViewContractValidator.SlotList.34a7a64966", path));
                    }
                }
            }
        }
    }
}
