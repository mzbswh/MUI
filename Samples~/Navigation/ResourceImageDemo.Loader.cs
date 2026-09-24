using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class ResourceImageDemo
    {
        /// <summary>仅用于示例的预期加载失败，便于与其他 UI 的诊断区分。</summary>
        private sealed class DemoLoadException : Exception
        {
            internal DemoLoadException(DemoLoader owner, string key) : base("示例主动拒绝加载资源：" + key)
            {
                Owner = owner;
                Key = key;
            }

            internal string Key
            {
                get;
            }

            internal DemoLoader Owner
            {
                get;
            }
        }

        /// <summary>只拥有本示例新建的原生资源；统计凭证归还，不将帧末 Destroy 当成即时卸载。</summary>
        private sealed class DemoLoader : ISynchronousResourceLoader, IResourceLoader
        {
            private readonly int delay;
            private readonly bool ignoreCancellation;

            internal DemoLoader(int delay, bool ignoreCancellation)
            {
                this.delay = delay;
                this.ignoreCancellation = ignoreCancellation;
            }

            public int Pending
            {
                get; private set;
            }

            public int Created
            {
                get; private set;
            }

            public int Released
            {
                get; private set;
            }

            public int LiveLeases => Created - Released;

            internal int FailuresRemaining
            {
                get; set;
            }

            public ISynchronousResourceLease<T> Load<T>(string key) where T : class
            {
                if (ConsumeFailure())
                {
                    throw new DemoLoadException(this, key);
                }

                return Create<T>(key);
            }

            public async ValueTask<IResourceLease<T>> LoadAsync<T>(string key, CancellationToken token) where T : class
            {
                // 在请求开始时确定故障归属，不让已经在途的旧请求抢走新请求的演示故障。
                var fail = ConsumeFailure();
                ++Pending;
                try
                {
                    // 故意忽略取消时仍交出迟到凭证，由框架负责归还。
                    await Task.Delay(delay, ignoreCancellation ? CancellationToken.None : token);
                    if (fail)
                    {
                        throw new DemoLoadException(this, key);
                    }

                    return Create<T>(key);
                }
                finally
                {
                    --Pending;
                }
            }

            private bool ConsumeFailure()
            {
                if (FailuresRemaining <= 0)
                {
                    return false;
                }

                --FailuresRemaining;
                return true;
            }

            private SynchronousResourceLease<T> Create<T>(string key) where T : class
            {
                if (key != "Warm" && key != "Cool")
                {
                    throw new ArgumentException("示例只提供 Warm 和 Cool 两个资源键。", nameof(key));
                }

                if (typeof(T) == typeof(Font))
                {
                    // 两个键借用同一内置字体，用于观察代际和凭证归还，不伪造字体外观差异。
                    var font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null)
                    {
                        throw new InvalidOperationException("未找到示例所需的内置字体。");
                    }

                    var fontLease = new SynchronousResourceLease<T>((T)(object)font, _ => ++Released);
                    ++Created;
                    return fontLease;
                }

                if (typeof(T) != typeof(Sprite) && typeof(T) != typeof(Texture))
                {
                    throw new NotSupportedException("示例加载器只提供 Sprite、Texture 和 Font。");
                }

                Texture2D texture = null;
                Sprite sprite = null;
                try
                {
                    texture = new Texture2D(2, 2) { name = "MUI Demo " + key, filterMode = FilterMode.Point };
                    var color = key == "Warm" ? new Color(1f, 0.6f, 0.15f) : new Color(0.15f, 0.65f, 1f);
                    texture.SetPixels(new[] { color, Color.white, Color.white, color });
                    texture.Apply();
                    if (typeof(T) == typeof(Sprite))
                    {
                        sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                    }

                    var asset = typeof(T) == typeof(Sprite) ? (T)(object)sprite : (T)(object)texture;
                    var lease = new SynchronousResourceLease<T>(asset, _ =>
                    {
                        // 槽已解除原生借用后才能到达这里；不触及项目的其他资源。
                        if (sprite != null)
                        {
                            UnityEngine.Object.Destroy(sprite);
                        }
                        if (texture != null)
                        {
                            UnityEngine.Object.Destroy(texture);
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
                        UnityEngine.Object.Destroy(sprite);
                    }
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                    throw;
                }
            }
        }
    }
}
