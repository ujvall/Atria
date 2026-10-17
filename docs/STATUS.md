# Status

_Last updated: 2026-10-17 (second cloud session)_

**Environment:** all checks below ran in a Claude Code cloud Linux container, which has no
Unity Editor and no Android SDK. Unity 6000.6.6f1 was still downloading on the Windows laptop.
**Nothing has been opened or compiled in Unity yet, and no APK has been built.**

## Milestones
| # | Milestone | State |
|---|---|---|
| 1 | Project foundation: folders, packages, Android settings menu, app flow and search UI | Code done; awaiting first open in Unity |
| 2 | First 3D corridor: sample T layout, 3 entrances, 2 turns, 9 doors with signs | Code done; needs a visual check in Unity |
| 3 | Scrubber: distance-based first-person camera | Code done; path maths tested |
| 4 | Navigation: graph, Dijkstra, room and entrance to route | Done and tested (outside Unity) |
| 5 | Building data format and validator | Format done; **real PRP data needed** |
| 6 | Android APK | Menu ready; not built |
| 7 | Validation on a device and in airplane mode | Not started |

## Verified (cloud session)
| Check | Result |
|---|---|
| `dotnet test Tools/CoreTests` (.NET 8, same test files as Unity EditMode) | **66/66 passed** |
| Every `.json` and `.asmdef` parses as JSON (sample data, manifest, asmdefs, settings) | Pass |
| Sample building validator: no errors and no warnings; every entrance reaches every room; the camera stays inside walkable space on all 27 routes | Pass (covered by tests) |
| Runtime and editor C# against Unity reference DLLs (UnityEngine 2021.3, UnityEditor 2020.2, uGUI 2018.3), with three Unity-6-only APIs stubbed | **0 errors, 0 warnings** |
| Graphify and Ponytail installed and working in Claude Code | Pass, see [DEV_TOOLS.md](DEV_TOOLS.md) |

The three stubbed APIs are `Slider.SetValueWithoutNotify`, `ColorBlock.selectedColor` and
`UnityEditor.Build.NamedBuildTarget`. They are newer than the only reference assemblies
available here, and all three exist in Unity 6.

## Must be validated in the Unity 6000.6.6f1 Editor
1. **Packages.** `com.unity.ugui` 2.0.0 and `com.unity.test-framework` 1.4.5 need checking. If
   the Package Manager reports a different version locked to the editor, accept its version.
2. **Missing assembly reference.** `Atria.App.asmdef` references `Unity.InputSystem` by name.
   When the package is absent, Unity should ignore the missing reference, and the
   `ATRIA_INPUT_SYSTEM` define keeps the code compiling. Confirm that no error appears.
3. **Active Input Handling.** Check *Project Settings > Player*. Both "Input Manager (Old)" and
   "Both" work. "Input System Package (New)" also requires the `com.unity.inputsystem` package.
4. **Render pipeline.** The project has no SRP asset, so it uses the Built-in pipeline. Unity 6
   is deprecating Built-in. The custom shaders (`Atria/VertexLit`, `Atria/SignText`) are written
   to also work in URP, but this is not yet tested.
5. **Legacy APIs.** Check that `TextMesh`, `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`
   and the legacy `InputField` compile without obsolete-as-error failures.
6. **Visuals.**
   * Sign auto-fit (`Renderer.localBounds` on a TextMesh): check the size and readability.
   * Door and sign orientation, face winding (no inside-out walls), and the camera viewport between the UI bars.
7. **Game view layout.**
   * Use a portrait resolution such as 1080×2400 or 720×1280; the app ships portrait-only.
   * The search page should scroll, with the summary and Start pinned at the bottom.
   * In the **Scene** view, a Screen Space Overlay canvas shows as a large flat rectangle
     floating over the grid. That is how the editor draws it, not a runtime problem.
