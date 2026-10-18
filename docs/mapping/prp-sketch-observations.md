# PRP sketch observations (provisional, not data)

**Status: unverified.** This sheet transcribes the five hand-drawn corridor sketches shared on
2026-10-17. They are drawn over the OSM 3D view, have no scale, and don't show the entrances.
They establish **topology and room labels only**. No coordinates, lengths, widths or door
positions can be **measured** from them. Any draft built from them uses estimates only.
Use this sheet as the checklist for the on-site survey ([MAPPING_GUIDE.md](../MAPPING_GUIDE.md)).

**Second set of drawings (2026-10-17):** a hand overview sketch of the left side, and an
expanded drawing of the G18/G21 junction (L1). See "Observations from the second drawings" below.

**Drafted so far:** the left section (hall, L1, L2) and the right wing (R1, R2, lift), in
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

## Observations from the second drawings
### Observed (shown clearly in the drawings)
* **Entrance 1** is on the left of L1, at the outer corner of the bend where the corridor from
  L1 turns up towards G17 and L2. In the overview sketch it is labelled "entrance 1", left of
  the lower of the two circled hexagon junctions.
* **L1 (expanded drawing):**
  * the corridor runs all the way round the central area
  * the central outline is drawn as an **octagon**; the outer outline is six-sided
  * **G18 ×2** is on the upper-right side, with **Physics Research Lab -II** labelled beside
    them
  * **G21 ×2** is on the lower side
  * corridors leave towards the upper-left (to the bend, Entrance 1 and G17) and towards the
    right (to the longer corridor and hall)
* **Overview sketch:**
  * L2 (upper) and L1 (lower) are a close pair joined via the bend
  * a large polygon lies to the right of L1 (taken to be the central hall)
  * a corridor leaves L2's upper left and goes up

### Approximate (observed but not measured)
* **About 15 normal walking steps** from Entrance 1 to the start of the hexagonal junction
  (L1), before turning into the longer corridor.
  * Stored in the draft as observation `OBS_ENT1_TO_L1` (`CORR_BEND` → `L1_S270`) with
    `approximate: true`.
  * **Not converted to metres**, because no stride is calibrated.
  * For reference only, not used in the data: at typical strides of 0.6–0.8 m this would be
    roughly 9–12 m. The draft's estimated corridor gives about 7.5 m from the bend node and
    about 6.3 m to L1's outer edge, so the corridor is probably drawn too short. It is left
    unchanged until the stride is calibrated.

### Changed in the draft
* Added `ENT_1` "Entrance 1 (observed; position estimated)" at the bend node. Its 1.6 m opening
  is in the west wall facing the corridor to L1. It is listed first, so the app selects it by
  default; "DRAFT START: central hall (not an entrance)" is kept as a second start point.
* L1's centre is now an octagonal void: `innerSides: 8`, inner radius 2.0 m (estimated). It is
  still drawn as the darker recessed area, and routes go round it.
* Nothing else moved: no room, door, corridor or connection was added or repositioned.

### Still unknown (needs on-site measurement)
* **Stride:** your calibrated stride length, from a taped 10 m walked three times. Then add
  `"strideMetres"` to `OBS_ENT1_TO_L1` and the validator checks the corridor length.
* **Entrance 1:** which wall it is in (west, south or the diagonal corner as drawn), its width,
  and whether it has doors.
* **L1 dimensions:** outer and inner sizes, corridor widths, and the exact angles at which the
  corridors join L1. They are drawn near L1's left and right corners; the draft joins them at
  the side midpoints.
* **L2's centre:** a hexagon in the overview sketch, octagonal in the first screenshots. It is
  left as a hexagon.
* **Hall:** drawn as a large hexagon-like polygon in the overview and as an octagon earlier.
  It is left as an octagon.
* **Lower junction in the overview sketch** (octagonal centre, an "entrance" on its left, a
  label that may read "ECE rooms", "main entrance" to the right): its position relative to
  the hall is unclear, so it is not modelled.
* **Other features in the overview sketch:** the hatched element at the top of L2 (stairs?)
  and where L2's upper-left corridor leads.
* **Door positions:** G17's door position, and the G18/G21 door offsets along their sides.

## Right wing (R1, R2) in the draft
**Source:** the first sketch set's right-hand close-ups, plus your label list (G07, G08, G03–G06,
Electrical Room, Lift). The new annotated drawings mentioned in the request did not arrive in
that session.

### Observed
* The corridor leaves the hall's right side heading down-right, then turns up-right into R1's
  lower-left corner. A second branch continues down-right; it is **not modelled**, because its
  destination is unknown.
* **R1** (lower-right): a six-sided outline with a roughly **octagonal** centre.
  * G08 ×2 on the upper-left side; your list resolves the earlier G08/G09 reading
  * G07 on the upper-right side
  * G11 on the lower-left side
  * G10 ×2 on the lower-right side
  * G10 and G11 are clearly labelled in the close-ups, although your list doesn't mention them
* The corridor leaves R1's upper-right corner heading up-right, then runs straight up into
  R2's bottom corner.
* **R2** (upper-right): a six-sided outline with a **hexagonal** centre.
  * G03 ×2 on the upper-left side
  * G04 ×2 on the left side
  * G05 ×2 on the right side, with the **Electrical room** labelled between them
  * G06 ×2 on the lower-right side
* A corridor runs up from R2's top corner. **LIFT** is on its right-hand wall.

### Estimated (in the JSON, not measured)
* **Centres:** R1 at (27.6, 0.3) and R2 at (34.53, 15.1), from the overview at about 0.1 m per
  pixel. Both bends are 8 m from R1's centre.
