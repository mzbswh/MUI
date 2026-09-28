using MUI.UGUI;
using UnityEditor;

namespace MUI.Editor
{
    internal static class ImmediateLayoutInitialization
    {
        [InitializeOnLoadMethod]
        private static void Initialize() => ImmediateLayout.InitializeMainThread();
    }
}
