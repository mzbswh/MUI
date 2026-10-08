using MUI.UGUI;
using UnityEditor;

namespace MUI.Editor
{
    internal static class ImmediateLayoutInitialization
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            UnityMainThread.Initialize();
            ImmediateLayout.Reset();
        }
    }
}
