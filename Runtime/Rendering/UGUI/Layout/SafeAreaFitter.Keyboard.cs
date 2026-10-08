using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class SafeAreaFitter
    {
        [SerializeField]
        private bool avoidKeyboard;
        private Rect? keyboardAreaOverride;

        /// <summary>是否从布局区域排除软键盘；默认关闭，避免改变已有页面布局。</summary>
        public bool AvoidKeyboard
        {
            get => avoidKeyboard;
            set
            {
                if (avoidKeyboard == value)
                {
                    return;
                }

                avoidKeyboard = value;
                dirty = true;
            }
        }

        /// <summary>
        /// 注入平台提供的键盘遮挡矩形，使用屏幕左下角为原点的像素坐标。
        /// 空矩形明确表示无键盘；覆盖值持续有效，旋转及窗口改变时由提供方更新。
        /// 此入口只更新布局数据，不打开、关闭或持有平台键盘。
        /// </summary>
        public void SetKeyboardAreaOverride(Rect screenPixelRect)
        {
            ValidateRect(screenPixelRect, nameof(screenPixelRect));
            if (keyboardAreaOverride.HasValue && keyboardAreaOverride.Value == screenPixelRect)
            {
                return;
            }

            keyboardAreaOverride = screenPixelRect;
            dirty = true;
        }

        /// <summary>恢复读取 Unity 键盘区域；平台未提供有效区域时不会推测键盘高度。</summary>
        public void ClearKeyboardAreaOverride()
        {
            keyboardAreaOverride = null;
            dirty = true;
        }

        private Rect ReadKeyboardArea()
        {
            if (!avoidKeyboard)
            {
                return default;
            }

            var area = keyboardAreaOverride ?? TouchScreenKeyboard.area;
            ValidateRect(area, nameof(area));
            return area;
        }

        /// <summary>
        /// 单个矩形容器无法表达带孔区域，选择键盘四周面积最大的无重叠矩形。
        /// 面积相同时优先上方，再下方、左侧、右侧；未启用的适配轴保持原尺寸。
        /// </summary>
        private Rect ExcludeKeyboard(Rect available, Rect keyboard)
        {
            if (keyboard.width <= 0 || keyboard.height <= 0 || !available.Overlaps(keyboard) ||
                (!fitHorizontal && !fitVertical))
            {
                return available;
            }

            var left = Mathf.Clamp(keyboard.xMin, available.xMin, available.xMax);
            var right = Mathf.Clamp(keyboard.xMax, available.xMin, available.xMax);
            var bottom = Mathf.Clamp(keyboard.yMin, available.yMin, available.yMax);
            var top = Mathf.Clamp(keyboard.yMax, available.yMin, available.yMax);
            var best = available;
            var bestArea = -1d;

            void Consider(Rect candidate)
            {
                var area = (double)candidate.width * candidate.height;
                if (area > bestArea)
                {
                    best = candidate;
                    bestArea = area;
                }
            }

            if (fitVertical)
            {
                Consider(Rect.MinMaxRect(available.xMin, top, available.xMax, available.yMax));
                Consider(Rect.MinMaxRect(available.xMin, available.yMin, available.xMax, bottom));
            }

            if (fitHorizontal)
            {
                Consider(Rect.MinMaxRect(available.xMin, available.yMin, left, available.yMax));
                Consider(Rect.MinMaxRect(right, available.yMin, available.xMax, available.yMax));
            }

            return best;
        }
    }
}
