# Mapping PRP: from a hand sketch to validated data

This takes a corridor sketch, like those in `docs/mapping/`, to a building file that the
validator accepts and the app can route on. Work **one section at a time**, for example one
hex junction and its spur, and validate after each.

The schema is in [DATA_FORMAT.md](DATA_FORMAT.md). What the current sketches already show is
in [mapping/prp-sketch-observations.md](mapping/prp-sketch-observations.md).

## 0. Ground rules
* Record only what you have seen or measured. Unknown stays unknown: see "Recording what you
  don't know" in DATA_FORMAT.md.
* Mark every item `placeholder`, `estimated` (paced, guessed or read off a sketch) or
  `measured` (taped, or paced with a calibrated pace).
* Keep `"dataStatus": "partial"`. The validator refuses `"verified"` until every item is
  measured.
* Don't edit `Resources/Atria/Data/building.json` (the demo). Work in a new file.

## 1. Choose a floor and a reference frame (once)
1. **Floor:** use the same identifier as the signs. `G` matches the room labels G03–G26;
   confirm that this is the ground floor.
2. **Origin:** pick a point you can stand on and find again. A suggestion is the centre of the
   large octagonal hall (the room with G16 on its edge).
3. **Axes:** +z points along one well-defined straight wall, for example from the octagon
   towards the courtyard. +x is 90° clockwise from it when seen from above. Write both choices
   in the file's `notes`.

## 2. Survey a section
Bring a tape (5 m or more), your phone's compass, and the observation sheet.
* **Calibrate your pace:** walk a taped 10 m three times and average the step count.
  Pace length = 10 m ÷ steps.
