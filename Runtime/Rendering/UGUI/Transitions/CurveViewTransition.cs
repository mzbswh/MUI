using System;
using UnityEngine;

namespace MUI.UGUI
{
    [CreateAssetMenu(menuName = "MUI/Curve View Transition")]
    public sealed class CurveViewTransition : ViewTransitionAsset
    {
        [SerializeField, Min(0)] private float duration = 0.2f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool animateAlpha = true;
        [SerializeField, Range(0, 1)] private float hiddenAlpha = 0;
        [SerializeField] private bool animateScale = false;
        [SerializeField] private Vector3 hiddenScale = new Vector3(0.9f, 0.9f, 1);
        [SerializeField] private bool animatePosition = false;
        [SerializeField] private Vector3 hiddenOffset = Vector3.zero;

        public override ITransitionPlayback CreatePlayback(ViewTransitionContext context)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0)
                throw new InvalidOperationException("Transition duration must be finite and nonnegative.");
            return new Playback(context, duration, curve == null ? null : new AnimationCurve(curve.keys),
                animateAlpha, hiddenAlpha, animateScale, hiddenScale, animatePosition, hiddenOffset);
        }

        internal sealed class Playback : TransitionPlayback
        {
            private readonly ViewTransitionContext context;
            private readonly float duration;
            private readonly AnimationCurve curve;
            private readonly bool alpha;
            private readonly bool scale;
            private readonly bool position;
            private readonly float fromAlpha, toAlpha;
            private readonly Vector3 fromScale, toScale, fromPosition, toPosition;
            private float elapsed;

            internal Playback(ViewTransitionContext context, float duration, AnimationCurve curve,
                bool alpha, float hiddenAlpha, bool scale, Vector3 hiddenScale, bool position, Vector3 offset)
            {
                this.context = context;
                this.duration = duration;
                this.curve = curve;
                this.alpha = alpha;
                this.scale = scale;
                this.position = position;
                context.RegisterTargetRestore(position, scale);
                var target = context.Target;
                if ((scale || position) && target == null)
                    throw new InvalidOperationException("Scale and position transitions require a RectTransform target.");
                fromAlpha = context.IsEntering ? hiddenAlpha : context.Alpha;
                toAlpha = context.IsEntering ? 1 : hiddenAlpha;
                fromScale = context.IsEntering ? Vector3.Scale(context.RestScale, hiddenScale) : (target == null ? context.RestScale : target.localScale);
                toScale = context.IsEntering ? context.RestScale : Vector3.Scale(context.RestScale, hiddenScale);
                fromPosition = context.IsEntering ? context.RestPosition + offset : (target == null ? context.RestPosition : target.anchoredPosition3D);
                toPosition = context.IsEntering ? context.RestPosition : context.RestPosition + offset;
            }

            protected override void OnPlay()
            {
                Sample(duration == 0 ? 1 : 0);
                if (duration == 0) Complete();
            }

            protected override void OnTick(float delta)
            {
                elapsed += delta;
                var progress = Mathf.Clamp01(elapsed / duration);
                Sample(progress);
                if (progress >= 1) Complete();
            }

            private void Sample(float time)
            {
                var value = curve == null ? time : curve.Evaluate(time);
                if (alpha) context.SetAlpha(Mathf.LerpUnclamped(fromAlpha, toAlpha, value));
                var target = context.Target;
                if (target != null)
                {
                    if (scale) target.localScale = Vector3.LerpUnclamped(fromScale, toScale, value);
                    if (position) target.anchoredPosition3D = Vector3.LerpUnclamped(fromPosition, toPosition, value);
                }
            }

            protected override void OnStop(TransitionEndReason reason)
            {
                if (reason == TransitionEndReason.Interrupted) return;
                if (context.IsEntering)
                {
                    if (alpha) context.SetAlpha(1);
                    var target = context.Target;
                    if (target != null)
                    {
                        if (scale) target.localScale = context.RestScale;
                        if (position) target.anchoredPosition3D = context.RestPosition;
                    }
                }
            }
        }
    }
}
