# Basic Example

In Unity 2022.3.62f3, add the MUI package from the local `package.json`, then import **Basic Example** from its Package Manager Samples list. Open the imported `Basic.unity` scene or use `MUI > Basic Example > Open Scene`.

Enter Play Mode and select **Done**. The page completes with result `42`; the scene button then shows `42: Reopen` and opens a fresh page. Run `Tools/MUI/Validate Build Catalogs` to check the imported Prefab and generated binding manifest before a Player build.

The sample keeps its Prefab in the imported Assets folder. It does not provide an asset loading backend or project persistence.
