namespace MUI.Navigation
{
    /// <summary>不包含参数工厂的依赖元数据，供路由图诊断及编辑器读取。</summary>
    public readonly struct RouteDependencyDescriptor
    {
        internal RouteDependencyDescriptor(Route target, DependencyPlacement placement, bool isRequired)
        {
            Target = target;
            Placement = placement;
            IsRequired = isRequired;
        }

        /// <summary>是否要求父页面与此依赖共同成功。</summary>
        public bool IsRequired
        {
            get;
        }

        public Route Target
        {
            get;
        }

        public DependencyPlacement Placement
        {
            get;
        }
    }
}