* **For each straight corridor, record:**
  * start junction
  * bearing relative to your +z wall (the building should be in multiples of 30° or 60°; note
    any that aren't)
  * centre-line length
  * clear width, wall to wall
* **For each hex junction, record:**
  * one outer side length (for a regular hexagon this equals `outerRadius`)
  * one inner side length (equals `innerRadius`)
  * the bearing of one outer corner as seen from the centre (this is `rotation`)
* **For each door, record:**
  * room number exactly as printed
  * which wall it is on
  * distance from the nearest corner to the middle of the door
  * door width
* **Paced distances you can't convert yet:** record them as observations, for example
  `{ "id": "OBS_…", "from": "<node>", "to": "<node>", "steps": 15, "approximate": true, "note": "…" }`.
  Leave out `strideMetres` until you've calibrated your pace. Once it's set, the validator
  warns if the map disagrees with the paced distance by more than 25%.
* **Rooms with two doors:** record both. G23, G26, G25, G18, G21, G10 and others appear twice
  in the sketches.

Example survey table:

| Item | From | Bearing | Length | Width | Notes |
|---|---|---|---|---|---|
| Corridor octagon→left hex | Octagon W side | 240° | 14 paces = 10.5 m | 2.4 m | |
| Hex left-lower | — | corner at 30° | side 7.8 m | inner side 4.9 m | |
| G18 door 1 | top-left side | — | 1.6 m from corner | 1.0 m | |

## 3. Convert to coordinates
Starting from a known point (x, z), a straight run of length L at bearing θ ends at:

```
x' = x + L · sin θ        z' = z + L · cos θ        (θ in degrees, 0 = +z, 90 = +x)
```

* **Corridor:** `{ "shape": "corridor", "from": [x,z], "to": [x',z'], "width": w }`. Let
  corridors overlap their junction slightly; the wall generator merges overlaps cleanly.
* **Hex junction:**
  `{ "shape": "hexRing", "center": [cx,cz], "outerRadius": R, "innerRadius": r, "rotation": θc }`.
  The corridor's clear width is (R − r) · cos 30° = 0.866 · (R − r).
* **Nodes:**
  * a node at each corridor end and junction
  * on hex rings, a node at each **corner** on the centre line, radius (R + r) / 2 at bearing
    θc + 60°·k. Don't link side midpoints directly: the edge would cross the void, and the
    validator rejects it.
  * a node in front of each door, on the corridor centre line
  * a node just inside each entrance
* **Edges:**
  * add only links you've walked
  * add `distance` only if paced or taped along the real path
  * add `width` if measured
* **Doors:** the door centre lies on the wall line. If you have a door on a ring side, measure
  from the corner along that wall, then convert with the formula above.

## 4. Validate
* **In Unity:**
  1. Put drafts under `Assets/Atria/MappingData/`. This folder is not packaged into the APK.
  2. Select the file and run **Atria > Validate Selected JSON**.
  3. The Console lists errors, warnings, and "Still to survey" (the missing-measurement list).
  4. Fix every error before trying the file in the app.
* **To try it in the app:**
  * For the Block C draft (both wings), choose **Atria > Dataset: Use PRP Draft (Block C ground floor)** (not in
    Play mode). This sets **Building Data Override** on the `Atria` object and saves the scene.
  * For any other draft, drag it into **Building Data Override** in the Inspector and save the
    scene.
  * Press Play. The Console logs the dataset name, source path, status and `override: yes/no`.
    A non-verified draft shows a **PROVISIONAL PRP DRAFT** banner. An unreadable file shows a
    **DATA ERROR** banner; the app never falls back to the demo silently.
  * Use **Atria > Dataset: Use Demo (default)** to switch back.
* **Without Unity:** `dotnet test Tools/CoreTests` checks that every JSON file in
  `Resources/Atria/Data/` loads and has no validation errors.

Common errors and what they mean:

| Message | Fix |
|---|---|
| `Edge 'A'-'B' leaves walkable space near (x, z)` | The edge cuts a corner or goes through a wall. Add a corner node, or widen or extend the space. |
| `door N does not lie on a wall` | The door centre is more than 0.25 m from any generated wall. Re-check the measurement or the corridor width. |
| `Node 'N' is outside every walkable space` | The node coordinates or the space outline are wrong. |
| `dataStatus is "verified" but …` | Keep `"partial"` until every item is measured. |

## 5. Grow the map
Repeat sections 2–4 for the next junction. When the three entrances are mapped and connected,
set their `label`s and remove the demo file from the build: point **Building Resource Path**
at your file. Only when everything is measured does the validator allow `"verified"`, which
removes the warning banner.

## 6. Checking the Block C draft in Unity
1. Choose **Atria > Dataset: Use PRP Draft (Block C ground floor)**. You must not be in Play
   mode. The Console prints `Dataset set to Assets/Atria/MappingData/prp_g_left_draft.json`.
2. Press Play and check the Console line: `'PRP Block C ground floor, left + right wings
   (PROVISIONAL DRAFT)' … override: yes`. The banner reads **PROVISIONAL PRP DRAFT**.
3. **Left wing (unchanged):** start at *Entrance 1* and search `G23`, `G18` or `G21`. The route
   goes from the bend into L1 and up to L2, and never through the hall's right side.
4. **Right wing:** with either start point, search each of these and start the walkthrough:
   * `G08`, `G07`, `G10`, `G11` (lower-right junction R1, octagonal recessed centre)
   * `G03`, `G04`, `G05`, `G06`, `Electrical` (upper-right junction R2, hexagonal recessed
     centre)
   * `Lift` (on the right-hand wall of the corridor above R2)
5. **Hall to wings:**
   * From *DRAFT START: central hall*, a right-wing route leaves the hall's right side, follows
     the dog-leg corridor into R1's lower-left corner, and continues to R2 through R1's
     upper-right corner.
   * From *Entrance 1*, a right-wing route crosses the whole building: L1, the hall, then the
     right wing.
   * From *Main Entrance (position estimated)*, you start in the middle of the hall's
     garden-side doorway, facing into the hall. Every route first walks to the hall centre.
6. **While scrubbing, check that:**
   * the camera stays on corridor floor and never crosses a recessed centre
   * door signs face into the corridor
   * the corridors line up at the bends
7. Choose **Atria > Dataset: Use Demo (default)** to switch back. The demo is unchanged.
