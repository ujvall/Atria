# Building data format (schemaVersion 1)

File: `Assets/Atria/Resources/Atria/Data/building.json`. To check it, run **Atria > Validate
Building Data** in Unity or `dotnet test Tools/CoreTests`.

## Coordinates
* Units are **metres**. The plan uses **x** and **z**, and **y** is up, matching Unity world space.
* The origin and axis directions are building-local. They are *not* geographic. Pick one
  surveyed reference point, for example the centre of the main entrance threshold, and one
  reference wall to define +z. Write both choices in `notes`.
* Polygon points are written as `[x, z]`.
* Yaw: 0° faces +z, and 90° faces +x.

## Provenance (required on everything you add)
| value | meaning |
|---|---|
| `placeholder` | Invented to exercise the software. |
| `estimated` | Taken from imagery, pacing or memory; not measured. |
| `measured` | Measured on site or taken from drawings. |

A missing value counts as `placeholder`. `dataStatus` at the top level is `sample`, `partial`
or `verified`. The app shows a warning banner unless it is `verified`.

## Sections
```jsonc
{
  "schemaVersion": 1,
  "buildingId": "PRP", "displayName": "Pearl Research Park", "dataStatus": "partial",
  "walkingSpeedMps": 1.2,          // ETA assumption
  "defaultCorridorWidth": 2.4,     // used when an edge has no "width"
  "defaultDoorWidth": 1.0,
  "floors":   [{ "id": "F1", "name": "First floor", "elevation": 0, "wallHeight": 3.0 }],
  "spaces":   [{ "id": "CORR_1", "floor": "F1", "kind": "corridor", "provenance": "measured",
                 "polygon": [[x,z], ...] },           // CONVEX walkable areas; split L/T shapes into pieces
              { "id": "CORR_2", "floor": "F1", "shape": "corridor",  // straight corridor at any angle
                 "from": [x,z], "to": [x,z], "width": 2.4, "provenance": "estimated" },  // centre line + clear width
              { "id": "HEX_A", "floor": "F1", "shape": "hexRing",   // hexagonal corridor round a central void
                 "center": [x,z], "outerRadius": 8, "innerRadius": 5, "rotation": 30,  // radii centre-to-corner, metres
                 "provenance": "estimated" }],        // expands to six convex pieces HEX_A#0..#5
  "nodes":    [{ "id": "N1", "floor": "F1", "kind": "corridor|intersection|entrance", "x": 0, "z": 0, "provenance": "..." }],
  "edges":    [{ "from": "N1", "to": "N2", "navigable": true, "kind": "corridor|stairs|lift",
                 "distance": 12.4,   // optional measured walking distance; else straight-line length
                 "width": 2.4,       // optional clear width
                 "provenance": "..." }],
  "entrances":[{ "id": "ENT_MAIN", "label": "Main entrance", "node": "N1",
                 "opening": { "x": 0, "z": -4, "width": 2.0 }, "provenance": "..." }],
  "rooms":    [{ "id": "G23", "number": "G23", "name": "...", "aliases": ["G 23"],
                 "floor": "G", "provenance": "estimated",
                 "doors": [                           // one entry per door; routes go to the nearest one
                   { "accessNode": "N7", "x": -1.2, "z": 6, "width": 1.0, "provenance": "measured" },
                   { "accessNode": "N8" }             // door exists, exact position unknown: not drawn
                 ] },
               { "id": "G99", "number": "G99", "floor": "G" },  // known room, location unknown: searchable, not routable
               { "id": "OLD", "number": "OLD", "floor": "F1",   // single-door shorthand (still supported)
                 "accessNode": "N7", "door": { "x": -1.2, "z": 6 } }]
}
```

Any other key (for example `"note": "door seen in sketch 3, unpaced"`) is ignored by the loader,
so use notes freely.

## Recording what you don't know
* **Unknown connection:** leave the edge out, or add it with `"navigable": false` and a note.
  The router never uses either.
* **Unknown length:** omit `distance`; the straight-line length between the nodes is used.
* **Unknown width:** omit `width`; `defaultCorridorWidth` is used, and the gap is listed as a
  missing measurement.
* **Unknown door position:** give the door's `accessNode` only, with no `x`/`z`.
* **Room known, location unknown:** list it with no doors.
* **Corner nodes on hex rings:** a straight edge between two side midpoints cuts across the
  inner corner into the void, so the validator rejects it. Add a node at each corner on the
  ring's centre line (radius = (outer + inner) / 2).
* **Status:** use `"partial"` while mapping. `"verified"` is rejected while any item is
  `placeholder` or `estimated`, any door position is unknown, or any edge width is missing.

## Rules the validator enforces
* IDs are unique, and every reference (floor, node, access node) exists.
* Spaces are convex and have at least 3 points.
* Every node lies inside a walkable space.
* Every navigable same-floor edge stays inside walkable space across 90% of its clear width.
  An edge that cuts a corner is reported as passing through a wall.
* Every door and entrance opening lies within 0.25 m of a generated wall.
* Floor-changing edges must have kind `stairs` or `lift`.
* Room IDs are unique, and every door's access node exists on the room's floor.
* `"verified"` status requires every item to be measured.
* **Warnings:**
  * a door more than 4 m from its access node
  * a room unreachable from every entrance
  * a room with no doors yet
  * a node with no navigable connection
  * two rooms with the same printed number
  * an entrance count other than 3
* **Missing measurements:** a separate survey to-do list, not errors. It covers every
  placeholder or estimated item, unknown door positions and unrecorded widths.

## Adding the real building
Follow [MAPPING_GUIDE.md](MAPPING_GUIDE.md), which takes you from a sketch to a validated file.
