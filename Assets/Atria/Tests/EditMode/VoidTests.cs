using System;
using System.Linq;
using System.Numerics;
using Atria.Core.Data;
using Atria.Core.Geometry;
using Atria.Core.Navigation;
using NUnit.Framework;

namespace Atria.Core.Tests
{
    /// <summary>Hex-ring centres are open floor voids: never walkable, never routed through, drawn as rails not walls.</summary>
    public class VoidTests
    {
        private const string RingJson = @"{
  ""schemaVersion"": 1, ""buildingId"": ""VOID-TEST"", ""dataStatus"": ""partial"",
  ""floors"": [ { ""id"": ""G"", ""elevation"": 0 } ],
  ""spaces"": [ { ""id"": ""RING"", ""floor"": ""G"", ""shape"": ""hexRing"", ""center"": [0, 0], ""outerRadius"": 5, ""innerRadius"": 2.2, ""rotation"": 0, ""provenance"": ""estimated"" } ],
  ""nodes"": [ { ""id"": ""A"", ""floor"": ""G"", ""x"": 0, ""z"": 3.6, ""provenance"": ""estimated"" } ],
  ""entrances"": [ { ""id"": ""S"", ""node"": ""A"" } ]
}";

        private static float Area(System.Collections.Generic.IList<Vector2> p) => Math.Abs(Polygon2D.SignedArea(p));

        [Test]
        public void HexRing_RecordsARealInnerOpening()
        {
            BuildingData b = BuildingDataLoader.FromJson(RingJson);
            VoidData hole = b.Voids.Single();
            Assert.AreEqual("RING#void", hole.Id);
            Assert.AreEqual(6, hole.Polygon.Count);
            Assert.IsTrue(Polygon2D.IsConvex(hole.Polygon));
            Assert.AreEqual(1.5f * (float)Math.Sqrt(3) * 2.2f * 2.2f, Area(hole.Polygon), 1e-3f); // regular hexagon, r = 2.2
            Assert.AreEqual(Provenance.Estimated, hole.Provenance);
        }

        [Test]
        public void WalkableArea_DoesNotFillTheVoid()
        {
            BuildingData b = BuildingDataLoader.FromJson(RingJson);
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            // Ring area = outer hexagon minus inner hexagon; no walkable piece covers the centre.
            float ring = b.Spaces.Sum(s => Area(s.Polygon));
            Assert.AreEqual(1.5f * (float)Math.Sqrt(3) * (25f - 2.2f * 2.2f), ring, 1e-2f);
            foreach (Vector2 p in new[] { Vector2.Zero, new Vector2(1f, 1f), new Vector2(-1.5f, 0f) })
            {
                Assert.IsFalse(layout.IsWalkable(p), $"{p} is in the void but walkable");
                Assert.IsTrue(layout.IsInVoid(p), $"{p} should be in the void");
            }
            Assert.IsFalse(layout.IsInVoid(new Vector2(0f, 3.6f))); // ring corridor
        }

        [Test]
        public void OnlyInnerEdgesBecomeRails()
        {
            BuildingData b = BuildingDataLoader.FromJson(RingJson);
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            Assert.AreEqual(6, layout.Walls.Count(w => w.BordersVoid));
            Assert.AreEqual(6 * 2.2f, layout.Walls.Where(w => w.BordersVoid).Sum(w => w.Length), 1e-3f);
            Assert.AreEqual(6 * 5f, layout.Walls.Where(w => !w.BordersVoid).Sum(w => w.Length), 1e-3f);
        }

        [Test]
        public void InnerEdgeWall_IsConfigurableAndMakesNoVoid()
        {
            BuildingData b = BuildingDataLoader.FromJson(RingJson.Replace("\"rotation\": 0,", "\"rotation\": 0, \"innerEdge\": \"wall\","));
            Assert.IsEmpty(b.Voids);
            Assert.IsFalse(FloorLayout.Build(b, b.Floors[0]).Walls.Any(w => w.BordersVoid));
            Assert.Throws<FormatException>(() => BuildingDataLoader.FromJson(RingJson.Replace("\"rotation\": 0,", "\"rotation\": 0, \"innerEdge\": \"glass\",")));
        }

        [Test]
        public void Validator_RejectsNodesEdgesAndDoorsInTheVoid()
        {
            string json = RingJson.Replace(
                @"{ ""id"": ""A"", ""floor"": ""G"", ""x"": 0, ""z"": 3.6, ""provenance"": ""estimated"" } ],",
                @"{ ""id"": ""A"", ""floor"": ""G"", ""x"": 0, ""z"": 3.6, ""provenance"": ""estimated"" },
                   { ""id"": ""B"", ""floor"": ""G"", ""x"": 0, ""z"": -3.6, ""provenance"": ""estimated"" },
                   { ""id"": ""MID"", ""floor"": ""G"", ""x"": 0, ""z"": 0, ""provenance"": ""estimated"" } ],
  ""edges"": [ { ""from"": ""A"", ""to"": ""B"" } ],
  ""rooms"": [ { ""id"": ""X"", ""number"": ""X"", ""floor"": ""G"", ""doors"": [ { ""accessNode"": ""A"", ""x"": 0, ""z"": 2.2 } ] } ],");
            ValidationReport report = BuildingValidator.Validate(BuildingDataLoader.FromJson(json));
            Assert.IsTrue(report.Errors.Any(e => e.Contains("'MID' is outside every walkable space")), report.ToString());
            Assert.IsTrue(report.Errors.Any(e => e.Contains("'A'-'B'") && e.Contains("leaves walkable space")), report.ToString());
            Assert.IsTrue(report.Errors.Any(e => e.Contains("opens onto a floor void")), report.ToString());
        }

        [Test]
        public void PrpDraft_RingsHaveVoidsAndRoutesNeverCrossThem()
        {
            BuildingData b = BuildingDataLoader.FromJson(System.IO.File.ReadAllText(TestData.FindRepoFile(PrpDraftTests.DraftRelativePath)));
            CollectionAssert.AreEquivalent(new[] { "L1#void", "L2#void" }, b.Voids.Select(v => v.Id).ToArray());
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            Assert.AreEqual(12, layout.Walls.Count(w => w.BordersVoid));
            Assert.IsFalse(b.Nodes.Any(n => layout.IsInVoid(new Vector2(n.Position.X, n.Position.Z))));

            var finder = new RouteFinder(b);
            foreach (RoomData room in b.Rooms.Where(r => r.Doors.Count > 0))
            {
                RouteResult route = finder.FindRoute("DRAFT_START_HALL", room.Id);
                Assert.IsTrue(route.Success, room.Id);
                RoutePath path = RoutePath.FromRoute(route, new RoutePathSettings());
                for (int i = 0; i <= 600; i++)
                {
                    System.Numerics.Vector3 p = path.PositionAtProgress(i / 600f);
                    Assert.IsFalse(layout.IsInVoid(new Vector2(p.X, p.Z)), $"{room.Id}: camera inside a void at {p}");
                }
                // Distance/ETA come from the graph route; the camera track only rounds corners, so it is slightly shorter.
                Assert.LessOrEqual(path.Length, route.DistanceMetres + 1e-3f, room.Id);
                Assert.GreaterOrEqual(path.Length, route.DistanceMetres * 0.95f, room.Id);
            }
        }

        [Test]
        public void Demo_HasNoVoidsAndNoRails()
        {
            BuildingData b = TestData.LoadShippedSample();
            Assert.IsEmpty(b.Voids);
            Assert.IsFalse(FloorLayout.Build(b, b.Floors[0]).Walls.Any(w => w.BordersVoid));
        }
    }
}
