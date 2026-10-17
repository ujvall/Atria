using System;
using System.Collections.Generic;
using System.Numerics;
using Atria.Core.Geometry;
using Atria.Core.Navigation;
using Atria.Core.Search;

namespace Atria.Core.Data
{
    public sealed class ValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        /// <summary>Survey to-do list: everything still placeholder/estimated or unrecorded. Not an error.</summary>
        public readonly List<string> MissingMeasurements = new List<string>();
        public bool IsValid => Errors.Count == 0;

        public override string ToString() =>
            $"{Errors.Count} error(s), {Warnings.Count} warning(s), {MissingMeasurements.Count} missing measurement(s)" +
            (Errors.Count > 0 ? "\nErrors:\n  " + string.Join("\n  ", Errors) : "") +
            (Warnings.Count > 0 ? "\nWarnings:\n  " + string.Join("\n  ", Warnings) : "");
    }

    /// <summary>
    /// Consistency checks for building data. The most important: every navigable same-floor edge
    /// must lie inside walkable space across its clear width, which is what guarantees routes
    /// (and the camera) never pass through walls.
    /// </summary>
    public static class BuildingValidator
    {
        /// <summary>Fraction of the edge's clear width that must be walkable either side of its centreline.</summary>
        public const float LateralCheckFraction = 0.45f;
        private const float SampleSpacing = 0.25f;
        /// <summary>A door further than this from its room's access node is suspicious.</summary>
        public const float MaxDoorToAccessNode = 4f;

        public static ValidationReport Validate(BuildingData b)
        {
            var report = new ValidationReport();
            CheckDuplicates(report, "node", b.Nodes, n => n.Id);
            CheckDuplicates(report, "room", b.Rooms, r => r.Id);
            CheckDuplicates(report, "entrance", b.Entrances, e => e.Id);
            CheckDuplicates(report, "space", b.Spaces, s => s.Id);
            CheckDuplicates(report, "floor", b.Floors, f => f.Id);

            if (b.WalkingSpeedMps <= 0f) report.Errors.Add("walkingSpeedMps must be positive");
            if (b.Entrances.Count != 3) report.Warnings.Add($"Expected exactly 3 entrances, found {b.Entrances.Count}");

            var layouts = new Dictionary<string, FloorLayout>();
            foreach (FloorData f in b.Floors) layouts[f.Id] = FloorLayout.Build(b, f);

            foreach (SpaceData s in b.Spaces)
            {
                if (b.FindFloor(s.FloorId) == null) report.Errors.Add($"Space '{s.Id}' references unknown floor '{s.FloorId}'");
                if (s.Polygon.Count < 3) report.Errors.Add($"Space '{s.Id}' needs at least 3 points");
                else if (!Polygon2D.IsConvex(s.Polygon)) report.Errors.Add($"Space '{s.Id}' is not convex; split it into convex pieces");
            }

            foreach (NodeData n in b.Nodes)
            {
                if (!layouts.TryGetValue(n.FloorId, out FloorLayout layout))
                {
                    report.Errors.Add($"Node '{n.Id}' references unknown floor '{n.FloorId}'");
                    continue;
                }
                if (!layout.IsWalkable(Plan(n.Position))) report.Errors.Add($"Node '{n.Id}' is outside every walkable space");
            }

            foreach (EdgeData e in b.Edges)
            {
                NodeData from = b.FindNode(e.From), to = b.FindNode(e.To);
                string name = $"Edge '{e.From}'-'{e.To}'";
                if (from == null || to == null) { report.Errors.Add($"{name} references an unknown node"); continue; }
                if (!e.Navigable) continue;
                if (e.MeasuredDistance.HasValue && e.MeasuredDistance.Value <= 0f) report.Errors.Add($"{name} has a non-positive distance");
                if (from.FloorId != to.FloorId)
                {
                    if (e.Kind != "stairs" && e.Kind != "lift") report.Warnings.Add($"{name} changes floor but is not marked as stairs or lift");
                    continue; // vertical connections are checked by survey, not by plan geometry
                }
                if (layouts.TryGetValue(from.FloorId, out FloorLayout layout))
                    CheckEdgeClearance(report, name, layout, Plan(from.Position), Plan(to.Position), e.Width ?? b.DefaultCorridorWidth);
            }

            foreach (EntranceData en in b.Entrances)
            {
                NodeData node = b.FindNode(en.NodeId);
                if (node == null) { report.Errors.Add($"Entrance '{en.Id}' references unknown node '{en.NodeId}'"); continue; }
                if (en.Opening != null && layouts.TryGetValue(node.FloorId, out FloorLayout layout) &&
                    (!layout.Entrances.TryGetValue(en.Id, out OpeningPlacement p) || !p.Placed))
                    report.Errors.Add($"Entrance '{en.Id}' opening does not lie on a wall (within {WallGenerator.OpeningSnapTolerance} m)");
            }

            CheckDuplicates(report, "room number", b.Rooms, r => RoomSearch.Normalize(r.Number), asWarning: true);
            foreach (RoomData r in b.Rooms)
            {
                if (b.FindFloor(r.FloorId) == null) report.Errors.Add($"Room '{r.Id}' references unknown floor '{r.FloorId}'");
                if (r.Doors.Count == 0) { report.Warnings.Add($"Room '{r.Id}' has no doors yet (searchable, but not routable)"); continue; }
                for (int i = 0; i < r.Doors.Count; i++)
                {
                    RoomDoor door = r.Doors[i];
                    string name = $"Room '{r.Id}' door {i}";
                    NodeData access = b.FindNode(door.AccessNodeId);
                    if (access == null) { report.Errors.Add($"{name} references unknown access node '{door.AccessNodeId}'"); continue; }
                    if (access.FloorId != r.FloorId) report.Errors.Add($"{name} access node is on a different floor");
                    if (door.Opening == null) { report.MissingMeasurements.Add($"{name}: position unknown (not drawn)"); continue; }
                    if (layouts.TryGetValue(r.FloorId, out FloorLayout layout))
                    {
                        if (!layout.Doors.TryGetValue(FloorLayout.DoorKey(r.Id, i), out OpeningPlacement p) || !p.Placed)
                            report.Errors.Add($"{name} does not lie on a wall (within {WallGenerator.OpeningSnapTolerance} m)");
                        else if (layout.IsVoidBehind(p.Center, p.Inward))
                            report.Errors.Add($"{name} opens onto a floor void, not a room");
                    }
                    if (Vector2.Distance(Plan(access.Position), door.Opening.Center) > MaxDoorToAccessNode)
                        report.Warnings.Add($"{name} is more than {MaxDoorToAccessNode} m from its access node");
                }
            }

            CheckIsolatedNodes(report, b);
            CheckObservations(report, b);
            CheckVerificationStatus(report, b);
            if (report.Errors.Count == 0) CheckReachability(report, b);
            return report;
        }

        /// <summary>Paced distances may differ from the geometry by this fraction before a warning.</summary>
        public const float ObservationTolerance = 0.25f;

        private static void CheckObservations(ValidationReport report, BuildingData b)
        {
            var finder = new RouteFinder(b);
            foreach (SurveyObservation o in b.Observations)
            {
                string name = $"Observation '{o.Id}'";
                if (b.FindNode(o.FromNodeId) == null || b.FindNode(o.ToNodeId) == null)
                {
                    report.Errors.Add($"{name} references an unknown node");
                    continue;
                }
                if (!o.StrideMetres.HasValue)
                {
                    report.MissingMeasurements.Add($"{name}: {o.Steps:0.#} steps, stride not calibrated (no metric distance derived)");
                    continue;
                }
                float paced = o.Steps * o.StrideMetres.Value;
                float? mapped = finder.ShortestDistance(o.FromNodeId, o.ToNodeId);
                if (!mapped.HasValue) report.Warnings.Add($"{name}: '{o.FromNodeId}' and '{o.ToNodeId}' are not connected in the graph");
                else if (Math.Abs(mapped.Value - paced) > ObservationTolerance * paced)
                    report.Warnings.Add($"{name}: map distance {mapped.Value:0.0} m disagrees with paced {paced:0.0} m ({o.Steps:0.#} steps x {o.StrideMetres.Value:0.00} m)");
            }
        }

        private static void CheckIsolatedNodes(ValidationReport report, BuildingData b)
        {
            var linked = new HashSet<string>();
            foreach (EdgeData e in b.Edges)
                if (e.Navigable) { linked.Add(e.From); linked.Add(e.To); }
            foreach (NodeData n in b.Nodes)
                if (!linked.Contains(n.Id)) report.Warnings.Add($"Node '{n.Id}' has no navigable connections");
        }

        /// <summary>
        /// Lists what still needs surveying, and refuses a "verified" building that contains any
        /// unmeasured item: provisional data must never be presented as verified.
        /// </summary>
        private static void CheckVerificationStatus(ValidationReport report, BuildingData b)
        {
            var missing = report.MissingMeasurements;
            foreach (SpaceData s in b.Spaces)
                if (s.Provenance != Provenance.Measured) missing.Add($"Space '{s.Id}' outline is {s.Provenance}");
            foreach (NodeData n in b.Nodes)
                if (n.Provenance != Provenance.Measured) missing.Add($"Node '{n.Id}' position is {n.Provenance}");
            foreach (EdgeData e in b.Edges)
            {
                if (e.Provenance != Provenance.Measured) missing.Add($"Edge '{e.From}'-'{e.To}' is {e.Provenance}");
                if (!e.Width.HasValue) missing.Add($"Edge '{e.From}'-'{e.To}' width not recorded (default {b.DefaultCorridorWidth} m used)");
            }
            foreach (EntranceData en in b.Entrances)
                if (en.Provenance != Provenance.Measured) missing.Add($"Entrance '{en.Id}' is {en.Provenance}");
            foreach (RoomData r in b.Rooms)
            {
                if (r.Provenance != Provenance.Measured) missing.Add($"Room '{r.Id}' is {r.Provenance}");
                for (int i = 0; i < r.Doors.Count; i++)
                    if (r.Doors[i].Provenance != Provenance.Measured) missing.Add($"Room '{r.Id}' door {i} is {r.Doors[i].Provenance}");
            }

            if (b.IsVerified && missing.Count > 0)
                report.Errors.Add($"dataStatus is \"verified\" but {missing.Count} item(s) are unmeasured or unknown; use \"partial\" until every item is measured");
        }

        private static void CheckEdgeClearance(ValidationReport report, string name, FloorLayout layout, Vector2 a, Vector2 c, float width)
        {
            float len = Vector2.Distance(a, c);
            if (len < 1e-4f) return;
            Vector2 dir = (c - a) / len;
            var side = new Vector2(-dir.Y, dir.X) * (width * LateralCheckFraction);
            int samples = Math.Max(1, (int)Math.Ceiling(len / SampleSpacing));
            for (int i = 0; i <= samples; i++)
            {
                Vector2 p = Vector2.Lerp(a, c, (float)i / samples);
                if (!layout.IsWalkable(p) || !layout.IsWalkable(p + side) || !layout.IsWalkable(p - side))
                {
                    report.Errors.Add($"{name} leaves walkable space near ({p.X:0.##}, {p.Y:0.##}) for a {width:0.##} m clear width");
                    return;
                }
            }
        }

        private static void CheckReachability(ValidationReport report, BuildingData b)
        {
            var finder = new RouteFinder(b);
            foreach (RoomData r in b.Rooms)
            {
                if (r.Doors.Count == 0) continue; // already reported as unmapped
                bool any = false;
                foreach (EntranceData e in b.Entrances)
                    if (finder.FindRoute(e.Id, r.Id).Success) { any = true; break; }
                if (!any) report.Warnings.Add($"Room '{r.Id}' is not reachable from any entrance");
            }
        }

        private static void CheckDuplicates<T>(ValidationReport report, string kind, List<T> items, Func<T, string> id, bool asWarning = false)
        {
            var seen = new HashSet<string>();
            foreach (T item in items)
                if (!seen.Add(id(item))) (asWarning ? report.Warnings : report.Errors).Add($"Duplicate {kind} '{id(item)}'");
        }

        private static Vector2 Plan(Vector3 p) => new Vector2(p.X, p.Z);
    }
}
