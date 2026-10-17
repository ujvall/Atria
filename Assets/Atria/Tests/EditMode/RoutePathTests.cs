using System;
using System.Collections.Generic;
using System.Numerics;
using Atria.Core.Data;
using Atria.Core.Geometry;
using Atria.Core.Navigation;
using NUnit.Framework;

namespace Atria.Core.Tests
{
    public class RoutePathTests
    {
        private static readonly RoutePathSettings Settings = new RoutePathSettings();

        private static float AngleDelta(float a, float b)
        {
            float d = Math.Abs(a - b) % 360f;
            return d > 180f ? 360f - d : d;
        }

        [Test]
        public void StraightPath_PositionIsDistanceProportional()
        {
            var path = RoutePath.Build(new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(0, 0, 40) }, null, null, Settings);
            Assert.AreEqual(40f, path.Length, 1e-4f);
            // Progress is by distance, not by waypoint: 25% is 10 m even though it is the first waypoint.
            Assert.AreEqual(10f, path.PositionAtProgress(0.25f).Z, 1e-4f);
            Assert.AreEqual(20f, path.PositionAtProgress(0.5f).Z, 1e-4f);
            Assert.AreEqual(0f, path.YawAtProgress(0.5f), 1e-3f);
        }

        [Test]
        public void Boundaries_AreClampedAndStable()
        {
            var path = RoutePath.Build(new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 10) }, null, 90f, Settings);
            Assert.AreEqual(Vector3.Zero, path.PositionAtProgress(-1f));
            Assert.AreEqual(new Vector3(0, 0, 10), path.PositionAtProgress(2f));
            Assert.AreEqual(Vector3.Zero, path.PositionAtProgress(float.NaN));
            // Ends facing the requested direction (e.g. the door).
            Assert.AreEqual(90f, path.YawAtProgress(1f), 1e-3f);
            Assert.AreEqual(0f, path.YawAtProgress(0f), 1e-3f);
        }

        [Test]
        public void ForwardAndBackwardScrubbing_GiveIdenticalPoses()
        {
            var path = RoutePath.Build(new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 10), new Vector3(10, 0, 20) }, null, null, Settings);
            var forward = new List<(Vector3, float)>();
            for (int i = 0; i <= 100; i++) forward.Add((path.PositionAtProgress(i / 100f), path.YawAtProgress(i / 100f)));
            for (int i = 100; i >= 0; i--)
            {
                Assert.AreEqual(forward[i].Item1, path.PositionAtProgress(i / 100f));
                Assert.AreEqual(forward[i].Item2, path.YawAtProgress(i / 100f));
            }
        }

        [Test]
        public void TurningPath_OrientationChangesSmoothlyWithoutFlips()
        {
            // North, then east, then north: two 90 degree turns.
            var path = RoutePath.Build(new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 10), new Vector3(10, 0, 20) }, null, null, Settings);
            float prevYaw = path.YawAt(0f);
            Vector3 prevPos = path.PositionAt(0f);
            const int steps = 2000;
            for (int i = 1; i <= steps; i++)
            {
                float d = path.Length * i / steps;
                float yaw = path.YawAt(d);
                Vector3 pos = path.PositionAt(d);
                Assert.Less(AngleDelta(yaw, prevYaw), 2f, $"yaw jump at {d:0.00} m");
                Assert.Less(Vector3.Distance(pos, prevPos), path.Length / steps + 1e-3f, $"position jump at {d:0.00} m");
                prevYaw = yaw;
                prevPos = pos;
            }
            Assert.AreEqual(0f, path.YawAt(2f), 1e-3f);      // heading north
            Assert.AreEqual(90f, path.YawAt(15f), 1e-3f);    // heading east mid-corridor
            Assert.AreEqual(0f, path.YawAt(path.Length), 1e-3f);
        }

        [Test]
        public void CornerCut_StaysInsideCorridorWidth()
        {
            // 2.4 m corridors meeting at (0,10). Every camera position must lie within the union of
            // the two corridor rectangles.
            var widths = new[] { 2.4f, 2.4f };
            var path = RoutePath.Build(new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 10) }, widths, null, Settings);
            var ns = new List<Vector2> { new Vector2(-1.2f, -1f), new Vector2(1.2f, -1f), new Vector2(1.2f, 11.2f), new Vector2(-1.2f, 11.2f) };
            var ew = new List<Vector2> { new Vector2(-1.2f, 8.8f), new Vector2(11f, 8.8f), new Vector2(11f, 11.2f), new Vector2(-1.2f, 11.2f) };
            for (int i = 0; i <= 500; i++)
            {
                Vector3 p = path.PositionAtProgress(i / 500f);
                var plan = new Vector2(p.X, p.Z);
                Assert.IsTrue(Polygon2D.ContainsConvex(ns, plan) || Polygon2D.ContainsConvex(ew, plan), $"outside corridor at {plan}");
            }
            // The corner was actually rounded (path is shorter than the 20 m polyline).
            Assert.Less(path.Length, 20f);
            Assert.Greater(path.Length, 19f);
        }

        [Test]
        public void SingleNodeRoute_DoesNotCrash()
        {
            var path = RoutePath.Build(new[] { new Vector3(3, 0, 4) }, null, 180f, Settings);
            Assert.AreEqual(0f, path.Length);
            Assert.AreEqual(new Vector3(3, 0, 4), path.PositionAtProgress(0.7f));
            Assert.AreEqual(180f, path.YawAtProgress(0.7f), 1e-3f);
        }

        [Test]
        public void FromRoute_EndsFacingTheDoor()
        {
            RouteResult r = new RouteFinder(TestData.Plus()).FindRoute("SOUTH", "R-N");
            RoutePath path = RoutePath.FromRoute(r, Settings);
            // Door of R-N is on the east wall (x = 1.2) beside the access node at (0, 20).
            Assert.AreEqual(90f, path.YawAtProgress(1f), 1e-3f);
            Assert.AreEqual(0f, path.YawAtProgress(0f), 1e-3f);
        }

        [Test]
        public void FromRoute_ShippedSampleCameraStaysInWalkableSpace()
        {
            BuildingData b = TestData.LoadShippedSample();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            var finder = new RouteFinder(b);
            foreach (EntranceData e in b.Entrances)
            {
                foreach (RoomData room in b.Rooms)
                {
                    RoutePath path = RoutePath.FromRoute(finder.FindRoute(e.Id, room.Id), Settings);
                    for (int i = 0; i <= 400; i++)
                    {
                        Vector3 p = path.PositionAtProgress(i / 400f);
                        Assert.IsTrue(layout.IsWalkable(new Vector2(p.X, p.Z)), $"{e.Id}->{room.Id} leaves corridor at {p}");
                    }
                }
            }
        }

        [Test]
        public void RemainingDistanceAndEta_UpdateWithProgress()
        {
            Assert.AreEqual(73f, RouteProgress.RemainingDistance(73f, 0f), 1e-4f);
            Assert.AreEqual(36.5f, RouteProgress.RemainingDistance(73f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, RouteProgress.RemainingDistance(73f, 1f), 1e-4f);
            Assert.AreEqual(0f, RouteProgress.RemainingDistance(73f, 1.5f), 1e-4f);
            Assert.AreEqual(73f, RouteProgress.RemainingDistance(73f, -0.5f), 1e-4f);
            Assert.AreEqual(30f, RouteProgress.RemainingSeconds(72f, 0.5f, 1.2f), 1e-3f);
            Assert.AreEqual(0f, RouteProgress.RemainingSeconds(72f, 0.5f, 0f));

            float previous = float.MaxValue;
            for (int i = 0; i <= 100; i++)
            {
                float remaining = RouteProgress.RemainingDistance(73f, i / 100f);
                Assert.LessOrEqual(remaining, previous);
                previous = remaining;
            }
        }

        [Test]
        public void DurationFormatting()
        {
            Assert.AreEqual("0 s", RouteProgress.FormatDuration(0f));
            Assert.AreEqual("45 s", RouteProgress.FormatDuration(44.2f));
            Assert.AreEqual("1 min", RouteProgress.FormatDuration(60f));
            Assert.AreEqual("1 min 1 s", RouteProgress.FormatDuration(60.5f));
            Assert.AreEqual("73 m", RouteProgress.FormatDistance(73.2f));
        }
    }
}