8. **Editor menu.** Check that **Atria > 1/2/3** run, including the PlayerSettings APIs:
   `minSdkVersion` API 26, IL2CPP, ARM64+ARMv7, and `forceInternetPermission = false`.
9. **Device checks.** Touch scrubbing, safe area, frame rate on a low-end phone, APK size and
   airplane mode.

## Changes in this session
* Runtime scripts now live in their own `Atria.App` assembly with an optional-Input-System
  define. Before, `ENABLE_INPUT_SYSTEM` alone could cause a compile error when the player
  setting was "New" but the package was missing.
* Removed `PlayerSettings.Android.forceSDCardPermission`, `gpuSkinning` and `MTRendering`. They
  are possibly obsolete in Unity 6, and the defaults are fine.
* Added Graphify and Ponytail as dev-only tooling: `.claude/`, `CLAUDE.md`, `docs/DEV_TOOLS.md`.
  `graphify-out/` is git-ignored.

## Mapping workflow (added 2026-10-17)
* The schema now supports rooms with several doors (routes go to the nearest door), doors with
  unknown positions, rooms with unknown locations, and the `hexRing` and `corridor` space
  shorthands. It also has a missing-measurement report and a rule that `"verified"` requires
  every item to be measured.
* See [MAPPING_GUIDE.md](MAPPING_GUIDE.md), and
  [mapping/prp-sketch-observations.md](mapping/prp-sketch-observations.md) for what the
  sketches show.
* **First provisional PRP section:** `Assets/Atria/MappingData/prp_g_left_draft.json` covers
  the hall, L1 and L2, with all geometry estimated. Select it with **Atria > Dataset: Use PRP
  Draft (left section)**, which sets **Building Data Override** and saves the scene. The demo
  stays the default.
* **Dataset loading (fixed 2026-10-17):**
  * The override field was read correctly but never assigned; it needed a manual drag in the
    Inspector, so Play mode kept loading the demo.
  * Selection now goes through `DatasetLoader` (tested) and the two menu commands.
  * The Console logs the name, path, status and whether an override was used.
  * Override failures show a **DATA ERROR** banner instead of falling back.
  * The demo is now labelled "Fictional demo corridor (SAMPLE, not PRP)".
* Details of what is estimated and what is unknown are in
  [mapping/prp-sketch-observations.md](mapping/prp-sketch-observations.md).

## Hex-ring centre fix (2026-10-17)
* **Before:** each ring's centre looked like a solid hexagonal pillar. The renderer drew every
  edge of the walkable outline as a 3 m capped wall, including the inner hexagon. The floor
  and routing were already correct: there was no floor or route across the centre.
* **Now:**
  * Hex rings record their centre as a floor void (configurable with `innerEdge`).
  * No wall is drawn along the inner edges. A 1 m rail was tried first; the user rejected it
    because it still looked like a pillar.
  * The centre is a dark, solid-filled area 0.15 m below the floor.
* **Still to check in Unity:** the visual result (recess depth and contrast, how the open
  shaft above the void looks under the ring ceiling).

## Known limitations
* Rooms are drawn as closed doors in the corridor wall; there are no room interiors yet.
* Single floor only in the sample. The format supports stair edges, but stairs are not drawn.
* Remaining distance is the route distance × (1 − progress). It is exact on straight
  corridors and approximate within rounded corners.

## Information needed from you to model the real PRP first floor
1. **Entrances:** which three entrances students use, their names, and photos from inside and
   outside.
2. **Reference frame:** one point to use as the origin, for example the centre of an entrance
   threshold, and which direction is +z.
3. **Corridors:** for each walked corridor, its length (paced or taped), its width, and where
   it meets others. A hand sketch with numbers is enough.
4. **Rooms:** room numbers exactly as printed, which corridor and side each door is on, and the
   distance from the nearest junction.
5. **Closed or restricted links:** passages that must not be routed.
6. **Floor numbering:** whether the floor you mapped is the ground floor or the one above, and
   where the stairs and lifts are.
