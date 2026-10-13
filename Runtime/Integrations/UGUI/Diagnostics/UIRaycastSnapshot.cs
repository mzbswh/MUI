using System.Collections.Generic;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>一次显式射线查询的元数据副本，不保留场景对象或事件数据。</summary>
    public sealed class UIRaycastSnapshot
    {
        internal UIRaycastSnapshot(Vector2 position, int eventSystemId, int targetId,
                    List<UIRaycastHitSnapshot> hits, int totalHits, int skippedHits, int firstTargetIndex)
        {
            Position = position;
            EventSystemId = eventSystemId;
            TargetId = targetId;
            Hits = hits.AsReadOnly();
            TotalHits = totalHits;
            SkippedHits = skippedHits;
            FirstTargetIndex = firstTargetIndex;
        }

        /// <summary>以屏幕左下角为原点的像素坐标。</summary>
        public Vector2 Position
        {
            get;
        }

        public int EventSystemId
        {
            get;
        }

        /// <summary>用于判断所属层级的目标；未指定时为 0。</summary>
        public int TargetId
        {
            get;
        }

        /// <summary>保持 EventSystem 排序的前若干有效对象命中。</summary>
        public IReadOnlyList<UIRaycastHitSnapshot> Hits
        {
            get;
        }

        public int TotalHits
        {
            get;
        }

        /// <summary>查询后对象已失效、因而被跳过的原始结果数。</summary>
        public int SkippedHits
        {
            get;
        }

        public bool IsTruncated => TotalHits > Hits.Count;

        /// <summary>目标或其后代的首个有效命中序号；不存在为 -1，可能超出保留列表。</summary>
        public int FirstTargetIndex
        {
            get;
        }

        public bool TopHitBelongsToTarget => FirstTargetIndex == 0;
    }

    /// <summary>单次命中的排序信息与点击处理目标；存在处理目标不代表点击一定执行。</summary>
    public sealed class UIRaycastHitSnapshot
    {
        internal UIRaycastHitSnapshot(int objectId, string path, int handlerId, string handlerPath,
                    string raycasterType, float distance, int depth, int sortingLayer, int sortingOrder,
                    bool belongsToTarget)
        {
            ObjectId = objectId;
            Path = path;
            ClickHandlerId = handlerId;
            ClickHandlerPath = handlerPath;
            RaycasterType = raycasterType;
            Distance = distance;
            Depth = depth;
            SortingLayer = sortingLayer;
            SortingOrder = sortingOrder;
            BelongsToTarget = belongsToTarget;
        }

        public int ObjectId
        {
            get;
        }

        /// <summary>层级路径最多 64 层，每层名称最多 128 字符，截断处使用省略号。</summary>
        public string Path
        {
            get;
        }

        /// <summary>最近的 IPointerClickHandler 所在对象编号；不存在为 0。</summary>
        public int ClickHandlerId
        {
            get;
        }

        public string ClickHandlerPath
        {
            get;
        }

        public string RaycasterType
        {
            get;
        }

        public float Distance
        {
            get;
        }

        public int Depth
        {
            get;
        }

        public int SortingLayer
        {
            get;
        }

        public int SortingOrder
        {
            get;
        }

        public bool BelongsToTarget
        {
            get;
        }
    }
}
