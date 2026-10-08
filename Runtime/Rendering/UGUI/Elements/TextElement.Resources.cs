using System;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class TextElement
    {
        private ElementResourceOwner<Font> fontResources;

        /// <summary>成功显示的资源键；赋值请求加载，空值清空，失败保留旧资源。</summary>
        public string FontSource
        {
            get
            {
                RequireAlive();
                return fontResources == null ? null : fontResources.Source;
            }
            set
            {
                RequireAlive();
                if (fontResources == null)
                {
                    ConfigureResourceSource(ConfigureFontSource);
                }

                if (fontResources == null)
                {
                    throw new InvalidOperationException("请先配置资源键加载器和生命周期。");
                }

                fontResources.SetSource(value);
            }
        }

        /// <summary>最新请求键、当前显示键及加载结果；重新设置请求键可显式重试。</summary>
        public ResourceSourceSnapshot FontSourceSnapshot
        {
            get
            {
                RequireAlive();
                return fontResources == null ? default : fontResources.SourceSnapshot;
            }
        }

        protected override string GetBindingWriteTarget(string propertyName, BindingMode mode)
        {
            if (propertyName != nameof(FontSource))
            {
                return base.GetBindingWriteTarget(propertyName, mode);
            }

            if (mode != BindingMode.OneWay && mode != BindingMode.OneTime)
            {
                throw new InvalidOperationException("资源键仅支持 OneWay 或 OneTime 绑定。");
            }

            return nameof(Font);
        }

        /// <summary>在绑定前配置异步资源键加载；所属生命周期跟踪加载并等待释放。</summary>
        public void ConfigureFontSource(LifetimeScope owner, IResourceLoader loader)
        {
            CreateFontSlot(owner, loader);
            fontResources.ConfigureSource(owner, () => NotifyChanged(nameof(FontSource)),
                () => NotifyChanged(nameof(FontSourceSnapshot)),
                CreateResourcePreparationTracker(fontResources, owner, nameof(FontSource)));
        }

        /// <summary>
        /// 创建异步字体资源槽，由指定生命周期等待加载结束并归还资源。
        /// 使用 ReplaceAsync/ClearAsync 更新字体；所属生命周期等待在途加载与实际归还。
        /// </summary>
        private ResourceSlot<Font> CreateFontSlot(LifetimeScope owner, IResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateFontResources(owner, () =>
                new ResourceSlot<Font>(loader, AssignResourceFont, owner.Token));
        }

        private ResourceSlot<Font> CreateFontResources(
                    LifetimeScope owner, Func<ResourceSlot<Font>> create)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireAlive();
            RequireUnmanagedFont();

            return owner.Run(_ =>
            {
                Initialize();
                var registration = new ElementResourceOwner<Font>(ReleaseFontOwner);
                // 先登记空持有者，构造失败不会遗留无人等待的异步资源槽。
                owner.Own(registration);

                registration.Slot = create();
                fontResources = registration;
                return registration.Slot;
            });
        }

        private void RequireUnmanagedFont()
        {
            if (fontResources != null)
            {
                throw new InvalidOperationException("字体已有资源拥有者，不能再次配置。");
            }
        }

        private void AssignResourceFont(Font font)
        {
            // 真正的 null 表示清空；已销毁的 Unity 对象不能作为有效候选提交。
            var clearing = ReferenceEquals(font, null);
            if (!clearing && (font == null || !IsAlive || target == null))
            {
                throw new InvalidOperationException("不能向已销毁的字体控件赋值，或使用已销毁的 Font。");
            }

            if (target == null)
            {
                return;
            }

            if (target.font == font)
            {
                fontResources.CommitSource(clearing);
                return;
            }

            fontResources.AssignNative(() => target.font = font,
                () =>
                {
                    if (!clearing || target == null || !ReferenceEquals(target.font, null))
                    {
                        return false;
                    }

                    target.canvasRenderer.Clear();
                    return true;
                }, () => SuspendResourceDisplay(target));
            fontResources.CommitSource(clearing);
            if (IsAlive)
            {
                fontResources.Notify(() => NotifyChanged(nameof(Font)));
            }
        }

        private void ReleaseFontOwner(ElementResourceOwner<Font> owner)
        {
            // 清理完成后解除独占；失败时保留持有者，防止新绑定覆盖清理证据。
            if (ReferenceEquals(fontResources, owner))
            {
                fontResources = null;
            }
        }
    }
}
