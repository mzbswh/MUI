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
                    ConfigureResourceSource(ConfigureSynchronousFontSource, ConfigureFontSource);
                }

                if (fontResources == null)
                {
                    throw new InvalidOperationException("请先配置资源键加载器和生命周期。");
                }

                fontResources.SetSource(value);
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

        /// <summary>在绑定前配置纯同步资源键加载；绑定期间直接对象属性不能同时写入。</summary>
        public void ConfigureSynchronousFontSource(Lifetime owner, ISynchronousResourceLoader loader)
        {
            CreateSynchronousFontSlot(owner, loader);
            fontResources.ConfigureSource(owner, () => NotifyChanged(nameof(FontSource)),
                CreateResourcePreparationTracker(fontResources, owner, nameof(FontSource)));
        }

        /// <summary>在绑定前配置异步资源键加载；所属生命周期跟踪加载并等待释放。</summary>
        public void ConfigureFontSource(Lifetime owner, IResourceLoader loader)
        {
            CreateFontSlot(owner, loader);
            fontResources.ConfigureSource(owner, () => NotifyChanged(nameof(FontSource)),
                CreateResourcePreparationTracker(fontResources, owner, nameof(FontSource)));
        }

        /// <summary>
        /// 创建同步字体资源槽，由指定生命周期托管。使用 Replace/Clear 更新字体，
        /// 所有者释放后才能创建下一槽或直接设置 Font。不会调用异步加载或清理。
        /// </summary>
        private ResourceSlot<Font> CreateSynchronousFontSlot(
            Lifetime owner, ISynchronousResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateFontResources(owner, true, () =>
                ResourceSlot<Font>.CreateSynchronous(loader, AssignResourceFont, owner.Token));
        }

        /// <summary>
        /// 创建异步字体资源槽，由指定生命周期等待加载结束并归还资源。
        /// 使用 ReplaceAsync/ClearAsync 更新字体；同步生命周期不能使用此入口。
        /// </summary>
        private ResourceSlot<Font> CreateFontSlot(Lifetime owner, IResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateFontResources(owner, false, () =>
                new ResourceSlot<Font>(loader, AssignResourceFont, owner.Token));
        }

        private ResourceSlot<Font> CreateFontResources(
                    Lifetime owner, bool synchronous, Func<ResourceSlot<Font>> create)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireAlive();
            RequireUnmanagedFont();
            if (!synchronous && owner.Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("同步生命周期不能创建异步字体资源槽。");
            }

            return owner.Run(_ =>
            {
                Initialize();
                var registration = new ElementResourceOwner<Font>(synchronous, ReleaseFontOwner);
                // 先登记空持有者，构造失败不会遗留无人等待的异步资源槽。
                if (synchronous)
                {
                    owner.OwnDisposable(registration);
                }
                else
                {
                    owner.Own(registration);
                }

                registration.Slot = create();
                fontResources = registration;
                return registration.Slot;
            });
        }

        private void RequireUnmanagedFont()
        {
            if (fontResources != null)
            {
                throw new InvalidOperationException("字体已由资源槽管理，请使用现有槽替换资源，或先释放其所属生命周期。");
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

            fontResources.AssignNative(() => target.font = font);
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
