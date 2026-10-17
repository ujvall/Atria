using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Atria.Core.Data;
using Atria.Core.Geometry;
using Atria.Core.Navigation;
using NUnit.Framework;

namespace Atria.Core.Tests
{
    /// <summary>Data features needed to map the real building: hex rings, multi-door rooms, provisional data.</summary>
    public class MappingTests
    {
        /// <summary>A hex junction (ring corridor round a void) with one spur, as in the PRP sketches.</summary>
        private const string HexJson = @"{
  ""schemaVersion"": 1, ""buildingId"": ""HEX-TEST"", ""dataStatus"": ""partial"",
  ""floors"": [ { ""id"": ""G"", ""elevation"": 0 } ],
  ""spaces"": [
    { ""id"": ""RING"", ""floor"": ""G"", ""shape"": ""hexRing"", ""center"": [0, 0], ""outerRadius"": 8, ""innerRadius"": 5, ""rotation"": 30, ""provenance"": ""estimated"" },
    { ""id"": ""SPUR"", ""floor"": ""G"", ""polygon"": [[-1.2, -20], [1.2, -20], [1.2, -6], [-1.2, -6]], ""provenance"": ""estimated"" }
  ],
  ""nodes"": [
    { ""id"": ""IN"",  ""floor"": ""G"", ""x"": 0, ""z"": -19, ""provenance"": ""estimated"" },
    { ""id"": ""S"",   ""floor"": ""G"", ""x"": 0, ""z"": -5.6, ""provenance"": ""estimated"" },
    { ""id"": ""SC"",  ""floor"": ""G"", ""x"": 3.25, ""z"": -5.63, ""provenance"": ""estimated"" },
    { ""id"": ""SWC"", ""floor"": ""G"", ""x"": -3.25, ""z"": -5.63, ""provenance"": ""estimated"" },
    { ""id"": ""SE"",  ""floor"": ""G"", ""x"": 4.85, ""z"": -2.8, ""provenance"": ""estimated"" },
    { ""id"": ""SW"",  ""floor"": ""G"", ""x"": -4.85, ""z"": -2.8, ""provenance"": ""estimated"" }
  ],
  ""edges"": [
    { ""from"": ""IN"", ""to"": ""S"", ""provenance"": ""estimated"" },
    { ""from"": ""S"",  ""to"": ""SC"", ""width"": 2.4, ""provenance"": ""estimated"" },
    { ""from"": ""SC"", ""to"": ""SE"", ""width"": 2.4, ""provenance"": ""estimated"" },
    { ""from"": ""S"",  ""to"": ""SWC"", ""width"": 2.4, ""provenance"": ""estimated"" },
    { ""from"": ""SWC"", ""to"": ""SW"", ""width"": 2.4, ""provenance"": ""estimated"" }
  ],
  ""entrances"": [ { ""id"": ""E1"", ""node"": ""IN"", ""provenance"": ""estimated"" } ],
  ""rooms"": [
    { ""id"": ""R1"", ""number"": ""R 1"", ""floor"": ""G"", ""provenance"": ""estimated"",
      ""doors"": [ { ""accessNode"": ""SE"" }, { ""accessNode"": ""SW"" } ] },
    { ""id"": ""R2"", ""number"": ""R 2"", ""floor"": ""G"", ""provenance"": ""estimated"" }
  ]
}";

        [Test]
        public void HexRing_IsSixConvexPiecesWithCorrectOutline()
        {
            List<List<Vector2>> pieces = Polygon2D.HexRing(Vector2.Zero, 8f, 5f, 30f);
            Assert.AreEqual(6, pieces.Count);
            foreach (List<Vector2> p in pieces)
            {
                Polygon2D.MakeCounterClockwise(p);
                Assert.IsTrue(Polygon2D.IsConvex(p));
            }
            // Union outline = outer hexagon + inner hexagon (side length equals circumradius).
            List<WallSegment> walls = WallGenerator.BuildOutline(pieces.Cast<IList<Vector2>>().ToList());
            Assert.AreEqual(6 * 8f + 6 * 5f, walls.Sum(w => w.Length), 1e-3f);
            Assert.AreEqual(12, walls.Count);
        }

