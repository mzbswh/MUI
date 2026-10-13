using System;
using UnityEngine;

namespace MUI.UGUI
{
    public sealed partial class View
    {
        [SerializeField, Min(0)]
        private float enterFadeDuration;
        private float enterAlpha = 1;

        /// <summary>零表示禁用效果；应在准备阶段、导航提交页面前设置。</summary>
        public float EnterDuration
        {
            get
            {
                RequireAlive();
                return enterFadeDuration;
            }

            set
            {
                RequireAlive();
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                enterFadeDuration = value;
            }
        }

        public void SampleEnter(float normalizedTime)
        {
            RequireAlive();
            if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedTime));
            }

            enterAlpha = Mathf.Clamp01(normalizedTime);
            if (group != null)
            {
                group.alpha = retainingVisuals ? (exitVisible ? retainedAlpha * exitAlpha : 0) : (hostVisible && localVisible ? enterAlpha : 0);
            }
        }

        public void FinishEnter()
        {
            RequireAlive();
            enterAlpha = 1;
            if (group != null)
            {
                group.alpha = retainingVisuals ? (exitVisible ? retainedAlpha * exitAlpha : 0) : (hostVisible && localVisible ? 1 : 0);
            }
        }
    }
}
