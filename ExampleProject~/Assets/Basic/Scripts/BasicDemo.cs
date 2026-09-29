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
        [SerializeField] private UnityEngine.UI.Text reopenLabel = null;
        private readonly Lifetime resultLifetime = new Lifetime(LifetimeMode.Synchronous);
        private Route<BasicPageViewModel, string, int> route;
        private bool initialized;

        private void Start()
        {
            if (host == null || pagePrefab == null || reopenButton == null || reopenLabel == null)
            {
                Debug.LogError("Basic Demo needs a UIHost, BasicView prefab, reopen button, and label.", this);
                return;
            }

            reopenButton.gameObject.SetActive(false);
            route = BasicPageViewModelRoute.Create(
                () => new BasicPageViewModel(), _ => new BasicPagePresenter());
            var provider = new PrefabViewProvider(host.transform,
                new[] { new KeyValuePair<ViewResource, GameObject>(route.Resource, pagePrefab) });
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
            try
            {
                reopenButton.onClick.AddListener(OpenPage);
                OpenPage();
            }
            catch (Exception failure)
            {
                initialized = false;
                var cleanupErrors = new List<Exception>();
                try
                {
                    if (reopenButton != null)
                    {
                        reopenButton.onClick.RemoveListener(OpenPage);
                    }
                }
                catch (Exception cleanupFailure)
                {
                    cleanupErrors.Add(cleanupFailure);
                }

                try
                {
                    host.Shutdown();
                }
                catch (Exception cleanupFailure)
                {
                    cleanupErrors.Add(cleanupFailure);
                }

                if (cleanupErrors.Count != 0)
                {
                    cleanupErrors.Insert(0, failure);
                    throw new AggregateException("Basic Demo startup and cleanup failed.", cleanupErrors);
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
                reopenLabel.text = "Reopen";
                reopenButton.gameObject.SetActive(true);
                return;
            }

            reopenButton.gameObject.SetActive(false);
            outcome.Handle.ObserveResult(resultLifetime, result =>
            {
                Debug.Log($"Page result: {result.Status}, value={result.Value}, cleanup={result.Cleanup}", this);
                if (initialized && host != null && !host.Navigator.IsShutdown && reopenButton != null && reopenLabel != null)
                {
                    reopenLabel.text = result.IsCompleted ? $"{result.Value}: Reopen" : "Reopen";
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
            var wasInitialized = initialized;
            initialized = false;
            var errors = new List<Exception>();
            try
            {
                if (wasInitialized && host != null)
                {
                    host.Shutdown();
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                if (reopenButton != null)
                {
                    reopenButton.onClick.RemoveListener(OpenPage);
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            try
            {
                resultLifetime.Dispose();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("Basic Demo cleanup failed.", errors);
            }
        }
    }
}
