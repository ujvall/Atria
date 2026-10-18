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
| `dotnet test Tools/CoreTests` (.NET 8, same test files as Unity EditMode) | **85/85 passed** |
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
  Draft (Block C ground floor)**, which sets **Building Data Override** and saves the scene. The demo
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

## Shorter hall corridors to L1 and R1 (2026-10-18)
* **Shorter stretch:** from your marked screenshot, the stretch from the hall to the junction
  below L1 was too long. It is now 6.57 m from `HALL_W`, down from 11.07 m. About 5 m of
  corridor now shows outside the hall.
* **How:** each wing slid 4.5 m along the straight lane towards the hall, the right wing as a
  mirror of the left. "Each wing" means everything beyond the hall's side walls: the junctions,
  L1/L2 and R1/R2, every room door, Entrance 1, G17, the lift and the lower-left block's end.
* **Unchanged:** the hall, the Main Entrance, G16 and the demo. Within each wing nothing moved
  relative to anything else, so no room, door, side or connection changed relative to its
  junction. The lane stays straight.
* **Checks:** 85/85 tests pass. Both junction outlines are still clean, and the plot was
  inspected.

## Straight lane at the L1 and R1 junctions (2026-10-18)
* **One lane:** from your corrected sketch, the hall corridor and the branch to the lower-left
  block are now one straight lane at about 252° yaw, from `HALL_W` to the unchanged block end.
  This replaces the 257° and 245° pair that met at a kink.
* **Junction and connector:** the junction `CORR_LB1` moved onto the lane, to (-19.553, -4.346).
  The L1 connector is now 4.29 m long instead of 3.0 m, measured from the ring corner. It still
  joins L1's lower-right corner at the same bearing.
* **Right wing:** the hall corridor, junction `RCORR_B1` and R1 connector mirror the left side.
  The hall corridor runs 1.3 m past the junction to cover the outside of the turn.
* **Not changed:** L1 and R1 were not rotated. Rotating them would move the G18, G21, G08, G10
  and G11 doors, and the Entrance 1 bend.
  * No change to rooms, doors, entrances, connectivity or the demo.
* **Checks:** the walls at both junctions form one outline with no gaps or notches. A test
  asserts the left lane is straight.

## Block C refinement: lower-left block as a destination (2026-10-18)
* **Branch is now a real destination:** a new unmapped landmark, `LOWER-LEFT-BLOCK`
  ("Lower-left block (not mapped yet)"), sits at the end of `CORR_LB1_BLOCK`. Its door position
  is not drawn. Search "block" finds it, and routes reach it from the Main Entrance and
  Entrance 1 via the bend before L1. The block's interior is not mapped.
* **Junction fix:** the branch now starts 0.5 m further back, inside the hall corridor. This
  removes a wall notch where it met the bend.
* **Photos:** they show open-air courtyards at the hex centres. These stay non-walkable and
  slightly recessed.
* **Symmetry:** the wings are approximately mirrored. You confirmed this is acceptable.
* **Unchanged:** the right wing, the hall, the entrances and every room position (checked by
  JSON comparison against the previous commit).

## Left wing: branch to the lower-left block, shorter run after L1 (2026-10-18)
* **New branch:** from your annotation, the hall corridor's bend before L1 is a junction. It
  also continues down-left, below L1, towards the separate lower-left block.
  * Added as corridor `CORR_LB1_BLOCK`, ending at `CORR_LBLOCK_END` after 8 m (placeholder).
  * The block itself is not mapped, and no rooms were invented.
* **Shorter run after L1:** the angled corridor from L1 to the Entrance 1 bend is now 3.0 m
  instead of the mirrored 6.9 m (no count given). L2, G17 and Entrance 1 move with the bend.
* **Unchanged:** the right wing, hall, G16, Main Entrance, and all rooms and their sides.
* **Not modelled yet:** the right wing has a matching down-right branch in the first sketch.

## Left wing mirrored from the right wing (2026-10-18)
* You confirmed the right wing's layout and asked for the left wing to be its mirror image. The
  left wing's geometry is now regenerated as the right wing reflected across the hall
  (x → −x):
  * L1 = mirrored R1 (octagonal centre)
  * L2 = mirrored R2 (sizes from the R2 foot-placement counts)
  * the dog-leg hall corridor, the angled approach, the straight run up to L2, and the corridor
    above L2
