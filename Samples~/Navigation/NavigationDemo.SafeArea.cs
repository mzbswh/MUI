using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private static void DemonstrateSafeArea(Canvas canvas)
        {
            Canvas.ForceUpdateCanvases();
            var viewport = canvas.pixelRect;
            var region = Node("SafeArea Demonstration", canvas.transform, Vector2.zero, Vector2.zero);
            var fitter = region.gameObject.AddComponent<SafeAreaFitter>();
            var notifications = 0;
            fitter.Applied += _ => notifications++;
            fitter.SetSafeAreaOverride(new Rect(viewport.x + viewport.width * 0.1f, viewport.y + viewport.height * 0.2f,
                viewport.width * 0.8f, viewport.height * 0.7f));
            fitter.Refresh();
            Debug.Log($"MUI SafeArea inset: min={region.anchorMin}; max={region.anchorMax}");
            fitter.Refresh();
            Debug.Log($"MUI SafeArea unchanged: notifications={notifications}");
            fitter.SetSafeAreaOverride(new Rect(viewport.x - 20, viewport.y - 20, viewport.width + 40, viewport.height + 40));
            fitter.Refresh();
            Debug.Log($"MUI SafeArea clipped: min={region.anchorMin}; max={region.anchorMax}");
            // 平台接入层以屏幕左下角为原点提供键盘像素区域；这里演示底部与浮动键盘。
            fitter.AvoidKeyboard = true;
            fitter.SetKeyboardAreaOverride(new Rect(viewport.xMin, viewport.yMin,
                viewport.width, viewport.height * 0.35f));
            fitter.Refresh();
            Debug.Log("MUI 底部键盘避让：" + fitter.AppliedScreenRect);
            fitter.SetKeyboardAreaOverride(new Rect(viewport.xMin + viewport.width * 0.25f,
                viewport.yMin + viewport.height * 0.2f, viewport.width * 0.5f, viewport.height * 0.3f));
            fitter.Refresh();
            Debug.Log("MUI 浮动键盘可用区域：" + fitter.AppliedScreenRect);
            fitter.SetKeyboardAreaOverride(default);
            fitter.Refresh();
            Debug.Log("MUI 键盘收起后恢复：" + fitter.AppliedScreenRect);
            fitter.ClearKeyboardAreaOverride();
            fitter.AvoidKeyboard = false;
            fitter.ClearSafeAreaOverride();
            fitter.Refresh();
            Debug.Log("MUI SafeArea device restored: " + fitter.AppliedScreenRect);
            Destroy(region.gameObject);
        }
    }
}
