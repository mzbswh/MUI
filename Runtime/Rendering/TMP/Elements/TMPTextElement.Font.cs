using System;
using TMPro;

namespace MUI.TMP
{
    public sealed partial class TMPTextElement
    {
        /// <summary>
        /// 借用 TMP 字体及其图集依赖，不转移资源持有权。
        /// 原生换字体可能替换共享材质，材质资源槽尚未释放时禁止写入。
        /// null 沿用 TMP 默认字体回退，不能用于保证解除字体引用。
        /// </summary>
        public TMP_FontAsset FontAsset
        {
            get => Target.font;
            set
            {
                if (!ReferenceEquals(value, null) && value == null)
                {
                    throw new ArgumentException("不能使用已销毁的 TMP 字体。", nameof(value));
                }

                RequireUnmanagedMaterial();
                var text = Target;
                if (text.font == value)
                {
                    return;
                }

                if (value != null)
                {
                    // TMP 先写入字体再读取首张图集；先检查可直接确认的依赖，避免原生访问空图集。
                    var atlases = value.atlasTextures;
                    if (atlases == null || atlases.Length == 0 || atlases[0] == null)
                    {
                        throw new ArgumentException("TMP 字体必须包含有效的首张图集。", nameof(value));
                    }

                    if (value.material == null)
                    {
                        throw new ArgumentException("TMP 字体必须包含有效的默认材质。", nameof(value));
                    }
                }

                text.font = value;
                // 一次发布字体及其原生关联属性变化，兼容字体切换导致材质回退。
                NotifyChanged(string.Empty);
            }
        }

        protected override string GetBindingWriteTarget(string propertyName, BindingMode mode)
        {
            // 字体写入可能替换材质，与直接材质和材质键绑定不能形成多个写入者。
            return propertyName == nameof(FontAsset)
                ? nameof(Material)
                : base.GetBindingWriteTarget(propertyName, mode);
        }
    }
}
