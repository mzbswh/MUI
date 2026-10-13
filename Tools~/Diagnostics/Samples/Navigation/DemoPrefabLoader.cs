using System;
using System.Threading;
using System.Threading.Tasks;
using MUI.Resources;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    /// <summary>演示独立加载和释放持有权的场景 Prefab 适配器。</summary>
    public sealed class DemoPrefabLoader : IResourceLoader
    {
        private readonly GameObject prefab;

        public DemoPrefabLoader(GameObject prefab)
        {
            this.prefab = prefab;
        }

        public int Loads
        {
            get; private set;
        }

        public int Releases
        {
            get; private set;
        }

        public async ValueTask<IAcquiredResource<T>> LoadAsync<T>(string key, CancellationToken token) where T : class
        {
            if (typeof(T) != typeof(GameObject) || key != "NavigationView@1")
            {
                throw new ArgumentException("Unknown sample resource.");
            }

            ++Loads;
            await Task.Delay(50); // 演示取消后仍然成功返回的迟到结果。
            if (prefab == null)
            {
                throw new InvalidOperationException("Sample prefab was destroyed.");
            }

            return new AcquiredResource<T>((T)(object)prefab, _ => { ++Releases; return default; });
        }
    }
}
