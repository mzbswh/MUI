using System;
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
        [SerializeField] private UnityEngine.UI.Button reopenButton = null;
        private readonly Lifetime resultLifetime = new Lifetime(LifetimeMode.Synchronous);
        private Route<BasicPageViewModel, string, int> route;
        private bool initialized;

        private void Start()
        {
            if (host == null || pagePrefab == null || reopenButton == null)
            {
                Debug.LogError("Basic Demo needs a UIHost, BasicView prefab, and reopen button.", this);
                return;
            }

            reopenButton.gameObject.SetActive(false);
            var provider = new PrefabViewProvider(host.transform,
                new[] { new KeyValuePair<ViewResource, GameObject>(new ViewResource("BasicView"), pagePrefab) });
            try
            {
                host.InitializeSynchronous(provider, ownsProvider: true);
            }
            catch (Exception failure)
            {
                try
                {
                    provider.Dispose();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Basic Demo initialization and provider cleanup failed.", failure, cleanupFailure);
                }

                throw;
            }

            initialized = true;
            reopenButton.onClick.AddListener(OpenPage);
            try
            {
                route = BasicPageViewModelRoute.Create(
                    () => new BasicPageViewModel(), _ => new BasicPagePresenter());
                OpenPage();
            }
            catch (Exception failure)
            {
                try
                {
                    host.Shutdown();
                    initialized = false;
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Basic Demo startup and cleanup failed.", failure, cleanupFailure);
                }

                throw;
            }
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
                reopenButton.gameObject.SetActive(true);
                return;
            }

            reopenButton.gameObject.SetActive(false);
            outcome.Handle.ObserveResult(resultLifetime, result =>
            {
                Debug.Log($"Page result: {result.Status}, value={result.Value}, cleanup={result.Cleanup}", this);
                if (initialized && host != null && !host.Navigator.IsShutdown && reopenButton != null)
                {
                    reopenButton.gameObject.SetActive(true);
                    var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                    if (eventSystem != null && !eventSystem.alreadySelecting)
                    {
                        eventSystem.SetSelectedGameObject(reopenButton.gameObject);
                    }
                }
            });
        }

        private void OnDestroy()
        {
            try
            {
                if (initialized && host != null)
                {
                    host.Shutdown();
                }
            }
            finally
            {
                try
                {
                    if (reopenButton != null)
                    {
                        reopenButton.onClick.RemoveListener(OpenPage);
                    }
                }
                finally
                {
                    resultLifetime.Dispose();
                }
            }
        }
    }
}
