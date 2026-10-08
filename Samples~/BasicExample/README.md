# Basic Example

In Unity 2022.3.62f3, add the MUI package from the local `package.json`, then import **Basic Example** from its Package Manager Samples list. Open the imported `Basic.unity` scene or use `MUI > Basic Example > Open Scene`.

Enter Play Mode and select **Done**. The page completes with result `42`; the scene button then shows `42: Reopen` and opens a fresh page. Run `Tools/MUI/Validate Build Catalogs` to check the imported Prefab and generated binding manifest before a Player build.

With Done selected, Enter submits `Done(42)` and Escape requests ordinary Back through `UIBackInput`. After the page exits, Enter on the selected Reopen button opens a new page. The Prefab declares the Cancel adapter; the provider configures its host before activation.

The sample keeps its Prefab in the imported Assets folder. It does not provide an asset loading backend or project persistence.

## What you write

| File | Responsibility |
| --- | --- |
| `Scripts/BasicPageViewModel.cs` | Declares the title binding, `Done(42)` command, View contract and typed Route. This page does not need a Presenter. |
| `Prefabs/BasicView.prefab` | Contains the View and named title/button Elements. |
| `Scripts/BasicExampleModule.cs` | Declares the generated registration module. |
| `Scripts/BasicDemo.cs` | Registers bindings, initializes UIHost with resident Prefabs and calls `OpenAsync`. |
| `Basic.unity` | Connects the host, Prefab and scene reopen button. |

The generator creates `BasicPageViewModelBindingFactory`, `BasicPageViewModelRoute`, the command property and `Generated.BasicBindings`; do not edit generated files or manually maintain their subscriptions. The `void` command uses the same asynchronous command contract as commands that await work. Resident Prefabs and immediate commands do not add a waiting frame.

The title binds through `ITextElement.Content`. Legacy `TextElement` and the optional `TMPTextElement` implement this same contract, so replacing the title backend does not require changing the ViewModel or generated binding.

The page result becomes available when closing is committed. The demo waits for visual exit before showing the scene reopen button. Before intentionally unloading this scene, await `BasicDemo.ShutdownAsync()` to finish host/resource cleanup. `OnDestroy` starts and observes fallback cleanup; it cannot delay scene destruction.
