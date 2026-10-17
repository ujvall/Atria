# PRP sketch observations (provisional, not data)

**Status: unverified.** This sheet transcribes the five hand-drawn corridor sketches shared on
2026-10-17. They are drawn over the OSM 3D view, have no scale, and don't show the entrances.
They establish **topology and room labels only**. No coordinates, lengths, widths or door
positions can be **measured** from them. Any draft built from them uses estimates only.
Use this sheet as the checklist for the on-site survey ([MAPPING_GUIDE.md](../MAPPING_GUIDE.md)).

**Drafted so far:** the left section (hall, L1, L2), in
[`Assets/Atria/MappingData/prp_g_left_draft.json`](../../Assets/Atria/MappingData/prp_g_left_draft.json).
All of its geometry is estimated; see "Left-section draft" below.

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

## Left-section draft (`prp_g_left_draft.json`)
**Status: partial, estimated. Not for real navigation.**

* **Covered:**
  * the central hall, with G16
  * the corridor from the hall to L1
  * L1, with G18 ×2 and G21 ×2
  * the corridor from L1 to L2, which bends and has G17 on it
  * L2, with G23 ×2, G26 ×2 and G25 ×2
* **Reference frame:** the origin is the hall centre, +z points towards the courtyard (up in
  the overview sketch), and +x is right in the sketch.
* **Estimated sizes:** these were chosen so the topology can be drawn. None was measured, and
  the sketches have no scale.
  * **Hall:** regular octagon, 11.5 m centre-to-corner.
  * **Rings:** regular hexagons, 5.0 m outer and 2.2 m inner centre-to-corner, corner pointing
    +z. That gives about a 2.4 m clear corridor.
  * **Corridors:** 2.4 m wide.
  * **Positions:** L1 at (-24, 2) and L2 at (-34.6, 16.1), taken from the overview sketch at
    about 0.1 m per pixel.
  * **Two-door rooms:** doors 1 m either side of the side's midpoint. G25 is at −1.5 m and
    +0.4 m, so it stays clear of the corridor at L2's south corner.
  * **G16:** at the middle of the hall's lower-right side.
* **Unknown, so left out or left unknown:**
  * all entrances (the only start point is "DRAFT START: central hall (not an entrance)")
  * the corridor beyond the top of L2
  * the right arm and the hall's lower-right corridor
  * which wall G17 is on, and where along it (its door is not drawn)
  * the location of Physics Research Lab -II (listed with no doors, so it is searchable but
    unmapped; it may be G18)
  * floor confirmation and stairs
* **Simplifications:** the sketched junction outlines are closer to octagons, but they are
  modelled as regular hexagons. The corridor from L1 to L2 is modelled as two straight runs
  with a 90° bend.
