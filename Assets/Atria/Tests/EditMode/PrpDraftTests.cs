using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Atria.Core.Data;
using Atria.Core.Geometry;
using Atria.Core.Navigation;
using Atria.Core.Search;
using NUnit.Framework;

namespace Atria.Core.Tests
{
    /// <summary>The provisional PRP left-section draft (Assets/Atria/MappingData/prp_g_left_draft.json).</summary>
    public class PrpDraftTests
    {
        public const string DraftRelativePath = "Assets/Atria/MappingData/prp_g_left_draft.json";
        private const string Start = "DRAFT_START_HALL";

        private static BuildingData Load() => BuildingDataLoader.FromJson(File.ReadAllText(TestData.FindRepoFile(DraftRelativePath)));

        [Test]
        public void Draft_IsValidAndClearlyProvisional()
        {
            BuildingData b = Load();
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual("partial", b.DataStatus);
            Assert.IsFalse(b.IsVerified);
            StringAssert.Contains("PROVISIONAL", b.DisplayName);

            // Nothing in the draft may claim to be measured.
            Assert.IsFalse(b.Spaces.Any(s => s.Provenance == Provenance.Measured));
            Assert.IsFalse(b.Nodes.Any(n => n.Provenance == Provenance.Measured));
            Assert.IsFalse(b.Edges.Any(e => e.Provenance == Provenance.Measured || e.MeasuredDistance.HasValue));
            Assert.IsFalse(b.Rooms.Any(r => r.Provenance == Provenance.Measured || r.Doors.Any(d => d.Provenance == Provenance.Measured)));
            Assert.IsNotEmpty(report.MissingMeasurements);

            // Flipping it to "verified" must be rejected.
            b.DataStatus = "verified";
            Assert.IsTrue(BuildingValidator.Validate(b).Errors.Any(e => e.Contains("verified")));
        }

        [Test]
        public void Draft_HexRingsKeepTheirCentralVoid()
        {
            BuildingData b = Load();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            // L1: hexagon outside, octagon centre (expanded drawing) -> 12 convex pieces; L2: hexagon/hexagon -> 6.
            foreach ((string ring, int pieces) in new[] { ("L1", 12), ("L2", 6) })
            {
                Assert.AreEqual(pieces, b.Spaces.Count(s => s.Id.StartsWith(ring + "#")), ring);
                NodeData corner = b.FindNode(ring + "_C0");
                var center = new Vector2(corner.Position.X, corner.Position.Z - 3.6f);
                Assert.IsFalse(layout.IsWalkable(center), ring + " void must not be walkable");
                Assert.IsTrue(layout.IsWalkable(new Vector2(corner.Position.X, corner.Position.Z)), ring + " ring corridor");
            }
            Assert.IsFalse(b.Nodes.Any(n => layout.IsWalkable(new Vector2(n.Position.X, n.Position.Z)) == false));
        }

        [Test]
        public void Draft_EveryMappedRoomIsReachableAndCameraStaysInCorridors()
        {
            BuildingData b = Load();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            var finder = new RouteFinder(b);
            foreach (RoomData room in b.Rooms.Where(r => r.Doors.Count > 0))
            {
                RouteResult route = finder.FindRoute(Start, room.Id);
                Assert.IsTrue(route.Success, $"{room.Id}: {route.Failure}");
                Assert.IsTrue(route.UsesUnverifiedData, room.Id);
                RoutePath path = RoutePath.FromRoute(route, new RoutePathSettings());
                for (int i = 0; i <= 300; i++)
                {
                    System.Numerics.Vector3 p = path.PositionAtProgress(i / 300f);
                    Assert.IsTrue(layout.IsWalkable(new Vector2(p.X, p.Z)), $"{room.Id}: camera leaves corridor at {p}");
                }
            }
        }

        [Test]
        public void Draft_TwoDoorRoomsRouteToTheNearerDoor()
        {
            BuildingData b = Load();
            foreach (string id in new[] { "G18", "G21", "G23", "G25", "G26" })
                Assert.AreEqual(2, b.FindRoom(id).Doors.Count, id);

            // From the hall, the path enters L2 at its south corner, so G23's lower (first) door is nearer.
            var finder = new RouteFinder(b);
            RouteResult g23 = finder.FindRoute(Start, "G23");
            Assert.AreEqual(0, g23.DoorIndex);
            Assert.AreEqual(g23.Door.AccessNodeId, g23.Nodes.Last().Id);
            b.FindRoom("G23").Doors.RemoveAt(0); // only the upper door left: the route must be longer
            Assert.Greater(new RouteFinder(b).FindRoute(Start, "G23").DistanceMetres, g23.DistanceMetres);
        }

