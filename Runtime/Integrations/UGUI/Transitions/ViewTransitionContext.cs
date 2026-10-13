using UnityEngine;

namespace MUI.UGUI
{
    public sealed class ViewTransitionContext
    {
        internal ViewTransitionContext(View view, RectTransform target, bool entering,
            Vector3 position, Vector3 scale)
        {
            View = view;
            Target = target;
            IsEntering = entering;
            RestPosition = position;
            RestScale = scale;
        }

        public View View { get; }
        public RectTransform Target { get; }
        public bool IsEntering { get; }
        public Vector3 RestPosition { get; }
        public Vector3 RestScale { get; }
        public float Alpha => View.TransitionAlpha;

        /// <summary>Opt in only for properties owned by this animation; restored after hiding and before reuse.</summary>
        public void RegisterTargetRestore(bool position, bool scale)
            => View.RegisterTransitionTargetRestore(position, scale);

        /// <summary>Writes through View visibility gates; never write the root CanvasGroup directly.</summary>
        public void SetAlpha(float value) => View.SetTransitionAlpha(value);
    }
}
