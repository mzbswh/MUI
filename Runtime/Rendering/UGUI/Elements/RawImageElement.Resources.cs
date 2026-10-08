using System;
using MUI.Resources;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class RawImageElement
    {
        private ElementResourceOwner<Texture> textureResources;

        /// <summary>成功显示的资源键；赋值请求加载，空值清空，失败保留旧资源。</summary>
        public string TextureSource
        {
            get
            {
                RequireAlive();
                return textureResources == null ? null : textureResources.Source;
            }
            set
            {
                RequireAlive();
                if (textureResources == null)
                {
                    ConfigureResourceSource(ConfigureTextureSource);
                }

                if (textureResources == null)
                {
                    throw new InvalidOperationException("请先配置资源键加载器和生命周期。");
                }

                textureResources.SetSource(value);
            }
        }

        /// <summary>最新请求键、当前显示键及加载结果；重新设置请求键可显式重试。</summary>
        public ResourceSourceSnapshot TextureSourceSnapshot
        {
            get
            {
                RequireAlive();
                return textureResources == null ? default : textureResources.SourceSnapshot;
            }
        }

        protected override string GetBindingWriteTarget(string propertyName, BindingMode mode)
        {
            if (propertyName != nameof(TextureSource))
            {
                return base.GetBindingWriteTarget(propertyName, mode);
            }

            if (mode != BindingMode.OneWay && mode != BindingMode.OneTime)
            {
                throw new InvalidOperationException("资源键仅支持 OneWay 或 OneTime 绑定。");
            }

            return nameof(Texture);
        }

        /// <summary>在绑定前配置异步资源键加载；所属生命周期跟踪加载并等待释放。</summary>
        public void ConfigureTextureSource(LifetimeScope owner, IResourceLoader loader)
        {
            CreateTextureSlot(owner, loader);
            textureResources.ConfigureSource(owner, () => NotifyChanged(nameof(TextureSource)),
                () => NotifyChanged(nameof(TextureSourceSnapshot)),
                CreateResourcePreparationTracker(textureResources, owner, nameof(TextureSource)));
        }

        /// <summary>创建并托管异步纹理槽；所属生命周期等待替换和最终清理。</summary>
        private ResourceSlot<Texture> CreateTextureSlot(LifetimeScope owner, IResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateTextureResources(owner, () =>
                new ResourceSlot<Texture>(loader, AssignResourceTexture, owner.Token));
        }

        private ResourceSlot<Texture> CreateTextureResources(
                    LifetimeScope owner, Func<ResourceSlot<Texture>> create)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireAlive();
            RequireUnmanagedTexture();

            return owner.Run(_ =>
            {
                Initialize();
                var registration = new ElementResourceOwner<Texture>(ReleaseTextureOwner);
                // 先登记空持有者，创建失败也不会遗留无人等待的异步槽。
                owner.Own(registration);

                registration.Slot = create();
                textureResources = registration;
                return registration.Slot;
            });
        }

        private void RequireUnmanagedTexture()
        {
            if (textureResources != null)
            {
                throw new InvalidOperationException("纹理已有资源拥有者，不能再次配置。");
            }
        }

        private void AssignResourceTexture(Texture texture)
        {
            // 区分真正的清空和已销毁的 Unity 对象，避免提交无效候选。
            if (!ReferenceEquals(texture, null) && (texture == null || !IsAlive || target == null))
            {
                throw new InvalidOperationException("不能向已销毁的纹理控件赋值，或使用已销毁的 Texture。");
            }

            if (target == null)
            {
                return;
            }

            var clearing = ReferenceEquals(texture, null);
            if (target.texture == texture)
            {
                textureResources.CommitSource(clearing);
                return;
            }

            textureResources.AssignNative(() => target.texture = texture,
                () =>
                {
                    if (!clearing || target == null || !ReferenceEquals(target.texture, null))
                    {
                        return false;
                    }

                    target.canvasRenderer.Clear();
                    return true;
                }, () => SuspendResourceDisplay(target));
            textureResources.CommitSource(clearing);
            if (IsAlive)
            {
                textureResources.Notify(() => NotifyChanged(nameof(Texture)));
            }
        }

        private void ReleaseTextureOwner(ElementResourceOwner<Texture> owner)
        {
            if (ReferenceEquals(textureResources, owner))
            {
                textureResources = null;
            }
        }
    }
}
