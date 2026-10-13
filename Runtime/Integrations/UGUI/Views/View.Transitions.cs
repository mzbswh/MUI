using MUI.Unity;
using System;
using UnityEngine;

namespace MUI.UGUI
{
    public enum ViewTransitionMode { Inherit, Disabled, Override }

    public sealed partial class View
    {
        [SerializeField] private ViewTransitionMode transitionMode;
        [SerializeField] private RectTransform transitionTarget;
        [SerializeField] private ViewTransitionAsset enterTransition;
        [SerializeField] private ViewTransitionAsset exitTransition;
        [SerializeField] private ViewTransitionBehaviour enterTransitionBehaviour;
        [SerializeField] private ViewTransitionBehaviour exitTransitionBehaviour;
        private UGUIHostSettings transitionSettings;
        private RectTransform capturedTransitionTarget;
        private Vector3 transitionRestPosition;
        private Vector3 transitionRestScale;
        private bool transitionPoseCaptured;
        private bool restoreTransitionPosition;
        private bool restoreTransitionScale;

        public ViewTransitionMode TransitionMode { get => transitionMode; set => transitionMode = value; }
        public ViewTransitionAsset EnterTransition { get => enterTransition; set => enterTransition = value; }
        public ViewTransitionAsset ExitTransition { get => exitTransition; set => exitTransition = value; }
        public ViewTransitionBehaviour EnterTransitionBehaviour { get => enterTransitionBehaviour; set => enterTransitionBehaviour = value; }
        public ViewTransitionBehaviour ExitTransitionBehaviour { get => exitTransitionBehaviour; set => exitTransitionBehaviour = value; }
        public RectTransform TransitionTarget { get => transitionTarget; set => transitionTarget = value; }
        public Func<ViewTransitionContext, ITransitionPlayback> EnterTransitionFactory { get; set; }
        public Func<ViewTransitionContext, ITransitionPlayback> ExitTransitionFactory { get; set; }
        public bool TransitionsEnabled => transitionMode != ViewTransitionMode.Disabled &&
            (transitionSettings == null || transitionSettings.AnimationsEnabled);

        internal float TransitionAlpha => retainingVisuals ? retainedAlpha * exitAlpha : enterAlpha;

        internal void SetTransitionAlpha(float value)
        {
            RequireAlive();
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (retainingVisuals)
            {
                retainedAlpha = Mathf.Clamp01(value);
                exitAlpha = 1;
            }
            else enterAlpha = Mathf.Clamp01(value);
            ApplyGates();
        }

        public ITransitionPlayback CreateEnterPlayback()
        {
            RequireAlive();
            transitionSettings = MUISettings.Global as UGUIHostSettings;
            CaptureTransitionPose();
            return CreateTransitionPlayback(true);
        }

        public ITransitionPlayback CreateExitPlayback()
        {
            RequireAlive();
            return CreateTransitionPlayback(false);
        }

        private ITransitionPlayback CreateTransitionPlayback(bool entering)
        {
            if (!TransitionsEnabled) return null;
            var context = new ViewTransitionContext(this, capturedTransitionTarget, entering,
                transitionRestPosition, transitionRestScale);
            var factory = entering ? EnterTransitionFactory : ExitTransitionFactory;
            if (factory != null) return factory(context);
            ViewTransitionAsset asset;
            if (transitionMode == ViewTransitionMode.Override)
            {
                var behaviour = entering ? enterTransitionBehaviour : exitTransitionBehaviour;
                if (behaviour != null) return behaviour.CreatePlayback(context);
                asset = entering ? enterTransition : exitTransition;
            }
            else asset = transitionSettings == null ? null :
                (entering ? transitionSettings.DefaultEnterTransition : transitionSettings.DefaultExitTransition);
            if (asset != null) return asset.CreatePlayback(context);
            var duration = entering ? enterFadeDuration : exitFadeDuration;
            if (duration == 0) return null;
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0)
                throw new InvalidOperationException("Transition duration must be finite and nonnegative.");
            return new CurveViewTransition.Playback(context, duration, null, true, 0,
                false, Vector3.one, false, Vector3.zero);
        }

        private void CaptureTransitionPose()
        {
            if (transitionPoseCaptured) return;
            capturedTransitionTarget = transitionTarget != null ? transitionTarget : transform as RectTransform;
            if (capturedTransitionTarget != null)
            {
                transitionRestPosition = capturedTransitionTarget.anchoredPosition3D;
                transitionRestScale = capturedTransitionTarget.localScale;
            }
            transitionPoseCaptured = true;
        }

        internal void RegisterTransitionTargetRestore(bool position, bool scale)
        {
            restoreTransitionPosition |= position;
            restoreTransitionScale |= scale;
        }

        private void RestoreTransitionPose()
        {
            if (transitionPoseCaptured && capturedTransitionTarget != null)
            {
                if (restoreTransitionPosition) capturedTransitionTarget.anchoredPosition3D = transitionRestPosition;
                if (restoreTransitionScale) capturedTransitionTarget.localScale = transitionRestScale;
            }
            transitionPoseCaptured = false;
            restoreTransitionPosition = false;
            restoreTransitionScale = false;
            capturedTransitionTarget = null;
        }
    }
}
