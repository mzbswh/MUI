using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MUI.Loading;
using MUI.Notifications;
using MUI.Samples.ResourceIntegration;
using MUI.Themes;
using MUI.UGUI;
using UnityEngine;

namespace MUI.Samples.CommonPatterns
{
    /// <summary>项目侧服务驱动的通知、加载、语言和主题组合示例。</summary>
    [DisallowMultipleComponent]
    public sealed partial class CommonPatternsDemo : MonoBehaviour
    {
        private static readonly ThemeToken<ThemeColor> BackgroundToken = new ThemeToken<ThemeColor>("background");
        private static readonly ThemeToken<ThemeColor> SurfaceToken = new ThemeToken<ThemeColor>("surface");
        private static readonly ThemeToken<ThemeColor> HeadingToken = new ThemeToken<ThemeColor>("heading");
        private static readonly ThemeToken<ThemeColor> DetailToken = new ThemeToken<ThemeColor>("detail");
        private static readonly ThemeToken<ThemeColor> ActionToken = new ThemeToken<ThemeColor>("action");
        private static readonly ThemeToken<ThemeColor> ActionTextToken = new ThemeToken<ThemeColor>("actionText");
        private static readonly ThemeToken<ThemeColor> ToastToken = new ThemeToken<ThemeColor>("toast");
        private static readonly ThemeToken<ThemeColor> LoadingToken = new ThemeToken<ThemeColor>("loading");
        private static readonly ThemeToken<string> IconToken = new ThemeToken<string>("icon");
        private readonly LifetimeScope lifetime = new LifetimeScope();
        private readonly List<UnityEngine.UI.Image> buttonImages = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.Text> buttonTexts = new List<UnityEngine.UI.Text>();
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private RectTransform panel;
        private RectTransform banners;
        private RectTransform toastPanel;
        private RectTransform loadingPanel;
        private UnityEngine.UI.Image backgroundGraphic;
        private UnityEngine.UI.Image panelGraphic;
        private UnityEngine.UI.Image toastGraphic;
        private UnityEngine.UI.Image loadingGraphic;
        private UnityEngine.UI.Image iconGraphic;
        private UnityEngine.UI.Text titleText;
        private UnityEngine.UI.Text detailText;
        private UnityEngine.UI.Text localeButtonText;
        private UnityEngine.UI.Text themeButtonText;
        private UnityEngine.UI.Text toastButtonText;
        private UnityEngine.UI.Text workButtonText;
        private UnityEngine.UI.Text inputText;
        private UnityEngine.UI.Text toastText;
        private UnityEngine.UI.Text loadingText;
        private UnityEngine.UI.Text progressText;
        private UnityEngine.UI.Button localeButton;
        private UnityEngine.UI.Button themeButton;
        private UnityEngine.UI.Button toastButton;
        private UnityEngine.UI.Button workButton;
        private Texture2D lightIconTexture;
        private Texture2D darkIconTexture;
        private Sprite lightIcon;
        private Sprite darkIcon;
        private NotificationQueue notifications;
        private LoadingScope loading;
        private InputGate inputGate;
        private ThemeService themes;
        private LocalizationService localization;
        private LocalizationCatalog english;
        private LocalizationCatalog french;
        private ThemeCatalog lightTheme;
        private ThemeCatalog darkTheme;
        private LoadingOperation activeLoad;
        private Coroutine loadRoutine;
        private string activeToastKey;
        private bool frenchSelected;
        private bool darkSelected;
        private bool stopping;

        private void Start()
        {
            try
            {
                BuildUI();
                english = CreateEnglishCatalog();
                french = CreateFrenchCatalog();
                lightTheme = CreateTheme(false);
                darkTheme = CreateTheme(true);
                notifications = lifetime.OwnDisposable(new NotificationQueue());
                loading = lifetime.OwnDisposable(new LoadingScope(0.15));
                inputGate = lifetime.OwnDisposable(new InputGate());
                themes = lifetime.OwnDisposable(new ThemeService(lightTheme));
                localization = lifetime.OwnDisposable(new LocalizationService(english));

                notifications.Changed += RenderToast;
                loading.Changed += RenderLoading;
                inputGate.Changed += RefreshInput;
                BindPresentation();

                localeButton.onClick.AddListener(SwitchLocale);
                themeButton.onClick.AddListener(SwitchTheme);
                toastButton.onClick.AddListener(ShowToast);
                workButton.onClick.AddListener(StartWork);
                RenderToast(notifications.Snapshot);
                RenderLoading(loading.Snapshot);
                RefreshInput();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Destroy(this);
            }
        }

        private void Update()
        {
            if (stopping || notifications == null || loading == null)
            {
                return;
            }

            notifications.Advance(Time.unscaledDeltaTime);
            loading.Advance(Time.unscaledDeltaTime);
        }