* **Sizes:** outer 5.0 m for both rings. Inner 1.85 m for R1's octagon, kept smaller than L1's
  so the 2.4 m corridor stays clear on sides with no side corridor. Inner 2.2 m for R2's
  hexagon.
* **Doors:** offsets along each side are estimated and kept clear of where corridors attach.
* **Electrical Room:** searchable as "Electrical Room", with no room number.
* **Lift:** searchable landmark "Lift", with a 1.2 m door 1.5 m above R2. The corridor above R2
  ends in a placeholder wall 4 m past the ring.

### Still unknown
* whether G05 and G06 are the right way round (the right-side labels were hard to read)
* whether the label beside R2's lower-left side is a second G07 door; it is **not added**
* where the down-right branch from the hall corridor leads, and where R2's upper corridor leads
* whether the lift serves other floors (no vertical route is modelled), and where the stairs
  are
* all real dimensions, and the right wing's entrances (the "main entrance" in the overview is
  not placed)

## Main Entrance (`ENT_MAIN`)
* **Modelled:** a doorway centred on the hall's courtyard/garden-facing side (+z). The camera
  starts 0.8 m inside it, facing into the hall.
* **Unknown:**
  * **Side:** taken from your description; the screenshot was not received.
  * **Width:** 2.4 m is an estimate.
  * **Doors:** whether the entrance has doors.
  * **Relation to the first overview sketch:** that sketch has a "main entrance" label to the
    right of a lower octagonal junction. Please confirm it is the same entrance.

## R2 foot-placement counts (2026-10-18)
**Method:** consecutive heel-to-toe placements wearing EU 44 Crocs. These are counts, not feet,
and approximate. In the model, one placement ≈ 0.30 m.

| Segment (as identified) | Count | Applied |
|---|---|---|
| Straight corridor above R2: width | 8 | 2.4 m (same as the existing corridors) |
| Straight corridor above R2: length, from R2's top corner | 33 | 9.9 m; the lift stays 1.5 m up it |
| Angled approach, R1's corner to the bend below R2 | 23 | 6.9 m |
| Junction side (R2) | 21 | 6.3 m outer side; the ring corridor kept at 8 placements, so the centre side is about 3.5 m |
| Floor height | — | 2.9 m (you confirmed 2.75–3 m) |

* **Not measured:** the vertical corridor from the bend up into R2 (kept at 5.8 m) and every
  other junction.
* **Moved:** R2 shifted from (34.5, 15.1) to (37.9, 18.35) only because these lengths changed.
  Its doors keep the same sides and offsets.
* **Sketch image:** not received. The junction was identified from your description; if it is
  a different junction, the counts move there.

## Left wing as the mirror of the right wing (2026-10-18)
* **Your instruction:** the right wing is the correct format; the left wing should look like
  its mirror image.
* **Geometry:** the left-wing positions, junction sizes and corridor angles are now the right
  wing's, reflected across the hall's centre line. L2 is therefore the same size as R2 (its
  own size has not been counted). A corridor above L2 matches the one your overview sketch
  shows leaving L2 upwards; its length is mirrored and its destination unknown.
* **Unchanged from your left-wing observations:** room labels and sides, the two-door rooms,
  G17 on the corridor up to L2, and Entrance 1 at the outer corner of the bend. The bend is
  now where the angled approach from L1 turns straight up.
* The earlier "Left-section draft" and "Changed in the draft" sections above describe the
  previous geometry. Positions and sizes in them are out of date.

## Lower-left block branch and shorter L1 run (2026-10-18)
* **Observed (your annotation):** the corridor from the hall does not only lead into L1. At the
  bend just before L1 it continues down-left, passing below L1, to a separate block (the
  wing seen at lower left in the 3D map view).
* **Modelled:** an 8 m corridor branch at about 245° yaw, kept about 0.8 m clear of L1's
  outline. It ends at a placeholder labelled "Towards the lower-left block (not mapped yet)".
* **Changed:** L1 → Entrance 1 bend is now 3.0 m (estimated; you said it is shorter than drawn).
* **Still unknown:**
  * the block's layout, rooms and how far away it is
  * whether the right wing's down-right branch leads to a matching block

## Lower-left block as a destination (2026-10-18)
* **Modelled:** the branch's end is now a searchable destination, `LOWER-LEFT-BLOCK`.
  * Its provenance is estimated, and it has no door position.
  * Nothing inside the block is mapped.
* **Junction:** the branch starts 0.5 m back along the hall corridor. This closes the wall
  notch at the bend before L1.
* **Photos:** the hex centres are open-air courtyards. They stay non-walkable.
* **Still unknown:**
  * the block's entrance position and its distance from the bend
  * the real corridor widths and lengths, apart from the R2 counts

## Straight lane at the L1/R1 junctions (2026-10-18)
* **Observed (your corrected sketch):** the hall corridor carries straight on towards the
  lower-left block. The L1 connector drops down to meet it. The two do not form two angled
  segments.
* **Modelled:**
  * a single lane at about 252° yaw
  * the L1 connector lengthened to 4.29 m past the ring (estimated)
  * the same change mirrored at R1
* **Not changed:** the hex junctions are not rotated, so the doors and Entrance 1 stay put.

## Shorter hall corridors (2026-10-18)
* **Observed (your marked screenshot):** the hall-to-junction stretch was drawn too long.
* **Modelled:** both wings moved 4.5 m towards the hall along the lane, as rigid blocks. The
  stretch is now 6.57 m from `HALL_W` (estimated, not measured).
