# Common Patterns

1. In Package Manager, import **Resource Integration**, then **Common Patterns**.
2. In a scene with an active Camera, add `CommonPatternsDemo` to an empty GameObject and enter Play mode. The camera prevents Unity's “No cameras rendering” message from covering the Overlay UI. The component creates its own Canvas and borrows an existing EventSystem, or creates one with `StandaloneInputModule`. Enable the legacy input backend or Both.
3. Press **Show toast**, then change language while the toast is visible. Its text and preferred height update. Press **Theme** to change colors and the project-owned icon sprite.
4. Press **Run task**. The task reports progress for about two seconds. Toast and task buttons become unavailable as soon as work starts, including before the delayed Loading panel appears. Language and theme remain usable while work runs; the Loading text follows language changes. At completion, input resumes and a localized Toast appears.

The demo uses `NotificationQueue`, `LoadingScope`, `InputGate`, `ThemeService`, `ThemeBindings` and the sample-local `LocalizationService` through their public APIs. It draws Toast and Loading from their state snapshots, so projects can replace this presentation without changing those services. The English/French directories and two theme catalogs are project data; there is no bundled translation asset or theme editor. Both icon sprites are created by this component and released when it is destroyed.

**Dialogs** is a separate sample for confirmation and alert flows; it has no import dependency on Common Patterns. This sample uses a demo coroutine instead of a resource backend. Replace it with project work and release the `LoadingOperation` when that work finishes. Package Manager import is verified in Unity 2022.3.62f3; this sample's pointer interaction, rendering and cleanup during scene exit still require runtime verification.
