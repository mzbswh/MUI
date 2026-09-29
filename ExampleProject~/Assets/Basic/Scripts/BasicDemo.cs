using System.Collections.Generic;
using MUI.Navigation;
using MUI.Resources;
using MUI.UGUI;
using UnityEngine;

namespace MUI.BasicExample
{
    public sealed class BasicDemo : MonoBehaviour
    {
        [SerializeField] private UIHost host = null;
        [SerializeField] private GameObject pagePrefab = null;
        private readonly Lifetime resultLifetime = new Lifetime(LifetimeMode.Synchronous);
        private Route<BasicPageViewModel, string, int> route;
        private bool initialized;

        private void Start()
        {
            if (host == null || pagePrefab == null)
            {
                Debug.LogError("Basic Demo needs a UIHost and BasicView prefab.", this);
                return;
            }

            var provider = new PrefabViewProvider(host.transform,
                new[] { new KeyValuePair<ViewResource, GameObject>(new ViewResource("BasicView"), pagePrefab) });
            try
            {
                host.InitializeSynchronous(provider, ownsProvider: true);
            }
            catch
            {
                provider.Dispose();
                throw;
            }

            initialized = true;
            route = BasicPageViewModelRoute.Create(
                () => new BasicPageViewModel(), _ => new BasicPagePresenter());
            OpenPage();
        }

        [ContextMenu("Open page")]
        public void OpenPage()
        {
            if (!initialized || host == null)
            {
                return;
            }

            var outcome = host.Navigator.Open(route, "MUI basic example");
            if (!outcome.IsSuccess)
            {
                Debug.LogError($"Open failed: {outcome.Status} ({outcome.Rejection})", this);
                return;
            }

            outcome.Handle.ObserveResult(resultLifetime, result =>
                Debug.Log($"Page result: {result.Status}, value={result.Value}, cleanup={result.Cleanup}", this));
        }

        private void OnDestroy()
        {
            resultLifetime.Dispose();
            if (initialized && host != null)
            {
                host.Shutdown();
            }
        }
    }
}
