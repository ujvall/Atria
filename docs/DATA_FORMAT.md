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
                 "polygon": [[x,z], ...] }],          // CONVEX walkable areas; split L/T shapes into pieces
  "nodes":    [{ "id": "N1", "floor": "F1", "kind": "corridor|intersection|entrance", "x": 0, "z": 0, "provenance": "..." }],
  "edges":    [{ "from": "N1", "to": "N2", "navigable": true, "kind": "corridor|stairs|lift",
                 "distance": 12.4,   // optional measured walking distance; else straight-line length
                 "width": 2.4,       // optional clear width
                 "provenance": "..." }],
  "entrances":[{ "id": "ENT_MAIN", "label": "Main entrance", "node": "N1",
                 "opening": { "x": 0, "z": -4, "width": 2.0 }, "provenance": "..." }],
  "rooms":    [{ "id": "PRP-101", "number": "PRP 101", "name": "...", "aliases": ["101"],
                 "floor": "F1", "accessNode": "N7",   // corridor node in front of the door; routes end here
                 "door": { "x": -1.2, "z": 6, "width": 1.0 }, "provenance": "..." }]
}
```

## Rules the validator enforces
* IDs are unique, and every reference (floor, node, access node) exists.
* Spaces are convex and have at least 3 points.
* Every node lies inside a walkable space.
* Every navigable same-floor edge stays inside walkable space across 90% of its clear width.
  An edge that cuts a corner is reported as passing through a wall.
* Every door and entrance opening lies within 0.25 m of a generated wall.
* Floor-changing edges must have kind `stairs` or `lift`.
* Warnings: a door more than 4 m from its access node, a room unreachable from every entrance,
  or an entrance count other than 3.

## Adding the real first floor
1. Draw the corridors as convex polygons, using measurements where you have them.
2. Put a node at every corridor junction, every bend, and in front of every door.
3. Connect only the links you have physically walked.
4. Add each room with the number exactly as printed on its sign.
5. Validate the file, then set provenance honestly.
