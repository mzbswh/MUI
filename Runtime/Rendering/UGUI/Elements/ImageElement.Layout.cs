using System;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public sealed partial class ImageElement
    {
        /// <summary>图片绘制类型。</summary>
        public Image.Type ImageType
        {
            get => Target.type;
            set
            {
                if (!Enum.IsDefined(typeof(Image.Type), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var image = Target;
                if (image.type == value)
                {
                    return;
                }

                image.type = value;
                NotifyChanged();
            }
        }

        /// <summary>是否保持图片原始宽高比。</summary>
        public bool PreserveAspect
        {
            get => Target.preserveAspect;
            set
            {
                var image = Target;
                if (image.preserveAspect == value)
                {
                    return;
                }

                image.preserveAspect = value;
                NotifyChanged();
            }
        }

        /// <summary>九宫格或平铺模式是否绘制中心。</summary>
        public bool FillCenter
        {
            get => Target.fillCenter;
            set
            {
                var image = Target;
                if (image.fillCenter == value)
                {
                    return;
                }

                image.fillCenter = value;
                NotifyChanged();
            }
        }

        /// <summary>填充方式；原生控件会同时将 FillOrigin 重置为零。</summary>
        public Image.FillMethod FillMethod
        {
            get => Target.fillMethod;
            set
            {
                if (!Enum.IsDefined(typeof(Image.FillMethod), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var image = Target;
                if (image.fillMethod == value)
                {
                    return;
                }

                image.fillMethod = value;
                NotifyChanged(string.Empty);
            }
        }

        /// <summary>填充起点，取值 0–3；具体有效起点由 FillMethod 决定。</summary>
        public int FillOrigin
        {
            get => Target.fillOrigin;
            set
            {
                if (value < 0 || value > 3)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var image = Target;
                if (image.fillOrigin == value)
                {
                    return;
                }

                image.fillOrigin = value;
                NotifyChanged();
            }
        }

        /// <summary>径向填充是否顺时针。</summary>
        public bool FillClockwise
        {
            get => Target.fillClockwise;
            set
            {
                var image = Target;
                if (image.fillClockwise == value)
                {
                    return;
                }

                image.fillClockwise = value;
                NotifyChanged();
            }
        }
    }
}
