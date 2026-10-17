using System;
using System.Collections.Generic;
using System.Numerics;
using Atria.Core.Data;

namespace Atria.Core.Navigation
{
    public sealed class RoutePathSettings
    {
        /// <summary>Upper bound on how far before a corner the camera starts cutting it (metres).</summary>
        public float MaxCornerCut = 1.2f;
        /// <summary>Corridor width assumed for edges without an explicit width.</summary>
        public float DefaultWidth = 2.4f;
        /// <summary>How far ahead along the path the camera looks; larger = earlier, gentler turns.</summary>
        public float LookAhead = 2.5f;
        /// <summary>Polyline samples used to approximate each rounded corner.</summary>
        public int CornerSamples = 10;
    }

    /// <summary>
    /// The camera track for a route: the graph route's node positions with corners rounded by a
    /// quadratic Bezier, parameterised by walked distance so long corridors take up more of the
    /// scrubber than short turns.
    ///
    /// Wall safety: each corner cut starts at most min(MaxCornerCut, width/2, 45% of either
    /// adjacent edge) before the corner. A Bezier stays inside the triangle formed by its control
    /// points, and with that bound the triangle lies within the two corridors' clear width, so the
    /// camera never leaves corridors that the edges themselves are validated to lie within.
    /// </summary>
    public sealed class RoutePath
    {
        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly List<float> _cumulative = new List<float>();
        private Vector2 _endDirection;
        private float _lookAhead;

        public IReadOnlyList<Vector3> Points => _points;
        public float Length { get; private set; }

        private RoutePath() { }

        /// <summary>Builds the camera track for a successful route; the camera finishes facing the room's door.</summary>
        public static RoutePath FromRoute(RouteResult route, RoutePathSettings settings)
        {
            if (route == null || !route.Success) throw new ArgumentException("Route must be successful", nameof(route));
            var points = new List<Vector3>();
            var widths = new List<float>();
            foreach (NodeData n in route.Nodes) points.Add(n.Position);
            foreach (EdgeData e in route.Edges) widths.Add(e.Width ?? settings.DefaultWidth);

            float? endYaw = null;
            if (route.Room?.Door != null && points.Count > 0)
            {
                Vector3 end = points[points.Count - 1];
                var toDoor = new Vector2(route.Room.Door.Center.X - end.X, route.Room.Door.Center.Y - end.Z);
                if (toDoor.LengthSquared() > 1e-6f) endYaw = YawOf(toDoor);
            }
            return Build(points, widths, endYaw, settings);
        }

        /// <param name="nodes">Route node positions in order.</param>
        /// <param name="edgeWidths">Clear width per edge (nodes.Count - 1 entries), or null for the default.</param>
        /// <param name="endYawDegrees">Direction to face at the end of the route (e.g. towards the door).</param>
        public static RoutePath Build(IList<Vector3> nodes, IList<float> edgeWidths, float? endYawDegrees, RoutePathSettings settings)
        {
            if (nodes == null || nodes.Count == 0) throw new ArgumentException("A route path needs at least one point", nameof(nodes));
            settings = settings ?? new RoutePathSettings();
            var path = new RoutePath { _lookAhead = Math.Max(0.1f, settings.LookAhead) };

            var clean = new List<Vector3> { nodes[0] };
            var widths = new List<float>();
            for (int i = 1; i < nodes.Count; i++)
            {
                if (Vector3.DistanceSquared(nodes[i], clean[clean.Count - 1]) < 1e-8f) continue;
                clean.Add(nodes[i]);
                widths.Add(edgeWidths != null && i - 1 < edgeWidths.Count ? edgeWidths[i - 1] : settings.DefaultWidth);
            }

            path._points.Add(clean[0]);
            for (int i = 1; i < clean.Count - 1; i++)
            {
                Vector3 prev = clean[i - 1], corner = clean[i], next = clean[i + 1];
                Vector3 inDir = Vector3.Normalize(corner - prev), outDir = Vector3.Normalize(next - corner);
                float turnCos = Vector3.Dot(inDir, outDir);
                if (turnCos > 0.9995f)
                {
                    path._points.Add(corner); // effectively straight
                    continue;
                }
                float cut = Math.Min(settings.MaxCornerCut, 0.5f * Math.Min(widths[i - 1], widths[i]));
                cut = Math.Min(cut, 0.45f * Math.Min(Vector3.Distance(prev, corner), Vector3.Distance(corner, next)));
                Vector3 p0 = corner - inDir * cut, p2 = corner + outDir * cut;
                int samples = Math.Max(2, settings.CornerSamples);
                for (int k = 0; k <= samples; k++)
                {
                    float t = (float)k / samples;
                    float u = 1f - t;
                    path._points.Add(u * u * p0 + 2f * u * t * corner + t * t * p2);
                }
            }
            if (clean.Count > 1) path._points.Add(clean[clean.Count - 1]);

            path._cumulative.Add(0f);
            for (int i = 1; i < path._points.Count; i++)
                path._cumulative.Add(path._cumulative[i - 1] + Vector3.Distance(path._points[i - 1], path._points[i]));
            path.Length = path._cumulative[path._cumulative.Count - 1];

            if (endYawDegrees.HasValue) path._endDirection = DirectionOf(endYawDegrees.Value);
            else
            {
                Vector2 last = path.HorizontalTangent(path.Length);
                path._endDirection = last.LengthSquared() > 0f ? last : new Vector2(0f, 1f);
            }
            return path;
        }

