# Basic Example

In Unity 2022.3.62f3, add the MUI package from the local `package.json`, then import **Basic Example** from its Package Manager Samples list. Open the imported `Basic.unity` scene or use `Tools > MUI > 示例 (Samples) > 基础场景 (Basic Scene)`.

Enter Play Mode and select **Done**. The page completes with result `42`; the scene button then shows `42: Reopen` and opens a fresh page. Run `Tools/MUI/检查 (Inspect)/构建目录 (Build Catalogs)` to check the imported Prefab and generated binding manifest before a Player build.

With Done selected, Enter submits `Done(42)` and Escape requests ordinary Back through `UIBackInput`. After the page exits, Enter on the selected Reopen button opens a new page. The Prefab declares the Cancel adapter; the provider configures its host before activation.

The sample keeps its Prefab at `Resources/BasicView.prefab`. The default Unity Resources loader resolves the page resource key `BasicView`; no host catalog is required.

## Host configuration and debugging

`BasicMUISettings.asset` can serve as the unique project settings asset. UIHost discovers it automatically; if your project already has settings, merge the sample settings and keep only one asset. BasicDemo resolves the route with `host.ResolvePolicy()` and calls `host.Initialize(configureDefaultView: ConfigurePage)`; the host owns its default provider. The build catalog resolves the same settings asset. Projects with their own resource system can call `host.Initialize(resourceLoader: loader)` once; the host borrows the loader and owns the view provider. The project must keep the loader available until host shutdown completes.

The UIHost Inspector shows read-only global Settings, View Root, a short status and **Open MUI Dashboard**. Resource loading uses the page resource key directly. During Play Mode, open `Tools/MUI/控制台 (Dashboard)`, select **UI Host**, and capture page state, final policy, focus/history or lifecycle traces there. Change rules before creating a route; existing routes keep their resolved policy.

## What you write

| File | Responsibility |
| --- | --- |
| `Scripts/BasicPageViewModel.cs` | Declares the title binding, `Done(42)` command, View contract and typed Route. This page does not need a Presenter. |
| `Resources/BasicView.prefab` | Contains the View and named title/button Elements. |
| `Scripts/BasicExampleModule.cs` | Declares the generated registration module. |
| `Scripts/BasicDemo.cs` | Registers bindings, resolves the configured policy, initializes UIHost's default provider and calls `OpenAsync`. |
| `BasicMUISettings.asset` | Defines the sample's named layers, default rules and presets. |
| `Basic.unity` | Connects the configuration asset, view root and scene reopen button. |

The generator creates `BasicPageViewModelBindingFactory`, `BasicPageViewModelRoute`, the command property and `Generated.BasicBindings`; do not edit generated files or manually maintain their subscriptions. The `void` command uses the same asynchronous command contract as commands that await work. Resident Prefabs and immediate commands do not add a waiting frame.

The title binds through `ITextElement.Content`. Legacy `TextElement` and the optional `TMPTextElement` implement this same contract, so replacing the title backend does not require changing the ViewModel or generated binding.

The page result becomes available when closing is committed. The demo waits for visual exit before showing the scene reopen button. Before intentionally unloading this scene, await `BasicDemo.ShutdownAsync()` to finish host/resource cleanup. `OnDestroy` starts and observes fallback cleanup; it cannot delay scene destruction.
