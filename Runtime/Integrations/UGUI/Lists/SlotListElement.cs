using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>
    /// 固定容量的数据容器；借用预制子视图，或在明确挂点中持有模板实例。
    /// 共用回收列表的来源通知、子视图生命周期与隔离换绑，不接管槽位布局。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlotListElement : RecyclingListElement
    {
        [SerializeField] private NestedViewElement[] slots = System.Array.Empty<NestedViewElement>();
        [SerializeField] private Transform[] mounts = System.Array.Empty<Transform>();
        [SerializeField] private NestedViewElement template;

        protected override bool UsesFixedSlots => true;

        /// <summary>槽位数量固定，数据超量时拒绝本次赋值并保留原来源。</summary>
        public int Capacity => IsInitialized ? MaterializedCount : template == null ?
            (slots == null ? 0 : slots.Length) : (mounts == null ? 0 : mounts.Length);

        /// <summary>初始化前指定已存在的包装节点；容器只借用原生对象。</summary>
        public void ConfigureSlots(NestedViewElement[] existingSlots)
        {
            RequireFixedConfiguration();
            slots = existingSlots == null ? throw new System.ArgumentNullException(nameof(existingSlots)) :
                (NestedViewElement[])existingSlots.Clone();
            mounts = System.Array.Empty<Transform>();
            template = null;
            ConfigureFixedCapacity(slots.Length);
        }

        /// <summary>初始化前指定空挂点及非激活模板；每个挂点只创建一份由容器持有的实例。</summary>
        public void ConfigureMounts(Transform[] contentMounts, NestedViewElement itemTemplate)
        {
            RequireFixedConfiguration();
            if (contentMounts == null)
            {
                throw new System.ArgumentNullException(nameof(contentMounts));
            }

            if (itemTemplate == null)
            {
                throw new System.ArgumentNullException(nameof(itemTemplate));
            }

            mounts = (Transform[])contentMounts.Clone();
            slots = System.Array.Empty<NestedViewElement>();
            template = itemTemplate;
            ConfigureFixedCapacity(mounts.Length);
        }

        protected override void OnInitialize()
        {
            ConfigureFixedNodes(slots, mounts, template);
            base.OnInitialize();
        }
    }
}
