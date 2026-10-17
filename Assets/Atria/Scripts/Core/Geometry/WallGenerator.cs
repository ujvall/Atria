using System.Collections.Generic;
using System.Numerics;

namespace Atria.Core.Geometry
{
    /// <summary>A straight wall piece in plan. <see cref="Inward"/> points into the walkable side.</summary>
    public struct WallSegment
    {
        public Vector2 A;
        public Vector2 B;
        public Vector2 Inward;
        /// <summary>True when the non-walkable side is an open floor void (drawn as a rail, not a wall).</summary>
        public bool BordersVoid;
        public float Length => Vector2.Distance(A, B);
    }

    public sealed class OpeningRequest
    {
        public string Tag;
        public Vector2 Center;
        public float Width;
    }

    /// <summary>Where an opening ended up after being snapped onto a wall.</summary>
    public sealed class OpeningPlacement
    {
        public string Tag;
        public bool Placed;
        /// <summary>Opening extents on the wall line (A to B).</summary>
        public Vector2 A;
        public Vector2 B;
        public Vector2 Inward;
        /// <summary>Distance from the requested centre to the wall it snapped to.</summary>
        public float SnapDistance;
        public Vector2 Center => (A + B) * 0.5f;
    }

    /// <summary>
    /// Derives walls from walkable space: walls are the outline of the union of convex space
    /// polygons, so overlapping/adjacent corridors join cleanly at junctions without any
    /// hand-authored wall list. Doors and entrances are then cut as gaps.
    /// </summary>
    public static class WallGenerator
    {
        /// <summary>Max distance an opening centre may be from a wall and still snap onto it.</summary>
        public const float OpeningSnapTolerance = 0.25f;
        private const float OutwardProbe = 1e-3f;
        private const float MinSegment = 1e-3f;

        public static List<WallSegment> BuildOutline(IList<IList<Vector2>> spaces)
        {
            var walls = new List<WallSegment>();
            var splits = new List<float>();

            for (int i = 0; i < spaces.Count; i++)
            {
                IList<Vector2> poly = spaces[i];
                for (int e = 0; e < poly.Count; e++)
                {
                    Vector2 a = poly[e], b = poly[(e + 1) % poly.Count];
                    Vector2 dir = b - a;
                    float len = dir.Length();
                    if (len < MinSegment) continue;
                    dir /= len;
                    var outward = new Vector2(dir.Y, -dir.X); // right of a CCW edge

                    // Split the edge wherever it crosses another polygon's boundary; coverage
                    // can only change at those points.
                    splits.Clear();
                    splits.Add(0f);
                    splits.Add(1f);
                    for (int j = 0; j < spaces.Count; j++)
                    {
                        if (j == i) continue;
                        IList<Vector2> q = spaces[j];
                        for (int k = 0; k < q.Count; k++)
                            Polygon2D.SegmentSplitParameters(a, b, q[k], q[(k + 1) % q.Count], splits);
                    }
                    splits.Sort();

                    for (int s = 0; s + 1 < splits.Count; s++)
                    {
                        float t0 = splits[s], t1 = splits[s + 1];
                        if ((t1 - t0) * len < MinSegment) continue;
                        Vector2 mid = Vector2.Lerp(a, b, (t0 + t1) * 0.5f);
                        if (!IsOuterBoundary(spaces, i, mid, outward)) continue;
                        walls.Add(new WallSegment { A = Vector2.Lerp(a, b, t0), B = Vector2.Lerp(a, b, t1), Inward = -outward });
                    }
                }
            }
            return MergeCollinear(walls);
        }

        private static bool IsOuterBoundary(IList<IList<Vector2>> spaces, int self, Vector2 mid, Vector2 outward)
        {
            Vector2 probe = mid + outward * OutwardProbe;
            for (int j = 0; j < spaces.Count; j++)
            {
                if (j == self) continue;
                // Walkable space continues across this edge: not a wall.
                if (Polygon2D.ContainsConvex(spaces[j], probe, 0f)) return false;
                // Same wall line also produced by an earlier polygon on the same side: keep one copy.
                if (j < self && Polygon2D.ContainsConvex(spaces[j], mid)) return false;
            }
            return true;
        }

        /// <summary>Joins consecutive pieces that continue in a straight line, reducing mesh count.</summary>
        private static List<WallSegment> MergeCollinear(List<WallSegment> walls)
        {
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < walls.Count && !merged; i++)
                {
                    for (int j = 0; j < walls.Count && !merged; j++)
                    {
                        if (i == j) continue;
                        WallSegment w1 = walls[i], w2 = walls[j];
                        if (Vector2.DistanceSquared(w1.B, w2.A) > 1e-8f) continue;
                        if (Vector2.Dot(w1.Inward, w2.Inward) < 0.9999f) continue;
                        Vector2 d1 = Vector2.Normalize(w1.B - w1.A), d2 = Vector2.Normalize(w2.B - w2.A);
                        if (Vector2.Dot(d1, d2) < 0.9999f) continue;
                        walls[i] = new WallSegment { A = w1.A, B = w2.B, Inward = w1.Inward };
                        walls.RemoveAt(j);
                        merged = true;
                    }
                }
            }
            return walls;
        }

        /// <summary>Cuts each opening out of the nearest wall. Unplaceable openings are reported, not dropped silently.</summary>
        public static List<OpeningPlacement> CutOpenings(List<WallSegment> walls, IEnumerable<OpeningRequest> openings)
        {
            var placements = new List<OpeningPlacement>();
            foreach (OpeningRequest req in openings)
            {
                var placement = new OpeningPlacement { Tag = req.Tag, SnapDistance = float.MaxValue };
                int best = -1;
                float bestT = 0f;
                for (int i = 0; i < walls.Count; i++)
                {
                    float t = Polygon2D.ClosestParameter(walls[i].A, walls[i].B, req.Center);
                    float d = Vector2.Distance(Vector2.Lerp(walls[i].A, walls[i].B, t), req.Center);
                    if (d < placement.SnapDistance) { placement.SnapDistance = d; best = i; bestT = t; }
                }

                if (best >= 0 && placement.SnapDistance <= OpeningSnapTolerance)
                {
                    WallSegment w = walls[best];
                    float len = w.Length;
                    float half = req.Width * 0.5f / len;
                    float t0 = System.Math.Max(0f, bestT - half), t1 = System.Math.Min(1f, bestT + half);
                    placement.Placed = true;
                    placement.A = Vector2.Lerp(w.A, w.B, t0);
                    placement.B = Vector2.Lerp(w.A, w.B, t1);
                    placement.Inward = w.Inward;

                    walls.RemoveAt(best);
                    if (t0 * len > MinSegment) walls.Add(new WallSegment { A = w.A, B = placement.A, Inward = w.Inward });
                    if ((1f - t1) * len > MinSegment) walls.Add(new WallSegment { A = placement.B, B = w.B, Inward = w.Inward });
                }
                placements.Add(placement);
            }
            return placements;
        }
    }
}
