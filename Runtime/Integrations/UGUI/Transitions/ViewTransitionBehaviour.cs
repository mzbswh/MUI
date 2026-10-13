using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>Prefab-local adapter for Timeline, tween libraries or project-specific animation.</summary>
    public abstract class ViewTransitionBehaviour : MonoBehaviour
    {
        public abstract ITransitionPlayback CreatePlayback(ViewTransitionContext context);
    }
}
