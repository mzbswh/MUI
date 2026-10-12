using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>Shared configuration only. Return a fresh, unstarted playback for every call.</summary>
    public abstract class ViewTransitionAsset : ScriptableObject
    {
        public abstract ITransitionPlayback CreatePlayback(ViewTransitionContext context);
    }
}
