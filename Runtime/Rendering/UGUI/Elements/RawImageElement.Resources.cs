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
                    ConfigureResourceSource(ConfigureSynchronousTextureSource, ConfigureTextureSource);
                }

                if (textureResources == null)
                {
                    throw new InvalidOperationException("请先配置资源键加载器和生命周期。");
                }

                textureResources.SetSource(value);
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

        /// <summary>在绑定前配置纯同步资源键加载；绑定期间直接对象属性不能同时写入。</summary>
        public void ConfigureSynchronousTextureSource(Lifetime owner, ISynchronousResourceLoader loader)
        {
            CreateSynchronousTextureSlot(owner, loader);
            textureResources.ConfigureSource(owner, () => NotifyChanged(nameof(TextureSource)),
                CreateResourcePreparationTracker(textureResources, owner, nameof(TextureSource)));
        }

        /// <summary>在绑定前配置异步资源键加载；所属生命周期跟踪加载并等待释放。</summary>
        public void ConfigureTextureSource(Lifetime owner, IResourceLoader loader)
        {
            CreateTextureSlot(owner, loader);
            textureResources.ConfigureSource(owner, () => NotifyChanged(nameof(TextureSource)),
                CreateResourcePreparationTracker(textureResources, owner, nameof(TextureSource)));
        }

        /// <summary>创建并托管同步纹理槽；使用 Replace/Clear 更新，所属生命周期负责最终释放。</summary>
        private ResourceSlot<Texture> CreateSynchronousTextureSlot(
            Lifetime owner, ISynchronousResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateTextureResources(owner, true, () =>
                ResourceSlot<Texture>.CreateSynchronous(loader, AssignResourceTexture, owner.Token));
        }

        /// <summary>创建并托管异步纹理槽；替换及最终清理均由调用者等待，拒绝同步生命周期。</summary>
        private ResourceSlot<Texture> CreateTextureSlot(Lifetime owner, IResourceLoader loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            return CreateTextureResources(owner, false, () =>
                new ResourceSlot<Texture>(loader, AssignResourceTexture, owner.Token));
        }

        private ResourceSlot<Texture> CreateTextureResources(
                    Lifetime owner, bool synchronous, Func<ResourceSlot<Texture>> create)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireAlive();
            RequireUnmanagedTexture();
            if (!synchronous && owner.Mode == LifetimeMode.Synchronous)
            {
                throw new InvalidOperationException("同步生命周期不能创建异步纹理资源槽。");
            }

            return owner.Run(_ =>
            {
                Initialize();
                var registration = new ElementResourceOwner<Texture>(synchronous, ReleaseTextureOwner);
                // 先登记空持有者，创建失败也不会遗留无人等待的异步槽。
                if (synchronous)
                {
                    owner.OwnDisposable(registration);
                }
                else
                {
                    owner.Own(registration);
                }

                registration.Slot = create();
                textureResources = registration;
                return registration.Slot;
            });
        }

        private void RequireUnmanagedTexture()
        {
            if (textureResources != null)
            {
                throw new InvalidOperationException("纹理已由资源槽管理，请更新现有槽，或先释放其所属生命周期。");
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
                });
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
