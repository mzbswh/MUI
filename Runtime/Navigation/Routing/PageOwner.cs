using System;

namespace MUI.Navigation
{
    public enum PageRole
    {
        Independent,
        Main,
        Overlay
    }

    public enum OwnerDeparture
    {
        Close,
        Hide
    }

    public enum PageOwnerKind
    {
        CurrentPage,
        Host,
        Page
    }

    /// <summary>打开时固定的归属；默认绑定当前主页面，宿主归属必须显式指定。</summary>
    public readonly struct PageOwner
    {
        private PageOwner(PageOwnerKind kind, ViewHandle page)
        {
            Kind = kind;
            Page = page;
        }

        public PageOwnerKind Kind
        {
            get;
        }

        public ViewHandle Page
        {
            get;
        }

        public static PageOwner Current => default;

        public static PageOwner Host => new PageOwner(PageOwnerKind.Host, default);

        public static PageOwner For(ViewHandle page)
        {
            if (!page.IsValid)
            {
                throw new ArgumentException("页面归属需要有效实例句柄。", nameof(page));
            }
            return new PageOwner(PageOwnerKind.Page, page);
        }
    }
}
