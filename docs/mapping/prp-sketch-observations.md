# PRP sketch observations (provisional, not data)

**Status: unverified.** This sheet transcribes the five hand-drawn corridor sketches shared on
2026-10-17. They are drawn over the OSM 3D view, have no scale, and don't show the entrances.
They establish **topology and room labels only**. No coordinates, lengths, widths or door
positions can honestly be derived from them, so no building JSON was generated from them.
Use this sheet as the checklist for the on-site survey ([MAPPING_GUIDE.md](../MAPPING_GUIDE.md)).

Labels marked **?** were hard to read in the sketch. Directions such as "left" or "up" are
as drawn in the overview sketch, with the courtyard at the top. They are not compass
directions.

## Overall shape
* One large **central octagonal hall**. **G16** is on its lower-right side.
* **Left arm:** octagon → corridor left → **hex junction L1** (left-lower) → corridor up →
  **hex junction L2** (left-upper) → corridor continues up, beyond the sketch.
* **Right arm:** octagon → corridor right → **hex junction R1** (right-lower) → corridor up →
  **hex junction R2** (right-upper) → corridor continues up, beyond the sketch.
* Every hex junction is drawn as a corridor ring around a central void (inner outline roughly
  octagonal or hexagonal). In the data this is a `hexRing` space.
* On the octagon's right side, a second corridor appears to leave towards the lower right, with
  a short stub beside it. **Where it leads is unclear.**

## Junctions and rooms

| Section | Labels seen (position in sketch) | Notes |
|---|---|---|
| Octagon | G16 (lower-right side) | Size unknown; whether the hall is walkable floor or has a central feature is unknown |
| L1 (left-lower hex) | G18 ×2 (top side); "Physics Research Lab -II" (above G18); G21 ×2 (bottom side) | G18 and G21 each look like rooms with two doors. Is the Physics Research Lab G18 or a separate room? |
| Corridor L1→L2 | G17 (on the corridor, near L2) | Which wall is G17 on? |
| L2 (left-upper hex) | G23 ×2 (left side); G26 ×2 (upper-right side); G25 ×2 (lower-right side) | Three two-door rooms |
| R1 (right-lower hex) | G08? and G08/G09? (top-left); G07 (top-right); G11 (bottom-left); G10 ×2 (bottom-right) | The top-left labels are unclear |
| R2 (right-upper hex) | G03 ×2 (top-left); G04 ×2 (left); G07? (bottom-left); G05/G06? ×2 (right) with "Electrical room" between them; G05/G06? ×2 (bottom-right) | G07 appears beside both R1 and R2. Is it one room between them, or a misread? |
| Corridor above R2 | LIFT (right wall) | First vertical connection: model it as a `lift` node, with no cross-floor edge until floors are known |

## Not shown in the sketches (needed before routing)
1. The three student entrances: where they are and what they're called.
2. Every length, width and hex size.
3. Exact door positions along each wall.
4. Where the corridors at the top of L2 and R2, and the lower-right corridor from the octagon,
   lead.
5. Whether rooms exist on the corridors between junctions, beyond G17.
6. Stairs locations, and which floor this is (G = ground?).
