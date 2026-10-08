using System.Collections.Generic;
using MUI.UGUI;
using UnityEditor;
using UnityEngine;

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
                    errors.Add($"固定槽位必须配置已有节点，或配置模板与空挂点：{path}。");
                }

                for (var index = 0; index < slots.arraySize; ++index)
                {
                    var slot = slots.GetArrayElementAtIndex(index).objectReferenceValue as NestedViewElement;
                    if (slot == null || !IsInsideListBoundary(element.transform, slot.transform, true))
                    {
                        errors.Add($"固定槽位必须是当前容器边界内的 NestedViewElement：{path}/{index}。");
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
                    errors.Add($"动态固定槽位需要非激活模板和空挂点，不能同时配置已有节点：{path}。");
                }

                ValidateNestedListChild(template, path, errors);
                for (var index = 0; index < mounts.arraySize; ++index)
                {
                    var mount = mounts.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    if (mount == null || mount.childCount != 0 || !IsInsideListBoundary(element.transform, mount, false) ||
                        mount.IsChildOf(template.transform) || template.transform.IsChildOf(mount))
                    {
                        errors.Add($"固定挂点必须是容器边界内的空节点，并与模板分离：{path}/{index}。");
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
                        errors.Add($"固定槽位不能重复引用或互相包含：{path}。");
                    }
                }
            }
        }
    }
}
