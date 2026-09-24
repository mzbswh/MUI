using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    /// <summary>在 Unity 主线程显式执行同步射线查询，不派发事件、不改变焦点。</summary>
    public static class UIRaycastDiagnostics
    {
        [ThreadStatic] private static bool capturing;

        /// <summary>
        /// 使用独立指针数据调用 EventSystem.RaycastAll，执行实际射线过滤及项目回调。
        /// 这不是被动状态读取；自定义射线器可能有副作用或抛出异常，异常直接返回调用者。
        /// maxHits 只限制保留的快照，不能限制原生射线器生成的临时结果数量。
        /// 点击处理目标只作定位，不检查按下、拖动、命令资格或自定义输入模块状态。
        /// </summary>
        public static UIRaycastSnapshot Capture(EventSystem eventSystem, Vector2 position,
            GameObject target = null, int maxHits = 128, int pointerId = -1,
            PointerEventData.InputButton button = PointerEventData.InputButton.Left)
        {
            if (eventSystem == null)
            {
                throw new ArgumentNullException(nameof(eventSystem));
            }
            if (!eventSystem.isActiveAndEnabled)
            {
                throw new InvalidOperationException("射线查询需要启用的 EventSystem。");
            }
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (maxHits < 1 || maxHits > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHits));
            }
            if (!Enum.IsDefined(typeof(PointerEventData.InputButton), button))
            {
                throw new ArgumentOutOfRangeException(nameof(button));
            }
            if (capturing)
            {
                throw new InvalidOperationException("射线诊断不允许从射线器或过滤器内重入。");
            }

            capturing = true;
            try
            {
                var eventSystemId = eventSystem.GetInstanceID();
                var targetId = target == null ? 0 : target.GetInstanceID();
                var data = new PointerEventData(eventSystem)
                {
                    position = position,
                    pointerId = pointerId,
                    button = button
                };
                var results = new List<RaycastResult>();
                eventSystem.RaycastAll(data, results);
                var hits = new List<UIRaycastHitSnapshot>(Math.Min(maxHits, results.Count));
                var total = 0;
                var skipped = 0;
                var firstTarget = -1;
                foreach (var result in results)
                {
                    var hit = result.gameObject;
                    // 与 BaseInputModule.FindFirstRaycast 一致，只跳过无有效对象的结果。
                    if (hit == null)
                    {
                        ++skipped;
                        continue;
                    }
                    var belongs = target != null && hit.transform.IsChildOf(target.transform);
                    if (belongs && firstTarget < 0)
                    {
                        firstTarget = total;
                    }
                    ++total;
                    if (hits.Count >= maxHits)
                    {
                        continue;
                    }
                    var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
                    hits.Add(new UIRaycastHitSnapshot(hit.GetInstanceID(), CapturePath(hit),
                        handler == null ? 0 : handler.GetInstanceID(), CapturePath(handler),
                        result.module == null ? string.Empty : result.module.GetType().FullName,
                        result.distance, result.depth, result.sortingLayer, result.sortingOrder, belongs));
                }
                return new UIRaycastSnapshot(position, eventSystemId, targetId, hits, total, skipped, firstTarget);
            }
            finally
            {
                capturing = false;
            }
        }

        private static string CapturePath(GameObject value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            var segments = new List<string>();
            var node = value.transform;
            while (node != null && segments.Count < 64)
            {
                var name = node.name;
                segments.Add(name.Length > 128 ? name.Substring(0, 128) + "…" : name);
                node = node.parent;
            }
            if (node != null)
            {
                segments.Add("…");
            }
            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