        public Vector3 PositionAtProgress(float progress) => PositionAt(Clamp01(progress) * Length);

        public float YawAtProgress(float progress) => YawAt(Clamp01(progress) * Length);

        public Vector3 PositionAt(float distance)
        {
            if (_points.Count == 1 || distance <= 0f) return _points[0];
            if (distance >= Length) return _points[_points.Count - 1];
            int i = SegmentIndex(distance);
            float segLen = _cumulative[i + 1] - _cumulative[i];
            float t = segLen > 0f ? (distance - _cumulative[i]) / segLen : 0f;
            return Vector3.Lerp(_points[i], _points[i + 1], t);
        }

        /// <summary>
        /// Viewing direction in degrees (0 = +Z, 90 = +X). The camera looks at the point
        /// <see cref="RoutePathSettings.LookAhead"/> metres further along the track; past the end
        /// the track is extended in the final facing direction, so orientation changes gradually
        /// through corners and settles on the door at the end with no discontinuities.
        /// </summary>
        public float YawAt(float distance)
        {
            distance = Math.Max(0f, Math.Min(Length, distance));
            Vector3 here = PositionAt(distance);
            float ahead = distance + _lookAhead;
            Vector3 target = ahead <= Length
                ? PositionAt(ahead)
                : _points[_points.Count - 1] + new Vector3(_endDirection.X, 0f, _endDirection.Y) * (ahead - Length);
            var chord = new Vector2(target.X - here.X, target.Z - here.Z);
            if (chord.LengthSquared() < 1e-6f) chord = HorizontalTangent(distance);
            if (chord.LengthSquared() < 1e-6f) chord = _endDirection;
            return YawOf(chord);
        }

        private Vector2 HorizontalTangent(float distance)
        {
            if (_points.Count < 2) return Vector2.Zero;
            int i = SegmentIndex(Math.Max(0f, Math.Min(Length, distance)));
            // Walk forward past vertical-only segments (e.g. a lift).
            for (int k = i; k < _points.Count - 1; k++)
            {
                var d = new Vector2(_points[k + 1].X - _points[k].X, _points[k + 1].Z - _points[k].Z);
                if (d.LengthSquared() > 1e-8f) return Vector2.Normalize(d);
            }
            return Vector2.Zero;
        }

        private int SegmentIndex(float distance)
        {
            int lo = 0, hi = _cumulative.Count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_cumulative[mid] <= distance) lo = mid; else hi = mid - 1;
            }
            return Math.Max(0, lo);
        }

        public static float YawOf(Vector2 planDirection)
        {
            double deg = Math.Atan2(planDirection.X, planDirection.Y) * 180.0 / Math.PI;
            return (float)((deg + 360.0) % 360.0);
        }

        public static Vector2 DirectionOf(float yawDegrees)
        {
            double r = yawDegrees * Math.PI / 180.0;
            return new Vector2((float)Math.Sin(r), (float)Math.Cos(r));
        }

        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : (float.IsNaN(v) ? 0f : v));
    }

    /// <summary>Progress read-outs for the scrubber. Distances use the router's (possibly measured) route length.</summary>
    public static class RouteProgress
    {
        public static float RemainingDistance(float routeDistance, float progress) =>
            Math.Max(0f, routeDistance * (1f - RoutePath.Clamp01(progress)));

        public static float RemainingSeconds(float routeDistance, float progress, float walkingSpeedMps) =>
            walkingSpeedMps > 0f ? RemainingDistance(routeDistance, progress) / walkingSpeedMps : 0f;

        /// <summary>"45 s", "2 min", "2 min 30 s" style text for walking times.</summary>
        public static string FormatDuration(float seconds)
        {
            int s = (int)Math.Ceiling(Math.Max(0f, seconds));
            if (s < 60) return s + " s";
            int m = s / 60, rem = s % 60;
            return rem == 0 ? m + " min" : m + " min " + rem + " s";
        }

        public static string FormatDistance(float metres) =>
            metres < 1000f ? Math.Round(metres) + " m" : (metres / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " km";
    }
}