        [Test]
        public void Draft_UnknownsStayUnknown()
        {
            BuildingData b = Load();
            var finder = new RouteFinder(b);

            // G17: corridor known, door position unknown -> routable to the corridor, nothing drawn.
            RoomData g17 = b.FindRoom("G17");
            Assert.IsNull(g17.Doors.Single().Opening);
            Assert.IsTrue(finder.FindRoute(Start, "G17").Success);
            Assert.IsFalse(FloorLayout.Build(b, b.Floors[0]).Doors.ContainsKey(FloorLayout.DoorKey("G17", 0)));

            // Physics Research Lab -II: seen, not located -> searchable but unmapped.
            Assert.AreEqual(RouteFailure.MissingNode, finder.FindRoute(Start, "PHYSICS-LAB-II").Failure);

            // The corridor beyond the top of L2 is unknown: L2's north corner only connects around the ring.
            Assert.AreEqual(2, b.Edges.Count(e => e.From == "L2_C0" || e.To == "L2_C0"));

            // Only Entrance 1 is observed; the hall start point stays labelled as not an entrance.
            CollectionAssert.AreEqual(new[] { "ENT_1", "DRAFT_START_HALL" }, b.Entrances.Select(e => e.Id).ToArray());
            StringAssert.Contains("not an entrance", b.FindEntrance("DRAFT_START_HALL").Label);
            Assert.AreEqual(Provenance.Estimated, b.FindEntrance("ENT_1").Provenance);
        }

        [Test]
        public void Search_DistinguishesDraftLabelsFromDemoIds()
        {
            var draft = new RoomSearch(Load().Rooms);
            var demo = new RoomSearch(TestData.LoadShippedSample().Rooms);
            Assert.AreEqual("G23", draft.FindExact("g 23").Id);
            Assert.IsNull(draft.FindExact("DEMO-122"));
            Assert.IsEmpty(draft.Search("DEMO"));
            Assert.IsNull(demo.FindExact("G23"));
            CollectionAssert.AreEquivalent(new[] { "G21", "G23", "G25", "G26" }, draft.Search("G2").Select(m => m.Room.Id).ToArray());
            Assert.AreEqual("PHYSICS-LAB-II", draft.Search("physics")[0].Room.Id);
        }

        [Test]
        public void Draft_Entrance1IsAtTheBendAndReachesG18AndG21()
        {
            BuildingData b = Load();
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.IsValid, report.ToString()); // the opening lies on a wall
            EntranceData e1 = b.FindEntrance("ENT_1");
            Assert.AreEqual("CORR_BEND", e1.NodeId);
            Assert.IsTrue(FloorLayout.Build(b, b.Floors[0]).Entrances["ENT_1"].Placed);

            var finder = new RouteFinder(b);
            foreach (string room in new[] { "G18", "G21" })
            {
                RouteResult r = finder.FindRoute("ENT_1", room);
                Assert.IsTrue(r.Success, room);
                Assert.AreEqual("CORR_BEND", r.Nodes[0].Id);
                Assert.AreEqual("L1_S270", r.Nodes[1].Id, room + ": enters L1 from the corridor west of it");
            }
            Assert.IsTrue(finder.FindRoute("ENT_1", "G23").Success); // and up the corridor to L2
        }

        [Test]
        public void Draft_L1CentreIsAnOctagonalVoid()
        {
            BuildingData b = Load();
            VoidData l1 = b.Voids.Single(v => v.Id == "L1#void");
            VoidData l2 = b.Voids.Single(v => v.Id == "L2#void");
            Assert.AreEqual(8, l1.Polygon.Count);
            Assert.AreEqual(6, l2.Polygon.Count);
            Assert.IsTrue(Polygon2D.IsConvex(l1.Polygon));
        }

        [Test]
        public void Draft_FifteenStepObservationIsStoredUncalibrated()
        {
            BuildingData b = Load();
            SurveyObservation o = b.Observations.Single();
            Assert.AreEqual("OBS_ENT1_TO_L1", o.Id);
            Assert.AreEqual(15f, o.Steps);
            Assert.IsTrue(o.Approximate);
            Assert.IsNull(o.StrideMetres, "no stride has been calibrated, so no metric distance may be derived");
            Assert.AreEqual("CORR_BEND", o.FromNodeId);
            Assert.AreEqual("L1_S270", o.ToNodeId);
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.MissingMeasurements.Any(m => m.Contains("OBS_ENT1_TO_L1") && m.Contains("stride not calibrated")));
            Assert.IsFalse(report.Warnings.Any(w => w.Contains("OBS_ENT1_TO_L1")));
        }
    }
}
