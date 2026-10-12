namespace MUI.Pages
{
    /// <summary>不包含参数工厂的依赖元数据，供页面定义图诊断及编辑器读取。</summary>
    public readonly struct PageDependencyDescriptor
    {
        internal PageDependencyDescriptor(PageDefinition target, DependencyPlacement placement, bool isRequired, DependencyMissingPolicy missingPolicy)
        {
            Target = target;
            Placement = placement;
            IsRequired = isRequired;
            MissingPolicy = missingPolicy;
        }

        /// <summary>是否要求父页面与此依赖共同成功。</summary>
        public bool IsRequired
        {
            get;
        }

        public DependencyMissingPolicy MissingPolicy
        {
            get;
        }

        public PageDefinition Target
        {
            get;
        }

        public DependencyPlacement Placement
        {
            get;
        }
    }
}
