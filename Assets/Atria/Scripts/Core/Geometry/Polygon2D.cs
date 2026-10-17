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
        /// Six convex trapezoids forming a hexagonal corridor ring around a central void.
        /// Radii are centre-to-corner; the corridor's clear width is (outer - inner) * cos 30°.
        /// <paramref name="rotationDegrees"/> is the yaw (0 = +Z, 90 = +X) of the first corner.
        /// </summary>
        public static List<List<Vector2>> HexRing(Vector2 center, float outerRadius, float innerRadius, float rotationDegrees)
        {
            if (!(innerRadius > 0f && outerRadius > innerRadius))
                throw new FormatException($"hexRing needs 0 < innerRadius < outerRadius (got {innerRadius}, {outerRadius})");
            var pieces = new List<List<Vector2>>();
            for (int k = 0; k < 6; k++)
            {
                double a0 = (rotationDegrees + 60.0 * k) * Math.PI / 180.0, a1 = a0 + Math.PI / 3.0;
                var d0 = new Vector2((float)Math.Sin(a0), (float)Math.Cos(a0));
                var d1 = new Vector2((float)Math.Sin(a1), (float)Math.Cos(a1));
                pieces.Add(new List<Vector2> { center + d0 * outerRadius, center + d1 * outerRadius, center + d1 * innerRadius, center + d0 * innerRadius });
            }
            return pieces;
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