* **Kept from your left-wing observations:** every room and its junction side (G18, G21, G23,
  G25 and G26 ×2 each), G17 on the corridor up to L2, and Entrance 1 at the outer corner of the
  bend.
* The 15-step observation now ends at L1's upper-left corner (`L1_C300`).
* **Unchanged:** the right wing, hall, G16, Main Entrance and hall start.

## R2 proportions from foot-placement counts (2026-10-18)
* **Counts:** your heel-to-toe EU 44 Crocs counts are applied to R2 (upper-right junction) at
  about 0.30 m per placement. At that scale, the 8-placement width matches the existing 2.4 m
  corridors.
  * Junction side: 21 → 6.3 m
  * Ring corridor: kept at 8 placements
  * Corridor above R2: 33 × 8 → 9.9 m long
  * Angled approach from R1: 23 → 6.9 m
* **Recorded:** the raw counts and the counting method are kept in `footPlacementCounts`, and
  two `footPlacements` observations let the validator check them against the map.
* **Floor height:** set to 2.9 m (you confirmed 2.75–3 m).
* **Unchanged:** everything outside R2 and its corridors. All positions are still estimated.
* **Not received:** the sketch image. The junction was identified from your description
  (straight corridor above, angled approach and corridor below = R2).

## Main Entrance added to the PRP draft (2026-10-17)
* **Added:** start point `ENT_MAIN` "Main Entrance (position estimated)".
  * **Doorway:** a 2.4 m opening centred on the hall's courtyard/garden-facing (+z) side, at
    (0, 10.625), computed from the hall polygon.
  * **Spawn:** 0.8 m inside the threshold, at (0, 9.825). The camera starts facing −z, into
    the hall and away from the garden.
  * **Graph link:** one new edge to the hall centre.
* **Unchanged:** Entrance 1 is still the default start, and it and the hall start point are
  unchanged. No geometry, rooms or doors changed.
* **Ambiguity:** the screenshot showing the entrance did not arrive in that session. The side
  was inferred from "face inward, away from the garden". The width is unknown.

## Right wing added to the PRP draft (2026-10-17)
* **Added:**
  * the corridor from the hall's right side (a dog-leg, as drawn)
  * R1, the lower-right junction: octagonal centre, with G08 ×2, G07, G10 ×2 and G11
  * the corridor from R1 to R2
  * R2, the upper-right junction: hexagonal centre, with G03 ×2, G04 ×2, G05 ×2, the
    Electrical Room and G06 ×2
  * the corridor above R2, with the Lift as a landmark
* **Status:** all positions are estimated. No other floor or vertical route is modelled.
* **Unchanged:** the left wing.
* **Menu renamed:** **Atria > Dataset: Use PRP Draft (Block C ground floor)**. The file name
  and building id are unchanged.
* **Source:** the first sketch set's right-hand close-ups. The new annotated drawings
  mentioned in the request were not received in that session.
* **Steps to check in Unity:** see [MAPPING_GUIDE.md §6](MAPPING_GUIDE.md).

## PRP draft refined from the second drawings (2026-10-17)
* **Added:** Entrance 1 (position estimated), and an octagonal centre for L1.
* **Recorded:** the 15-step reference, as an uncalibrated survey observation (`observations`
  in the JSON; the validator compares it once a stride is set).
* **Unchanged:** all other geometry, rooms and connections.
* Details and unknowns are in [mapping/prp-sketch-observations.md](mapping/prp-sketch-observations.md).

## Hex-ring centre fix (2026-10-17)
* **Before:** each ring's centre looked like a solid hexagonal pillar. The renderer drew every
  edge of the walkable outline as a 3 m capped wall, including the inner hexagon. The floor
  and routing were already correct: there was no floor or route across the centre.
* **Now:**
  * Hex rings record their centre as a floor void (configurable with `innerEdge`).
  * No wall is drawn along the inner edges. A 1 m rail was tried first; the user rejected it
    because it still looked like a pillar.
  * The centre is a solid-filled area 0.15 m below the floor. It is a few shades darker than
    the walls; near-black was tried first and the user found it too harsh.
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
