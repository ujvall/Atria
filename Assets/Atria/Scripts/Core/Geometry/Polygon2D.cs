using System;
using System.Collections.Generic;
using System.Numerics;

namespace Atria.Core.Geometry
{
    /// <summary>Small plan-view (x, z) polygon helpers. Vector2.Y holds the plan Z coordinate.</summary>
    public static class Polygon2D
    {
        public const float Epsilon = 1e-4f;

        public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        public static float SignedArea(IList<Vector2> poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Count; i++)
                area += Cross(poly[i], poly[(i + 1) % poly.Count]);
            return area * 0.5f;
        }

        /// <summary>
        /// Convex pieces forming a corridor ring between an outer regular hexagon and an inner
        /// regular polygon (the central void). Radii are centre-to-corner; rotations are the yaw
        /// (0 = +Z, 90 = +X) of the first corner. With a hexagonal inner outline this is six
        /// trapezoids. Otherwise the ring is cut along rays through every corner of both outlines,
        /// which keeps each piece convex.
        /// </summary>
        public static List<List<Vector2>> HexRing(Vector2 center, float outerRadius, float innerRadius, float rotationDegrees,
            int innerSides = 6, float? innerRotationDegrees = null)
        {
            if (!(innerRadius > 0f && outerRadius > innerRadius))
                throw new FormatException($"hexRing needs 0 < innerRadius < outerRadius (got {innerRadius}, {outerRadius})");
            if (innerSides < 3) throw new FormatException($"hexRing innerSides must be at least 3 (got {innerSides})");
            List<Vector2> outer = RegularPolygon(center, 6, outerRadius, rotationDegrees);
            List<Vector2> inner = RegularPolygon(center, innerSides, innerRadius, innerRotationDegrees ?? rotationDegrees);

            var angles = new List<double>();
            foreach (Vector2 v in outer) angles.Add(Yaw(v - center));
            foreach (Vector2 v in inner)
            {
                double a = Yaw(v - center);
                if (Vector2.Distance(RayHit(center, a, outer), center) <= innerRadius + Epsilon)
                    throw new FormatException("hexRing inner outline must fit inside the outer hexagon");
                angles.Add(a);
            }
            angles.Sort();
            var unique = new List<double>();
            foreach (double a in angles)
                if (unique.Count == 0 || a - unique[unique.Count - 1] > 1e-4) unique.Add(a);
            if (unique.Count > 1 && unique[0] + 360.0 - unique[unique.Count - 1] <= 1e-4) unique.RemoveAt(unique.Count - 1);

            var pieces = new List<List<Vector2>>();
            for (int i = 0; i < unique.Count; i++)
            {
                double a0 = unique[i], a1 = unique[(i + 1) % unique.Count];
                pieces.Add(new List<Vector2> { RayHit(center, a0, outer), RayHit(center, a1, outer), RayHit(center, a1, inner), RayHit(center, a0, inner) });
            }
            return pieces;
        }

        /// <summary>Corners of a regular polygon; the first corner is at yaw <paramref name="rotationDegrees"/>.</summary>
        public static List<Vector2> RegularPolygon(Vector2 center, int sides, float radius, float rotationDegrees)
        {
            var pts = new List<Vector2>();
            for (int k = 0; k < sides; k++)
            {
                double a = (rotationDegrees + 360.0 * k / sides) * Math.PI / 180.0;
                pts.Add(center + new Vector2((float)Math.Sin(a), (float)Math.Cos(a)) * radius);
            }
            return pts;
        }

        private static double Yaw(Vector2 d) => (Math.Atan2(d.X, d.Y) * 180.0 / Math.PI + 360.0) % 360.0;

        /// <summary>Where a ray from <paramref name="center"/> at the given yaw leaves a convex polygon containing it.</summary>
        private static Vector2 RayHit(Vector2 center, double yawDegrees, IList<Vector2> poly)
        {
            double r = yawDegrees * Math.PI / 180.0;
            var dir = new Vector2((float)Math.Sin(r), (float)Math.Cos(r));
            float best = float.MaxValue;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], e = poly[(i + 1) % poly.Count] - a;
                float denom = Cross(dir, e);
                if (Math.Abs(denom) < 1e-9f) continue;
                float t = Cross(a - center, e) / denom;   // distance along the ray
                float u = Cross(a - center, dir) / denom; // position along the edge
                if (t > 0f && u >= -1e-4f && u <= 1f + 1e-4f && t < best) best = t;
            }
            return center + dir * best;
        }

        public static void MakeCounterClockwise(List<Vector2> poly)
        {
            if (poly.Count >= 3 && SignedArea(poly) < 0f) poly.Reverse();
        }

        public static bool IsConvex(IList<Vector2> poly)
        {
            if (poly.Count < 3) return false;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count], c = poly[(i + 2) % poly.Count];
                if (Cross(b - a, c - b) < -Epsilon) return false;
            }
            return true;
        }

        /// <summary>Point-in-convex-polygon test (CCW polygon). Points within <paramref name="tolerance"/> of the boundary count as inside.</summary>
        public static bool ContainsConvex(IList<Vector2> poly, Vector2 p, float tolerance = Epsilon)
        {
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                Vector2 edge = b - a;
                float len = edge.Length();
                if (len < Epsilon) continue;
                // Signed distance to the edge line; negative means outside for a CCW polygon.
                if (Cross(edge, p - a) / len < -tolerance) return false;
            }
            return true;
        }

        /// <summary>
        /// Parameters t in [0,1] along segment a-b where it meets segment c-d. Collinear overlaps
        /// contribute the projected endpoints of c-d.
        /// </summary>
        public static void SegmentSplitParameters(Vector2 a, Vector2 b, Vector2 c, Vector2 d, List<float> output)
        {
            Vector2 r = b - a, s = d - c;
            float denom = Cross(r, s);
            float rr = Vector2.Dot(r, r);
            if (rr < Epsilon * Epsilon) return;

            if (Math.Abs(denom) < Epsilon * Math.Sqrt(rr))
            {
                // Parallel: only matters if collinear.
                float dist = Math.Abs(Cross(r, c - a)) / (float)Math.Sqrt(rr);
                if (dist > Epsilon * 10f) return;
                AddIfInside(Vector2.Dot(c - a, r) / rr, output);
                AddIfInside(Vector2.Dot(d - a, r) / rr, output);
                return;
            }

            float t = Cross(c - a, s) / denom;
            float u = Cross(c - a, r) / denom;
            if (u >= -1e-5f && u <= 1f + 1e-5f) AddIfInside(t, output);
        }

        private static void AddIfInside(float t, List<float> output)
        {
            if (t > 1e-5f && t < 1f - 1e-5f) output.Add(t);
        }

        /// <summary>Closest point on segment a-b to p, as a parameter in [0,1].</summary>
        public static float ClosestParameter(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len2 = Vector2.Dot(ab, ab);
            if (len2 < Epsilon * Epsilon) return 0f;
            return Math.Max(0f, Math.Min(1f, Vector2.Dot(p - a, ab) / len2));
        }
    }
}
