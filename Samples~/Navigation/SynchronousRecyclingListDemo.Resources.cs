using System;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class SynchronousRecyclingListDemo
    {
        private readonly RewardResourceLoader resourceLoader = new RewardResourceLoader();
        private UnityEngine.UI.Text resourceStatus;
        private int shownCreated = -1;
        private int shownReleased = -1;
        private int shownItems = -1;
        private int shownCells = -1;
        private int shownFailures = -1;

        private void Update()
        {
            if (resourceStatus == null || list == null || rewards == null || lifetime.IsEnded)
            {
                return;
            }

            if (shownCreated == resourceLoader.Created && shownReleased == resourceLoader.Released &&
                shownItems == rewards.Count && shownCells == list.MaterializedCount &&
                shownFailures == resourceLoader.FontFailures)
            {
                return;
            }

            shownCreated = resourceLoader.Created;
            shownReleased = resourceLoader.Released;
            shownItems = rewards.Count;
            shownCells = list.MaterializedCount;
            shownFailures = resourceLoader.FontFailures;
            resourceStatus.text = $"Items: {shownItems}   Pool: {shownCells}   Live leases: {resourceLoader.LiveLeases}   Released: {shownReleased}   Font failures: {shownFailures}";
        }

        [ContextMenu("同步清空首项字体")]
        public void ClearFirstFont() => ChangeFirstFont(item => item.FontKey = null);

        [ContextMenu("同步恢复或重试首项字体")]
        public void RetryFirstFont()
        {
            ChangeFirstFont(item =>
            {
                if (item.FontKey == "DefaultFont")
                {
                    item.RefreshFont();
                }
                else
                {
                    item.FontKey = "DefaultFont";
                }
            });
        }

        [ContextMenu("模拟首项字体加载失败")]
        public void FailFirstFont()
        {
            resourceLoader.FailNextFont = true;
            try
            {
                RetryFirstFont();
            }
            finally
            {
                // 同步请求已返回；未发起加载时也不能污染之后新增条目的请求。
                resourceLoader.FailNextFont = false;
            }
        }

        private void ChangeFirstFont(Action<RewardItemViewModel> change)
        {
            if (lifetime.IsEnded || rewards == null || rewards.Count == 0)
            {
                return;
            }

            try
            {
                change((RewardItemViewModel)rewards[0]);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }

            LogResourceCounts();
        }

        [ContextMenu("输出奖励资源计数")]
        public void LogResourceCounts()
        {
            Debug.Log($"奖励资源凭证（图标和字体）：创建 {resourceLoader.Created}，归还 {resourceLoader.Released}，仍持有 {resourceLoader.LiveLeases}。", this);
        }

        /// <summary>只实现同步加载契约，资源直接生成，不需要图片文件、异步接口或后台任务。</summary>
        private sealed class RewardResourceLoader : ISynchronousResourceLoader
        {
            public int Created
            {
                get; private set;
            }

            public int Released
            {
                get; private set;
            }

            public int LiveLeases => Created - Released;

            public int FontFailures
            {
                get; private set;
            }

            public bool FailNextFont
            {
                get; set;
            }

            public ISynchronousResourceLease<T> Load<T>(string key) where T : class
            {
                if (typeof(T) == typeof(Font) && key == "DefaultFont")
                {
                    if (FailNextFont)
                    {
                        FailNextFont = false;
                        ++FontFailures;
                        throw new InvalidOperationException("示例主动模拟字体加载失败；已有字体应保持显示，可通过同键通知重试。");
                    }

                    var font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null)
                    {
                        throw new InvalidOperationException("未找到示例所需的内置字体。");
                    }

                    // Unity 拥有内置字体；凭证只统计借用，不销毁共享资源。
                    var fontLease = new SynchronousResourceLease<T>((T)(object)font, _ => ++Released);
                    ++Created;
                    return fontLease;
                }

                if (typeof(T) != typeof(Sprite) || (key != "Warm" && key != "Cool"))
                {
                    throw new NotSupportedException("奖励示例只提供 Warm/Cool Sprite 和 DefaultFont 字体。");
                }

                Texture2D texture = null;
                Sprite sprite = null;
                try
                {
                    texture = new Texture2D(2, 2) { name = "Reward " + key, filterMode = FilterMode.Point };
                    var tint = key == "Warm" ? new Color(1f, 0.65f, 0.2f) : new Color(0.2f, 0.7f, 1f);
                    texture.SetPixels(new[] { tint, Color.white, Color.white, tint });
                    texture.Apply();
                    sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                    var lease = new SynchronousResourceLease<T>((T)(object)sprite, _ =>
                    {
                        // 槽先解除 Image 的借用，再归还本凭证；Destroy 的物理销毁仍由 Unity 在帧末完成。
                        if (sprite != null)
                        {
                            Destroy(sprite);
                        }

                        if (texture != null)
                        {
                            Destroy(texture);
                        }

                        ++Released;
                    });
                    ++Created;
                    return lease;
                }
                catch
                {
                    if (sprite != null)
                    {
                        Destroy(sprite);
                    }

                    if (texture != null)
                    {
                        Destroy(texture);
                    }

                    throw;
                }
            }
        }
    }
}
