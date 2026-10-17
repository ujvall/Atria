# Atria

Offline 3D indoor directions for **Pearl Research Park (PRP), VIT Vellore**. You pick a room
and one of three entrances. Atria then works out the shortest mapped route and shows a
first-person walkthrough that you scrub with a slider. It needs no GPS, no internet, no account
and no backend.

> **Data status: SAMPLE.** The building in this repository is an invented T-shaped corridor
> used to test the software. It is **not** the PRP layout. Room numbers `DEMO-xxx` and
> "Entrance A/B/C" are fictitious. The app shows a warning banner until the data file is
> marked `"verified"`. See [docs/STATUS.md](docs/STATUS.md) for the information still needed.

> **Unity status.** You have opened the project in Unity 6.6 and run the demo in Play mode. Claude's
> checks run in a cloud environment with no Unity Editor or Android SDK: the core tests and a
> compile check against older Unity reference assemblies. See [docs/STATUS.md](docs/STATUS.md)
> for what is still unverified.

## Getting started (Windows, Unity 6000.6.6f1)

1. **Android module.** In Unity Hub, open *Installs*, then the cog next to 6000.6.6f1, then
   *Add modules*. Tick **Android Build Support**, including **OpenJDK** and
   **Android SDK & NDK Tools**.
2. **Open the project.** In Unity Hub, choose *Add*, then *Add project from disk*, and select
   `C:\Users\ujval\Desktop\Projects\Atria` (the folder that contains `Assets/` and
   `Packages/`). Open it with 6000.6.6f1. On first open Unity creates `Library/`, fills in
   `ProjectSettings/` and generates `.meta` files. Commit the `.meta` files.
3. **Create the scene.** From the menu bar choose **Atria > 1. Create Main Scene**. This creates
   `Assets/Atria/Scenes/Main.unity` and adds it to Build Settings.
4. **Press Play.** Type `122`, tap **DEMO-122**, keep *Entrance A*, then tap
   **Start walkthrough** and drag the slider.

   **Which dataset?** The fictional demo loads by default, and the Console shows
   `override: no`. To load the provisional PRP draft instead:
   1. Stop Play mode.
   2. Choose **Atria > Dataset: Use PRP Draft (Block C ground floor)**. This assigns the draft to the
      `Atria` object and saves the scene, so the choice survives restarting Unity.
   3. Press Play. The Console should show `Dataset 'PRP Block C ground floor, left + right wings (PROVISIONAL DRAFT)' …
      source Assets/Atria/MappingData/prp_g_left_draft.json, override: yes`, and the app shows a
      **PROVISIONAL PRP DRAFT** banner.
   4. To switch back, choose **Atria > Dataset: Use Demo (default)**.
   If a selected file can't be read, the app shows a **DATA ERROR** banner rather than falling
   back to the demo.
5. **Configure Android.** Choose **File > Build Profiles**, switch the platform to Android, then
   run **Atria > 2. Configure Android Player Settings**.
6. **Build the APK.** Run **Atria > 3. Build Android APK**. The output goes to
   `Builds/Android/Atria.apk`.

### Tests

* **In Unity:** open *Window > General > Test Runner > EditMode > Run All*. The tests are in
  `Assets/Atria/Tests/EditMode`.
* **Without Unity:** run `dotnet test Tools/CoreTests`. This compiles the engine-independent
  core and the same test files with the .NET 8 SDK.

### Developer tools (optional, not part of the app)

Graphify (code knowledge graph) and Ponytail (a Claude Code plugin) are configured for
AI-assisted development. For setup, see [docs/DEV_TOOLS.md](docs/DEV_TOOLS.md).

## Layout

```
Assets/Atria/
  Scripts/Core/        Pure C# (Atria.Core.asmdef, no UnityEngine): data, walls, graph, router, camera path, search
  Scripts/Runtime/     Unity layer (Atria.App.asmdef): AtriaApp (flow), BuildingRenderer, RouteCameraController, RouteVisuals, UI/
  Editor/              Atria menu: create scene, Android settings, build APK, validate data
  Resources/Atria/     Packaged offline assets: Data/building.json, Shaders/
  Tests/EditMode/      NUnit tests for the core
Tools/CoreTests/       dotnet harness for running the core tests outside Unity
docs/                  ARCHITECTURE.md, DATA_FORMAT.md, STATUS.md, DEV_TOOLS.md
.claude/, CLAUDE.md    Claude Code config: Graphify skill + hook, Ponytail plugin (dev only)
```