        private void BindPresentation()
        {
            BindText("title", titleText);
            BindText("detail", detailText);
            BindText("locale", localeButtonText);
            BindText("theme", themeButtonText);
            BindText("toast", toastButtonText);
            BindText("work", workButtonText);
            BindText("working", loadingText);

            ThemeBindings.BindColor(themes, lifetime, backgroundGraphic, BackgroundToken);
            ThemeBindings.BindColor(themes, lifetime, panelGraphic, SurfaceToken);
            ThemeBindings.BindColor(themes, lifetime, iconGraphic, HeadingToken);
            ThemeBindings.BindColor(themes, lifetime, titleText, HeadingToken);
            ThemeBindings.BindColor(themes, lifetime, detailText, DetailToken);
            ThemeBindings.BindColor(themes, lifetime, inputText, DetailToken);
            ThemeBindings.BindColor(themes, lifetime, toastGraphic, ToastToken);
            ThemeBindings.BindColor(themes, lifetime, loadingGraphic, LoadingToken);
            ThemeBindings.BindColor(themes, lifetime, toastText, HeadingToken);
            ThemeBindings.BindColor(themes, lifetime, loadingText, HeadingToken);
            ThemeBindings.BindColor(themes, lifetime, progressText, HeadingToken);
            foreach (var image in buttonImages)
            {
                ThemeBindings.BindColor(themes, lifetime, image, ActionToken);
            }

            foreach (var text in buttonTexts)
            {
                ThemeBindings.BindColor(themes, lifetime, text, ActionTextToken);
            }

            themes.Observe(lifetime, IconToken, key =>
            {
                if (iconGraphic == null)
                {
                    return;
                }

                iconGraphic.sprite = key == "light" ? lightIcon : darkIcon;
            });
        }

        private void BindText(string key, UnityEngine.UI.Text target)
        {
            localization.Observe(lifetime, new LocalizedMessage(key), value =>
            {
                if (target == null)
                {
                    return;
                }

                target.text = value.Text;
                if (target == detailText)
                {
                    target.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight =
                        Mathf.Clamp(target.preferredHeight + 12f, 64f, 100f);
                }

                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            });
        }

        private void SwitchLocale()
        {
            if (stopping)
            {
                return;
            }

            var visibleToast = notifications.Snapshot.IsVisible ? activeToastKey : null;
            frenchSelected = !frenchSelected;
            localization.SetCatalog(frenchSelected ? french : english);
            RefreshInput();
            if (visibleToast != null)
            {
                PostToast(visibleToast);
            }
        }

        private void SwitchTheme()
        {
            if (stopping)
            {
                return;
            }

            darkSelected = !darkSelected;
            themes.SetCatalog(darkSelected ? darkTheme : lightTheme);
        }

        private void ShowToast()
        {
            if (inputGate.IsOpen)
            {
                PostToast("toast.message");
            }
        }

        private void PostToast(string key)
        {
            notifications.Clear();
            activeToastKey = key;
            var message = localization.Format(new LocalizedMessage(key)).Text;
            notifications.Post(new Notification(message, displayDuration: 3, expiresAfter: 3));
        }

        private void StartWork()
        {
            if (stopping || activeLoad != null || !inputGate.IsOpen)
            {
                return;
            }

            activeLoad = loading.Begin(lifetime, "sample work", blockInput: inputGate);
            loadRoutine = StartCoroutine(RunWork());
        }

        private IEnumerator RunWork()
        {
            for (var step = 1; step <= 5; ++step)
            {
                yield return new WaitForSecondsRealtime(0.35f);
                if (stopping || activeLoad == null || !activeLoad.IsActive)
                {
                    yield break;
                }

                activeLoad.Report(step / 5d);
            }

            activeLoad.Dispose();
            activeLoad = null;
            loadRoutine = null;
            PostToast("work.done");
        }

        private void RefreshInput()
        {
            if (inputGate == null || workButton == null || toastButton == null)
            {
                return;
            }

            var enabled = inputGate.IsOpen;
            workButton.interactable = enabled;
            toastButton.interactable = enabled;
            if (localization != null && inputText != null)
            {
                inputText.text = localization.Format(new LocalizedMessage(enabled ? "input.ready" : "input.blocked")).Text;
            }
        }

        private void RenderToast(NotificationSnapshot snapshot)
        {
            if (toastPanel == null)
            {
                return;
            }

            toastPanel.gameObject.SetActive(snapshot.IsVisible);
            if (!snapshot.IsVisible)
            {
                activeToastKey = null;
            }
            else
            {
                toastText.text = snapshot.Current.Message;
                var layout = toastPanel.GetComponent<UnityEngine.UI.LayoutElement>();
                layout.preferredHeight = Mathf.Clamp(toastText.preferredHeight + 22f, 54f, 94f);
            }

            UpdateBannerLayout();
        }

        private void RenderLoading(LoadingSnapshot snapshot)
        {
            if (loadingPanel == null)
            {
                return;
            }

            loadingPanel.gameObject.SetActive(snapshot.IsVisible);
            progressText.text = snapshot.Progress.HasValue ? ((int)(snapshot.Progress.Value * 100)).ToString() + "%" : "";
            UpdateBannerLayout();
        }

        private void OnDestroy()
        {
            stopping = true;
            if (loadRoutine != null)
            {
                StopCoroutine(loadRoutine);
                loadRoutine = null;
            }

            if (notifications != null)
            {
                notifications.Changed -= RenderToast;
            }

            if (loading != null)
            {
                loading.Changed -= RenderLoading;
            }

            if (inputGate != null)
            {
                inputGate.Changed -= RefreshInput;
            }

            lifetime.Cancel();
            _ = CleanupAsync();
        }

        private async Task CleanupAsync()
        {
            try
            {
                await lifetime.DisposeAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                if (canvasObject != null)
                {
                    Destroy(canvasObject);
                }

                if (eventSystemObject != null)
                {
                    Destroy(eventSystemObject);
                }

                if (lightIcon != null)
                {
                    Destroy(lightIcon);
                }

                if (darkIcon != null)
                {
                    Destroy(darkIcon);
                }

                if (lightIconTexture != null)
                {
                    Destroy(lightIconTexture);
                }

                if (darkIconTexture != null)
                {
                    Destroy(darkIconTexture);
                }
            }
        }
    }
}