        [Test]
        public void HexRing_RejectsBadRadii()
        {
            Assert.Throws<FormatException>(() => Polygon2D.HexRing(Vector2.Zero, 5f, 8f, 0f));
            Assert.Throws<FormatException>(() => BuildingDataLoader.FromJson(HexJson.Replace("\"innerRadius\": 5", "\"innerRadius\": 0")));
        }

        [Test]
        public void HexRing_LoadsAsSixSpacesAndValidates()
        {
            BuildingData b = BuildingDataLoader.FromJson(HexJson);
            Assert.AreEqual(6, b.Spaces.Count(s => s.Id.StartsWith("RING#")));
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            Assert.IsTrue(layout.IsWalkable(new Vector2(0f, -6.5f)));   // in the ring corridor
            Assert.IsFalse(layout.IsWalkable(Vector2.Zero));           // the central void
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.IsValid, report.ToString());

            // Joining two side midpoints directly cuts across the inner corner into the void: rejected.
            b.Edges.Add(new EdgeData { From = "S", To = "SE", Width = 2.4f });
            Assert.IsTrue(BuildingValidator.Validate(b).Errors.Any(e => e.Contains("'S'-'SE'") && e.Contains("leaves walkable space")));
        }

        [Test]
        public void CorridorShape_IsRotatedRectangle()
        {
            string json = HexJson.Replace(
                "{ \"id\": \"SPUR\", \"floor\": \"G\", \"polygon\": [[-1.2, -20], [1.2, -20], [1.2, -6], [-1.2, -6]]",
                "{ \"id\": \"SPUR\", \"floor\": \"G\", \"shape\": \"corridor\", \"from\": [0, -20], \"to\": [0, -6], \"width\": 2.4");
            BuildingData b = BuildingDataLoader.FromJson(json);
            SpaceData spur = b.FindSpace("SPUR");
            Assert.AreEqual(4, spur.Polygon.Count);
            Assert.AreEqual(2.4f * 14f, Math.Abs(Polygon2D.SignedArea(spur.Polygon)), 1e-3f);
            Assert.IsTrue(BuildingValidator.Validate(b).IsValid);

            // A 60-degree corridor (hex-building angle) keeps its width.
            string angled = json.Replace("\"from\": [0, -20], \"to\": [0, -6]", "\"from\": [0, 0], \"to\": [8.66, 5]");
            SpaceData a = BuildingDataLoader.FromJson(angled).FindSpace("SPUR");
            Assert.AreEqual(2.4f * 10f, Math.Abs(Polygon2D.SignedArea(a.Polygon)), 1e-2f);
            Assert.Throws<FormatException>(() => BuildingDataLoader.FromJson(json.Replace("\"to\": [0, -6]", "\"to\": [0, -20]")));
        }

        [Test]
        public void MultiDoorRoom_RoutesToNearestDoor()
        {
            BuildingData b = TestData.Plus();
            var room = new RoomData { Id = "R-2D", Number = "R-2D", FloorId = "F1" };
            room.Doors.Add(new RoomDoor { AccessNodeId = "N" });
            room.Doors.Add(new RoomDoor { AccessNodeId = "E", Opening = new OpeningData { Center = new Vector2(20f, 1.2f), Width = 1f } });
            b.Rooms.Add(room);

            RouteResult fromEast = new RouteFinder(b).FindRoute("EAST", "R-2D");
            Assert.IsTrue(fromEast.Success);
            Assert.AreEqual(1, fromEast.DoorIndex);
            Assert.AreEqual(0f, fromEast.DistanceMetres, 1e-4f);

            b.Edges.First(e => e.From == "X" && e.To == "N").MeasuredDistance = 5f; // N door now nearer from the south
            RouteResult fromSouth = new RouteFinder(b).FindRoute("SOUTH", "R-2D");
            Assert.AreEqual(0, fromSouth.DoorIndex);
            Assert.AreEqual(25f, fromSouth.DistanceMetres, 1e-4f);
            Assert.AreEqual("N", fromSouth.Nodes.Last().Id);
        }

        [Test]
        public void MultiDoorRoom_LoadsFromJsonAndUnknownDoorPositionIsReportedNotDrawn()
        {
            BuildingData b = BuildingDataLoader.FromJson(HexJson);
            RoomData r1 = b.FindRoom("R1");
            Assert.AreEqual(2, r1.Doors.Count);
            Assert.IsNull(r1.Doors[0].Opening);
            Assert.AreEqual(Provenance.Estimated, r1.Doors[0].Provenance); // inherits the room's provenance

            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.MissingMeasurements.Any(m => m.Contains("'R1' door 0") && m.Contains("position unknown")));
            Assert.IsEmpty(FloorLayout.Build(b, b.Floors[0]).Doors);
            RouteResult route = new RouteFinder(b).FindRoute("E1", "R1");
            Assert.IsTrue(route.Success);
            Assert.IsNull(route.Door.Opening);
            Assert.DoesNotThrow(() => RoutePath.FromRoute(route, new RoutePathSettings()));
        }

        [Test]
        public void UnmappedRoom_IsWarningAndRoutesReportMissingNode()
        {
            BuildingData b = BuildingDataLoader.FromJson(HexJson);
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.Warnings.Any(w => w.Contains("'R2'") && w.Contains("no doors")), report.ToString());
            Assert.IsFalse(report.Errors.Any(e => e.Contains("R2")));
            Assert.AreEqual(RouteFailure.MissingNode, new RouteFinder(b).FindRoute("E1", "R2").Failure);
        }

        [Test]
        public void VerifiedStatus_IsRejectedWhileAnythingIsUnmeasured()
        {
            BuildingData b = BuildingDataLoader.FromJson(HexJson.Replace("\"partial\"", "\"verified\""));
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.Errors.Any(e => e.Contains("verified")), report.ToString());

            BuildingData partial = BuildingDataLoader.FromJson(HexJson);
            Assert.IsFalse(BuildingValidator.Validate(partial).Errors.Any(e => e.Contains("verified")));
        }

        [Test]
        public void VerifiedStatus_AcceptedWhenEverythingIsMeasured()
        {
            string json = HexJson.Replace("\"partial\"", "\"verified\"").Replace("\"estimated\"", "\"measured\"")
                .Replace("{ \"accessNode\": \"SE\" }, { \"accessNode\": \"SW\" }",
                         "{ \"accessNode\": \"SE\", \"x\": 6.0, \"z\": -3.46 }, { \"accessNode\": \"SW\", \"x\": -6.0, \"z\": -3.46 }");
            BuildingData b = BuildingDataLoader.FromJson(json);
            foreach (EdgeData e in b.Edges) e.Width = 2.4f;
            b.Rooms.RemoveAll(r => r.Doors.Count == 0);
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsFalse(report.Errors.Any(e => e.Contains("verified")), report.ToString());
            Assert.IsEmpty(report.MissingMeasurements, report.ToString());
        }

        [Test]
        public void Validator_ReportsDuplicateNumbersIsolatedNodesAndMissingMeasurements()
        {
            BuildingData b = TestData.Plus();
            b.Rooms.Add(new RoomData { Id = "R-N-copy", Number = "r n", FloorId = "F1" });
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.Warnings.Any(w => w.Contains("Duplicate room number 'RN'")), report.ToString());
            Assert.IsTrue(report.Warnings.Any(w => w.Contains("'ISO' has no navigable connections")), report.ToString());
            Assert.IsTrue(report.MissingMeasurements.Any(m => m.Contains("width not recorded")));
            Assert.IsTrue(report.MissingMeasurements.Any(m => m.Contains("Node 'X' position is Placeholder")));
        }

        [Test]
        public void Validator_RejectsDuplicateRoomIdsAndBadDoorNodes()
        {
            BuildingData b = TestData.Plus();
            var dup = new RoomData { Id = "R-N", Number = "R-N2", FloorId = "F1" };
            dup.Doors.Add(new RoomDoor { AccessNodeId = "NOWHERE" });
            b.Rooms.Add(dup);
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.Errors.Any(e => e.Contains("Duplicate room 'R-N'")), report.ToString());
            Assert.IsTrue(report.Errors.Any(e => e.Contains("NOWHERE")), report.ToString());
        }

        [Test]
        public void HexRing_WithOctagonalCentreIsConvexPiecesAroundTheOpening()
        {
            List<List<Vector2>> pieces = Polygon2D.HexRing(Vector2.Zero, 5f, 2f, 0f, innerSides: 8, innerRotationDegrees: 0f);
            Assert.AreEqual(12, pieces.Count); // hexagon corners (6) + octagon corners (8), two shared (0 and 180 degrees)
            foreach (List<Vector2> p in pieces)
            {
                Polygon2D.MakeCounterClockwise(p);
                Assert.IsTrue(Polygon2D.IsConvex(p));
            }
            float hex = 1.5f * (float)Math.Sqrt(3) * 25f, oct = 2f * (float)Math.Sqrt(2) * 4f;
            Assert.AreEqual(hex - oct, pieces.Sum(p => Math.Abs(Polygon2D.SignedArea(p))), 1e-2f);
            List<WallSegment> walls = WallGenerator.BuildOutline(pieces.Cast<IList<Vector2>>().ToList());
            float octPerimeter = 8f * 2f * 2f * (float)Math.Sin(Math.PI / 8);
            Assert.AreEqual(30f + octPerimeter, walls.Sum(w => w.Length), 1e-3f);
            Assert.Throws<FormatException>(() => Polygon2D.HexRing(Vector2.Zero, 5f, 4.9f, 0f, innerSides: 8)); // octagon pokes out of the hexagon
            Assert.Throws<FormatException>(() => Polygon2D.HexRing(Vector2.Zero, 5f, 2f, 0f, innerSides: 2));
        }

        [Test]
        public void Observation_WithStrideIsComparedWithTheMap()
        {
            BuildingData b = TestData.Plus(); // S(0,-20) to N(0,20) is 40 m
            b.Observations.Add(new SurveyObservation { Id = "OK", FromNodeId = "S", ToNodeId = "N", Steps = 57, StrideMetres = 0.7f });
            b.Observations.Add(new SurveyObservation { Id = "OFF", FromNodeId = "S", ToNodeId = "N", Steps = 30, StrideMetres = 0.7f });
            b.Observations.Add(new SurveyObservation { Id = "ISOLATED", FromNodeId = "S", ToNodeId = "ISO", Steps = 10, StrideMetres = 0.7f });
            b.Observations.Add(new SurveyObservation { Id = "GHOST", FromNodeId = "S", ToNodeId = "NOWHERE", Steps = 10 });
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsFalse(report.Warnings.Any(w => w.Contains("'OK'")), report.ToString());
            Assert.IsTrue(report.Warnings.Any(w => w.Contains("'OFF'") && w.Contains("disagrees")), report.ToString());
            Assert.IsTrue(report.Warnings.Any(w => w.Contains("'ISOLATED'") && w.Contains("not connected")), report.ToString());
            Assert.IsTrue(report.Errors.Any(e => e.Contains("'GHOST'")), report.ToString());
        }

        [Test]
        public void Observation_LoadsFromJsonWithoutStride()
        {
            string json = HexJson.TrimEnd().TrimEnd('}') + @", ""observations"": [ { ""id"": ""O1"", ""from"": ""IN"", ""to"": ""S"", ""steps"": 15, ""note"": ""approx"" } ] }";
            BuildingData b = BuildingDataLoader.FromJson(json);
            SurveyObservation o = b.Observations.Single();
            Assert.AreEqual("pacedSteps", o.Kind);
            Assert.IsNull(o.StrideMetres);
            Assert.IsTrue(o.Approximate);
            Assert.IsTrue(BuildingValidator.Validate(b).MissingMeasurements.Any(m => m.Contains("'O1'")));
        }

        /// <summary>Every packaged data file and every mapping draft must load and validate without errors.</summary>
        [Test]
        public void AllDataFilesAndDrafts_AreValid()
        {
            string packaged = Path.GetDirectoryName(TestData.FindRepoFile(TestData.SampleRelativePath));
            string drafts = Path.GetDirectoryName(TestData.FindRepoFile(PrpDraftTests.DraftRelativePath));
            string[] files = Directory.GetFiles(packaged, "*.json").Concat(Directory.GetFiles(drafts, "*.json")).ToArray();
            Assert.GreaterOrEqual(files.Length, 2);
            foreach (string f in files)
            {
                ValidationReport report = BuildingValidator.Validate(BuildingDataLoader.FromJson(File.ReadAllText(f)));
                Assert.IsTrue(report.IsValid, Path.GetFileName(f) + ": " + report);
            }
        }
    }
}
