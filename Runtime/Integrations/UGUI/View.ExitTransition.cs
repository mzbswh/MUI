using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        [SerializeField, Min(0)]
        private float exitFadeDuration;
        private float exitAlpha = 1;
        private bool exitVisible = true;

        /// <summary>退出淡出时长；零表示立即退出，应在准备阶段配置。</summary>
        public float ExitDuration
        {
            get
            {
                RequireAlive();
                return exitFadeDuration;
            }
            set
            {
                RequireAlive();
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                exitFadeDuration = value;
            }
        }

        public void SetExitVisible(bool visible)
        {
            RequireAlive();
            exitVisible = visible;
            ApplyGates();
        }

        public void SampleExit(float normalizedTime)
        {
            RequireAlive();
            if (!retainingVisuals)
            {
                throw new InvalidOperationException("Exit sampling requires retained visuals.");
            }

            if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedTime));
            }

            exitAlpha = 1 - Mathf.Clamp01(normalizedTime);
            ApplyGates();
        }

        public void FinishExit()
        {
            RequireAlive();
            exitAlpha = 1;
            exitVisible = true;
        }
    }
}
