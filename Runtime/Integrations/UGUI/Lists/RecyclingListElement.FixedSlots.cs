using System;
using System.Collections.Generic;
using UnityEngine;

namespace MUI.UGUI
{
    public partial class RecyclingListElement
    {
        private NestedViewElement[] fixedSlots;
        private Transform[] fixedMounts;
        private NestedViewElement fixedTemplate;
        private readonly Dictionary<NestedViewElement, Transform> fixedCellParents = new Dictionary<NestedViewElement, Transform>();
        private readonly HashSet<NestedViewElement> ownedFixedCells = new HashSet<NestedViewElement>();

        protected bool IsInitialized => initialized;

        protected void RequireFixedConfiguration()
        {
            RequireListAlive();
            if (initialized || !UsesFixedSlots)
            {
                throw new InvalidOperationException("Configure fixed slots before initialization.");
            }
        }

        protected void ConfigureFixedCapacity(int count)
        {
            RequireFixedConfiguration();
            if (count < 1 || snapshot.Count > count)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Fixed slots require positive capacity sufficient for current data.");
            }

            capacity = count;
        }

        protected void ConfigureFixedNodes(NestedViewElement[] slots, Transform[] mounts, NestedViewElement template)
        {
            RequireFixedConfiguration();
            fixedSlots = slots ?? Array.Empty<NestedViewElement>();
            fixedMounts = mounts ?? Array.Empty<Transform>();
            fixedTemplate = template;
            ConfigureFixedCapacity(template == null ? fixedSlots.Length : fixedMounts.Length);
        }

        private void InitializeFixedSlots()
        {
            var nodes = new List<Transform>();
            if (fixedTemplate == null)
            {
                if (fixedMounts.Length != 0)
                {
                    throw new InvalidOperationException("Choose existing fixed slots or template mounts, not both.");
                }

                foreach (var slot in fixedSlots)
                {
                    if (slot == null || !IsOwnedDescendant(slot.transform, true))
                    {
                        throw new InvalidOperationException("Every fixed slot must be inside this container boundary.");
                    }

                    nodes.Add(slot.transform);
                }
            }
            else
            {
                if (fixedSlots.Length != 0 || fixedTemplate.gameObject.activeSelf ||
                    !IsOwnedDescendant(fixedTemplate.transform, true))
                {
                    throw new InvalidOperationException("Mounted slots require an inactive owned template and no existing slots.");
                }

                foreach (var mount in fixedMounts)
                {
                    if (mount == null || mount.childCount != 0 || !IsOwnedDescendant(mount, false) ||
                        mount.IsChildOf(fixedTemplate.transform) || fixedTemplate.transform.IsChildOf(mount))
                    {
                        throw new InvalidOperationException("Fixed mounts must be empty owned descendants outside the template.");
                    }

                    nodes.Add(mount);
                }
            }

            for (var i = 0; i < nodes.Count; ++i)
            {
                for (var j = 0; j < i; ++j)
                {
                    if (nodes[i] == nodes[j] || nodes[i].IsChildOf(nodes[j]) || nodes[j].IsChildOf(nodes[i]))
                    {
                        throw new InvalidOperationException("Fixed slots must be distinct and cannot contain each other.");
                    }
                }
            }

            // 验证全部归属后才创建节点；初始化失败也由同一清理入口释放已接管内容。
            try
            {
                for (var index = 0; index < nodes.Count; ++index)
                {
                    var slot = fixedTemplate == null ? fixedSlots[index] : Instantiate(fixedTemplate, fixedMounts[index], false);
                    if (fixedTemplate != null)
                    {
                        ownedFixedCells.Add(slot);
                    }

                    cells.Add(slot);
                    fixedCellParents.Add(slot, slot.transform.parent);
                    slot.Initialize();
                }
            }
            catch (Exception failure)
            {
                try
                {
                    ReleaseCells();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Fixed slot initialization and cleanup failed.", failure, cleanup);
                }

                throw;
            }
        }
    }
}
