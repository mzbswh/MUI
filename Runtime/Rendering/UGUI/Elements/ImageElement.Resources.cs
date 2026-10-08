using System;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class ImageElement
    {
        private ElementResourceOwner<Sprite> spriteResources;

        /// <summary>成功显示的资源键；赋值请求加载，空值清空，失败保留旧资源。</summary>
        public string SpriteSource
        {
            get
            {
                RequireAlive();
                return spriteResources == null ? null : spriteResources.Source;
            }
            set
            {
                RequireAlive();
                if (spriteResources == null)
                {
                    ConfigureResourceSource(ConfigureSpriteSource);
                }

                if (spriteResources == null)
                {
                    throw new InvalidOperationException("请先配置资源键加载器和生命周期。");
                }

                spriteResources.SetSource(value);
            }
        }

        /// <summary>最新请求键、当前显示键及加载结果；重新设置请求键可显式重试。</summary>
        public ResourceSourceSnapshot SpriteSourceSnapshot
        {
            get
            {
                RequireAlive();
                return spriteResources == null ? default : spriteResources.SourceSnapshot;
            }
        }

        protected override string GetBindingWriteTarget(string propertyName, BindingMode mode)
        {
            if (propertyName != nameof(SpriteSource))
            {
                return base.GetBindingWriteTarget(propertyName, mode);
            }

            if (mode != BindingMode.OneWay && mode != BindingMode.OneTime)
            {
                throw new InvalidOperationException("资源键仅支持 OneWay 或 OneTime 绑定。");
            }

            return nameof(Sprite);
        }

        /// <summary>在绑定前配置异步资源键加载；所属生命周期跟踪加载并等待释放。</summary>
        public void ConfigureSpriteSource(LifetimeScope owner, IResourceLoader loader)
        {
            CreateSpriteSlot(owner, loader);
            spriteResources.ConfigureSource(owner, () => NotifyChanged(nameof(SpriteSource)),
                () => NotifyChanged(nameof(SpriteSourceSnapshot)),
                CreateResourcePreparationTracker(spriteResources, owner, nameof(SpriteSource)));
        }

        /// <summary>
        /// 创建异步图标资源槽，由指定生命周期等待加载结束并归还资源。
        /// 使用 ReplaceAsync/ClearAsync 更新图标；所属生命周期等待在途加载与实际归还。
        /// </summary>
        private ResourceSlot<Sprite> CreateSpriteSlot(LifetimeScope owner, IResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateSpriteResources(owner, () =>
                new ResourceSlot<Sprite>(loader, AssignResourceSprite, owner.Token));
        }

        private ResourceSlot<Sprite> CreateSpriteResources(
                    LifetimeScope owner, Func<ResourceSlot<Sprite>> create)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireAlive();
            RequireUnmanagedSprite();

            return owner.Run(_ =>
            {
                Initialize();
                var registration = new ElementResourceOwner<Sprite>(ReleaseSpriteOwner);
                // 先登记空持有者，构造失败不会遗留无人等待的异步资源槽。
                owner.Own(registration);

                registration.Slot = create();
                spriteResources = registration;
                return registration.Slot;
            });
        }

        private void RequireUnmanagedSprite()
        {
            if (spriteResources != null)
            {
                throw new InvalidOperationException("图标已有资源拥有者，不能再次配置。");
            }
        }

        private void AssignResourceSprite(Sprite sprite)
        {
            // 真正的 null 表示清空；已销毁的 Unity 对象不能作为有效候选提交。
            var clearing = ReferenceEquals(sprite, null);
            if (!clearing && (sprite == null || !IsAlive || target == null))
            {
                throw new InvalidOperationException("不能向已销毁的图标控件赋值，或使用已销毁的 Sprite。");
            }

            if (target == null)
            {
                return;
            }

            if (target.sprite == sprite)
            {
                spriteResources.CommitSource(clearing);
                return;
            }

            spriteResources.AssignNative(() => target.sprite = sprite,
                () =>
                {
                    if (!clearing || target == null || !ReferenceEquals(target.sprite, null) ||
                        !ReferenceEquals(target.overrideSprite, null))
                    {
                        return false;
                    }

                    target.canvasRenderer.Clear();
                    return true;
                }, () => SuspendResourceDisplay(target));
            spriteResources.CommitSource(clearing);
            if (IsAlive)
            {
                spriteResources.Notify(() => NotifyChanged(nameof(Sprite)));
            }
        }

        private void ReleaseSpriteOwner(ElementResourceOwner<Sprite> owner)
        {
            // 清理完成后解除独占；失败时保留持有者，防止新绑定覆盖清理证据。
            if (ReferenceEquals(spriteResources, owner))
            {
                spriteResources = null;
            }
        }
    }
}
