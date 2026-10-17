using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Atria.Core.Data;
using Atria.Core.Geometry;
using Atria.Core.Json;
using Atria.Core.Search;
using NUnit.Framework;

namespace Atria.Core.Tests
{
    public class DataAndSearchTests
    {
        [Test]
        public void Json_ParsesNestedValues()
        {
            var o = (Dictionary<string, object>)MiniJson.Parse("{\"a\": [1, -2.5e1, true, null, \"x\\u0041\\n\"], \"b\": {}}");
            var a = (List<object>)o["a"];
            Assert.AreEqual(1.0, a[0]);
            Assert.AreEqual(-25.0, a[1]);
            Assert.AreEqual(true, a[2]);
            Assert.IsNull(a[3]);
            Assert.AreEqual("xA\n", a[4]);
            Assert.IsInstanceOf<Dictionary<string, object>>(o["b"]);
        }

        [Test]
        public void Json_RejectsMalformedInput()
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\": 1,}"));
            Assert.Throws<FormatException>(() => MiniJson.Parse("[1 2]"));
            Assert.Throws<FormatException>(() => MiniJson.Parse("{} extra"));
        }

        [Test]
        public void Loader_RejectsUnsupportedSchema()
        {
            Assert.Throws<FormatException>(() => BuildingDataLoader.FromJson("{\"schemaVersion\": 99}"));
        }

        [Test]
        public void Loader_MissingProvenanceIsPlaceholder()
        {
            BuildingData b = TestData.Plus();
            Assert.IsTrue(b.Nodes.All(n => n.Provenance == Provenance.Placeholder));
            Assert.IsFalse(b.IsVerified);
        }

        [Test]
        public void ShippedSample_IsValidAndClearlyMarkedAsSample()
        {
            BuildingData b = TestData.LoadShippedSample();
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.IsEmpty(report.Warnings, report.ToString());
            Assert.AreEqual("sample", b.DataStatus);
            Assert.IsFalse(b.IsVerified);
            Assert.AreEqual(3, b.Entrances.Count);
            Assert.IsTrue(b.Rooms.All(r => r.Provenance == Provenance.Placeholder));
        }

        [Test]
        public void Validator_RejectsEdgeThroughWall()
        {
            BuildingData b = TestData.Plus();
            // Diagonal shortcut from S to E cuts across solid corners: must be rejected.
            b.Edges.Add(new EdgeData { From = "S", To = "E" });
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(report.Errors.Any(e => e.Contains("'S'-'E'") && e.Contains("leaves walkable space")), report.ToString());
        }

        [Test]
        public void Validator_ReportsBrokenReferencesAndUnplacedDoors()
        {
            BuildingData b = TestData.Plus();
            b.Edges.Add(new EdgeData { From = "X", To = "GHOST" });
            b.FindRoom("R-N").Door.Center = new Vector2(5f, 5f); // not on any wall
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.Errors.Any(e => e.Contains("GHOST")), report.ToString());
            Assert.IsTrue(report.Errors.Any(e => e.Contains("R-N") && e.Contains("door")), report.ToString());
        }

        [Test]
        public void Validator_WarnsAboutUnreachableRooms()
        {
            ValidationReport report = BuildingValidator.Validate(TestData.Plus());
            Assert.IsTrue(report.Warnings.Any(w => w.Contains("R-ISO") && w.Contains("not reachable")), report.ToString());
        }

        [Test]
        public void Walls_FollowUnionOutlineOfPlusShape()
        {
            BuildingData b = TestData.Plus();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            // Plus outline = 12 sides; the island adds 4. One door gap splits one wall into two.
            Assert.AreEqual(12 + 4 + 1, layout.Walls.Count);
            // No wall may cross the junction at the origin.
            foreach (WallSegment w in layout.Walls)
            {
                float t = Polygon2D.ClosestParameter(w.A, w.B, Vector2.Zero);
                Assert.Greater(Vector2.Distance(Vector2.Lerp(w.A, w.B, t), Vector2.Zero), 1.6f);
            }
            float perimeter = layout.Walls.Sum(w => w.Length);
            float expected = 8 * (21f - 1.2f) + 4 * 2.4f + 16f - 1.0f; // plus arms + ends + island - door gap
            Assert.AreEqual(expected, perimeter, 1e-3f);
            Assert.IsTrue(layout.Doors["R-N"].Placed);
            Assert.AreEqual(-1f, layout.Doors["R-N"].Inward.X, 1e-4f); // door faces into the corridor (west)
        }

        [Test]
        public void Walls_CollinearSameSideEdgesAreKeptOnce()
        {
            // Two overlapping corridors sharing the same bottom and top wall lines.
            var a = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 2), new Vector2(0, 2) };
            var c = new List<Vector2> { new Vector2(5, 0), new Vector2(15, 0), new Vector2(15, 2), new Vector2(5, 2) };
            List<WallSegment> walls = WallGenerator.BuildOutline(new List<IList<Vector2>> { a, c });
            Assert.AreEqual(4, walls.Count);
            Assert.AreEqual(15f + 15f + 2f + 2f, walls.Sum(w => w.Length), 1e-3f);
        }

        [Test]
        public void Walls_SampleWestWallIsContinuousApartFromDoors()
        {
            BuildingData b = TestData.LoadShippedSample();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            // CORR_1's west wall runs x=-1.2 from z=0 to z=31.2 with two 1 m door gaps.
            float westWall = layout.Walls.Where(w => Math.Abs(w.A.X + 1.2f) < 1e-3f && Math.Abs(w.B.X + 1.2f) < 1e-3f).Sum(w => w.Length);
            Assert.AreEqual(31.2f - 2f, westWall, 1e-3f);
            Assert.AreEqual(b.Rooms.Count, layout.Doors.Values.Count(d => d.Placed));
            Assert.AreEqual(3, layout.Entrances.Values.Count(d => d.Placed));
        }

        [Test]
        public void Search_NormalisesAndRanks()
        {
            var search = new RoomSearch(TestData.LoadShippedSample().Rooms);
            Assert.AreEqual("DEMO-122", search.FindExact("demo 122").Id);
            Assert.AreEqual("DEMO-122", search.FindExact("d122").Id);
            Assert.AreEqual("DEMO-122", search.Search("DEMO122")[0].Room.Id);
            List<RoomMatch> prefix = search.Search("demo-12");
            CollectionAssert.AreEquivalent(new[] { "DEMO-121", "DEMO-122" }, prefix.Select(m => m.Room.Id).ToArray());
            Assert.AreEqual(9, search.Search("").Count);
        }

        [Test]
        public void Search_UnknownRoomReturnsNothing()
        {
            var search = new RoomSearch(TestData.LoadShippedSample().Rooms);
            Assert.IsNull(search.FindExact("PRP 999"));
            Assert.IsEmpty(search.Search("zzz"));
        }
    }
}
