using System.Collections.Generic;
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

            // The corridor above L2 (mirroring the right wing) leads nowhere known: it ends at a placeholder, with no rooms.
            Assert.AreEqual(1, b.Edges.Count(e => e.From == "L2_UP_END" || e.To == "L2_UP_END"));
            Assert.IsFalse(b.Rooms.Any(r => r.Doors.Any(d => d.AccessNodeId == "L2_UP_END")));

            // Only Entrance 1 is observed; the hall start point stays labelled as not an entrance.
            CollectionAssert.AreEqual(new[] { "ENT_1", "ENT_MAIN", "DRAFT_START_HALL" }, b.Entrances.Select(e => e.Id).ToArray());
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
                Assert.AreEqual("L1_C300", r.Nodes[1].Id, room + ": enters L1 at its upper-left corner, via the angled approach");
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
            SurveyObservation o = b.Observations.Single(x => x.Id == "OBS_ENT1_TO_L1");
            Assert.AreEqual("OBS_ENT1_TO_L1", o.Id);
            Assert.AreEqual(15f, o.Steps);
            Assert.IsTrue(o.Approximate);
            Assert.IsNull(o.StrideMetres, "no stride has been calibrated, so no metric distance may be derived");
            Assert.AreEqual("CORR_BEND", o.FromNodeId);
            Assert.AreEqual("L1_C300", o.ToNodeId);
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.MissingMeasurements.Any(m => m.Contains("OBS_ENT1_TO_L1") && m.Contains("stride not calibrated")));
            Assert.IsFalse(report.Warnings.Any(w => w.Contains("OBS_ENT1_TO_L1")));
        }

        // ------------------------------------------------------------------ right wing (R1, R2)

        private static readonly string[] RightWingRooms = { "G03", "G04", "G05", "G06", "G07", "G08", "G10", "G11", "ELECTRICAL-ROOM", "LIFT" };

        [Test]
        public void RightWing_IsConnectedToTheHallAndEveryRoomIsReachable()
        {
            BuildingData b = Load();
            var finder = new RouteFinder(b);
            foreach (string start in new[] { "ENT_1", "DRAFT_START_HALL" })
                foreach (string room in RightWingRooms)
                {
                    RouteResult r = finder.FindRoute(start, room);
                    Assert.IsTrue(r.Success, $"{start} -> {room}: {r.Failure}");
                    CollectionAssert.Contains(r.Nodes.Select(n => n.Id).ToList(), "HALL_E", $"{start} -> {room} must pass through the hall's right exit");
                }
            // The only link between the hall and the right wing is the new corridor.
            Assert.AreEqual(1, b.Edges.Count(e => (e.From == "HALL_C" && e.To == "HALL_E") || (e.From == "HALL_E" && e.To == "HALL_C")));
        }

        [Test]
        public void RightWing_DoorsAreOnTheIntendedJunctionSides()
        {
            BuildingData b = Load();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            var expected = new (string room, string ring, float sideYaw, int doors)[]
            {
                ("G08", "R1", 330, 2), ("G07", "R1", 30, 1), ("G10", "R1", 150, 2), ("G11", "R1", 210, 1),
                ("G03", "R2", 330, 2), ("G04", "R2", 270, 2), ("G05", "R2", 90, 2), ("ELECTRICAL-ROOM", "R2", 90, 1), ("G06", "R2", 150, 2),
            };
            foreach (var (room, ring, sideYaw, doors) in expected)
            {
                Vector2 centre = b.Voids.Single(v => v.Id == ring + "#void").Polygon.Aggregate(Vector2.Zero, (a, p) => a + p) / b.Voids.Single(v => v.Id == ring + "#void").Polygon.Count;
                RoomData r = b.FindRoom(room);
                Assert.AreEqual(doors, r.Doors.Count, room);
                for (int i = 0; i < r.Doors.Count; i++)
                {
                    Assert.IsTrue(layout.Doors[FloorLayout.DoorKey(room, i)].Placed, $"{room} door {i} not on a wall");
                    Vector2 d = r.Doors[i].Opening.Center - centre;
                    float yaw = (float)((Math.Atan2(d.X, d.Y) * 180 / Math.PI + 360) % 360);
                    float diff = Math.Abs(((yaw - sideYaw) % 360 + 540) % 360 - 180);
                    Assert.Less(diff, 30f, $"{room} door {i} at yaw {yaw:0} is not on the {sideYaw} side of {ring}");
                }
            }
            // The lift is on the right-hand wall of the corridor above R2.
            OpeningPlacement lift = layout.Doors[FloorLayout.DoorKey("LIFT", 0)];
            Assert.IsTrue(lift.Placed);
            Assert.AreEqual(-1f, lift.Inward.X, 1e-3f, "lift door faces west into the corridor, i.e. it is on the east wall");
        }

        [Test]
        public void RightWing_JunctionCentresHaveTheDrawnShapesAndAreNotWalkable()
        {
            BuildingData b = Load();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            Assert.AreEqual(8, b.Voids.Single(v => v.Id == "R1#void").Polygon.Count); // lower: roughly octagonal centre
            Assert.AreEqual(6, b.Voids.Single(v => v.Id == "R2#void").Polygon.Count); // upper: hexagonal centre
            foreach (string ring in new[] { "R1", "R2" })
            {
                VoidData v = b.Voids.Single(x => x.Id == ring + "#void");
                Vector2 c = v.Polygon.Aggregate(Vector2.Zero, (a, p) => a + p) / v.Polygon.Count;
                Assert.IsFalse(layout.IsWalkable(c), ring);
                Assert.IsTrue(layout.IsInVoid(c), ring);
                Assert.IsTrue(Polygon2D.IsConvex(v.Polygon), ring);
            }
        }

        [Test]
        public void RightWing_HasNoDanglingNodesAndTheLiftIsOnlyALandmark()
        {
            BuildingData b = Load();
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.IsFalse(report.Warnings.Any(w => w.Contains("no navigable connections")), report.ToString());
            Assert.AreEqual(1, b.Floors.Count, "no other floor is invented");
            Assert.IsFalse(b.Edges.Any(e => e.Kind == "lift" || e.Kind == "stairs"), "no vertical route is invented");
            Assert.AreEqual("lift", b.FindNode("R2_LIFT").Kind);

            var search = new RoomSearch(b.Rooms);
            Assert.AreEqual("LIFT", search.Search("lift")[0].Room.Id);
            RoomData electrical = search.Search("electrical")[0].Room;
            Assert.AreEqual("ELECTRICAL-ROOM", electrical.Id);
            Assert.AreEqual("Electrical Room", electrical.Number, "no room number is invented");
            Assert.AreEqual(b.Rooms.Count, b.Rooms.Select(r => r.Id).Distinct().Count());
            Assert.AreEqual(b.Nodes.Count, b.Nodes.Select(n => n.Id).Distinct().Count());
        }

        [Test]
        public void LeftWing_IsTheMirrorImageOfTheRightWing()
        {
            BuildingData b = Load();
            // L1 and the hall corridor mirror the right wing across the hall's centre line (x -> -x).
            VoidData l1 = b.Voids.Single(v => v.Id == "L1#void"), r1 = b.Voids.Single(v => v.Id == "R1#void");
            Vector2 lc = l1.Polygon.Aggregate(Vector2.Zero, (a, p) => a + p) / l1.Polygon.Count;
            Vector2 rc = r1.Polygon.Aggregate(Vector2.Zero, (a, p) => a + p) / r1.Polygon.Count;
            Assert.AreEqual(-rc.X, lc.X, 1e-3f); Assert.AreEqual(rc.Y, lc.Y, 1e-3f);
            Assert.AreEqual(r1.Polygon.Count, l1.Polygon.Count);
            foreach ((string left, string right) in new[] { ("HALL_W", "HALL_E"), ("CORR_LB1", "RCORR_B1") })
            {
                NodeData l = b.FindNode(left), r = b.FindNode(right);
                Assert.AreEqual(-r.Position.X, l.Position.X, 1e-3f, left); Assert.AreEqual(r.Position.Z, l.Position.Z, 1e-3f, left);
            }
            foreach ((string left, string right) in new[] { ("CORR_HALL_L1", "RCORR_HALL_B1"), ("CORR_LB1_L1", "RCORR_B1_R1"), ("CORR_BEND_L2", "RCORR_B2_R2"), ("CORR_L2_UP", "RCORR_R2_UP") })
                Assert.AreEqual(Math.Abs(Polygon2D.SignedArea(b.FindSpace(right).Polygon)), Math.Abs(Polygon2D.SignedArea(b.FindSpace(left).Polygon)), 1e-2f, left);
            // L2 has R2's shape and size, mirrored side of the building.
            VoidData l2 = b.Voids.Single(v => v.Id == "L2#void"), r2 = b.Voids.Single(v => v.Id == "R2#void");
            Assert.AreEqual(Math.Abs(Polygon2D.SignedArea(r2.Polygon)), Math.Abs(Polygon2D.SignedArea(l2.Polygon)), 1e-2f);

            // Deliberate difference (user): the angled run after L1 is shorter than the right wing's 6.9 m.
            NodeData bend = b.FindNode("CORR_BEND");
            Assert.AreEqual(3.0f, Vector2.Distance(lc, new Vector2(bend.Position.X, bend.Position.Z)) - 5f, 1e-2f);
            NodeData rbend = b.FindNode("RCORR_B2");
            Assert.Less(Vector2.Distance(lc, new Vector2(bend.Position.X, bend.Position.Z)), Vector2.Distance(rc, new Vector2(rbend.Position.X, rbend.Position.Z)));

            // Left-wing rooms keep their observed sides, labels and door counts.
            CollectionAssert.AreEquivalent(new[] { "G16", "G17", "G18", "G21", "G23", "G25", "G26", "PHYSICS-LAB-II" },
                b.Rooms.Select(r => r.Id).Except(RightWingRooms).ToArray());
            foreach (string id in new[] { "G18", "G21", "G23", "G25", "G26" }) Assert.AreEqual(2, b.FindRoom(id).Doors.Count, id);
            var finder = new RouteFinder(b);
            RouteResult g23 = finder.FindRoute("ENT_1", "G23");
            Assert.IsTrue(g23.Success);
            Assert.IsFalse(g23.Nodes.Any(n => n.Id.StartsWith("R1") || n.Id.StartsWith("R2") || n.Id == "HALL_E"), "left-wing routes do not detour through the right wing");
            CollectionAssert.Contains(g23.Nodes.Select(n => n.Id).ToList(), "CORR_G17", "the route to L2 passes G17's corridor");
        }

        // ------------------------------------------------------------------ main entrance

        [Test]
        public void MainEntrance_IsCentredOnTheHallsGardenSide()
        {
            BuildingData b = Load();
            SpaceData hall = b.FindSpace("HALL");
            // The hall edge facing +z (towards the courtyard/garden).
            Vector2 a = hall.Polygon.OrderByDescending(p => p.Y).ElementAt(0), c = hall.Polygon.OrderByDescending(p => p.Y).ElementAt(1);
            Vector2 faceMid = (a + c) * 0.5f;
            OpeningPlacement door = FloorLayout.Build(b, b.Floors[0]).Entrances["ENT_MAIN"];
            Assert.IsTrue(door.Placed, "the opening lies on the hall wall");
            Assert.AreEqual(faceMid.X, door.Center.X, 1e-3f, "centred along the face");
            Assert.AreEqual(faceMid.Y, door.Center.Y, 1e-3f);
            Assert.AreEqual(0f, door.Inward.X, 1e-4f);
            Assert.AreEqual(-1f, door.Inward.Y, 1e-4f, "inward points away from the garden, into the hall");
        }

        [Test]
        public void MainEntrance_SpawnIsJustInsideAndClearOfTheWall()
        {
            BuildingData b = Load();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            OpeningPlacement door = layout.Entrances["ENT_MAIN"];
            NodeData spawn = b.FindNode(b.FindEntrance("ENT_MAIN").NodeId);
            var p = new Vector2(spawn.Position.X, spawn.Position.Z);
            Assert.IsTrue(layout.IsWalkable(p), "spawn is inside the building");
            float depth = Vector2.Dot(p - door.Center, door.Inward);
            Assert.AreEqual(0.8f, depth, 1e-3f, "0.8 m inside the threshold");
            Assert.AreEqual(0f, Vector2.Distance(door.Center + door.Inward * depth, p), 1e-3f, "straight in from the doorway midpoint");
            float nearestWall = layout.Walls.Min(w => Vector2.Distance(Vector2.Lerp(w.A, w.B, Polygon2D.ClosestParameter(w.A, w.B, p)), p));
            Assert.Greater(nearestWall, 0.5f, "camera clears every wall");
        }

        [Test]
        public void MainEntrance_StartsFacingInwardAndJoinsTheCorridorGraph()
        {
            BuildingData b = Load();
            var finder = new RouteFinder(b);
            foreach (string room in new[] { "G16", "G18", "G23", "G08", "G05", "LIFT" })
            {
                RouteResult r = finder.FindRoute("ENT_MAIN", room);
                Assert.IsTrue(r.Success, room);
                Assert.AreEqual("HALL_MAIN_ENT", r.Nodes[0].Id, room);
                Assert.AreEqual("HALL_C", r.Nodes[1].Id, room + ": walks into the hall first");
                RoutePath path = RoutePath.FromRoute(r, new RoutePathSettings());
                Assert.AreEqual(180f, path.YawAtProgress(0f), 1f, room + ": starts facing into the hall (-z), away from the garden");
            }
        }

        [Test]
        public void ExistingStartsAreUnchangedByTheMainEntrance()
        {
            BuildingData b = Load();
            Assert.AreEqual("ENT_1", b.Entrances[0].Id, "Entrance 1 stays the default start");
            Assert.AreEqual("CORR_BEND", b.FindEntrance("ENT_1").NodeId);
            NodeData bend = b.FindNode("CORR_BEND");
            Assert.AreEqual(bend.Position.X - 1.2f, b.FindEntrance("ENT_1").Opening.Center.X, 1e-3f, "Entrance 1 is in the west wall at the bend");
            Assert.AreEqual("HALL_C", b.FindEntrance("DRAFT_START_HALL").NodeId);
            var finder = new RouteFinder(b);
            CollectionAssert.AreEqual(new[] { "CORR_BEND", "L1_C300" }, finder.FindRoute("ENT_1", "G21").Nodes.Take(2).Select(n => n.Id).ToArray());
            Assert.IsFalse(finder.FindRoute("ENT_1", "G23").Nodes.Any(n => n.Id == "HALL_MAIN_ENT"));
            Assert.IsFalse(finder.FindRoute("DRAFT_START_HALL", "G08").Nodes.Any(n => n.Id == "HALL_MAIN_ENT"));
        }

        // ------------------------------------------------------------------ R2 foot-placement counts

        private const float Placement = 0.30f; // one heel-to-toe EU 44 Crocs placement, approximate

        [Test]
        public void R2_ProportionsFollowTheFootPlacementCounts()
        {
            BuildingData b = Load();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            // Junction side = 21 placements; for a regular hexagon the side equals the outer radius.
            List<Vector2> outer = Polygon2D.RegularPolygon(Vector2.Zero, 6, 21 * Placement, 0f);
            Assert.AreEqual(21 * Placement, Vector2.Distance(outer[0], outer[1]), 1e-3f);
            VoidData centre = b.Voids.Single(v => v.Id == "R2#void");
            Vector2 c = centre.Polygon.Aggregate(Vector2.Zero, (a, p) => a + p) / centre.Polygon.Count;
            float outerApothem = 21 * Placement * (float)Math.Cos(Math.PI / 6);
            float innerApothem = Vector2.Distance(c, (centre.Polygon[0] + centre.Polygon[1]) * 0.5f);
            Assert.AreEqual(8 * Placement, outerApothem - innerApothem, 0.01f, "ring corridor keeps the 8-placement width");
            Assert.AreEqual(6, centre.Polygon.Count);
            Assert.IsFalse(layout.IsWalkable(c));

            // Upper corridor: 33 x 8 placements from R2's outer top corner.
            float topCorner = c.Y + 21 * Placement;
            var up = b.Spaces.Single(s => s.Id == "RCORR_R2_UP");
            float zMax = up.Polygon.Max(p => p.Y), xMin = up.Polygon.Min(p => p.X), xMax = up.Polygon.Max(p => p.X);
            Assert.AreEqual(33 * Placement, zMax - topCorner, 0.01f);
            Assert.AreEqual(8 * Placement, xMax - xMin, 0.01f);

            // Angled approach: 23 placements from R1's outer corner to the bend.
            Vector2 r1 = b.Voids.Single(v => v.Id == "R1#void").Polygon.Aggregate(Vector2.Zero, (a, p) => a + p) / 8f;
            NodeData bend = b.FindNode("RCORR_B2");
            Assert.AreEqual(23 * Placement, Vector2.Distance(r1, new Vector2(bend.Position.X, bend.Position.Z)) - 5f, 0.02f);

            // Counts are stored with their method; the map agrees with them within the validator tolerance.
            ValidationReport report = BuildingValidator.Validate(b);
            Assert.IsTrue(report.IsValid, report.ToString());
            foreach (string id in new[] { "OBS_R2_UPPER_CORRIDOR", "OBS_R1_R2_ANGLED_APPROACH" })
            {
                SurveyObservation o = b.Observations.Single(x => x.Id == id);
                Assert.AreEqual("footPlacements", o.Kind);
                Assert.IsTrue(o.Approximate);
                Assert.IsFalse(report.Warnings.Any(w => w.Contains(id)), report.ToString());
            }
        }

        [Test]
        public void FloorHeight_IsTheConfirmedValueAndNothingIsMarkedVerified()
        {
            BuildingData b = Load();
            Assert.That(b.Floors.Single().WallHeight, Is.InRange(2.75f, 3.0f));
            Assert.IsFalse(b.IsVerified);
            Assert.IsFalse(b.Spaces.Any(s => s.Provenance == Provenance.Measured));
        }

        [Test]
        public void R2_RoutesStillReachEveryRightWingRoomFromAllStarts()
        {
            BuildingData b = Load();
            var finder = new RouteFinder(b);
            foreach (string start in new[] { "ENT_1", "ENT_MAIN", "DRAFT_START_HALL" })
                foreach (string room in new[] { "G03", "G04", "G05", "G06", "ELECTRICAL-ROOM", "LIFT", "G08", "G23" })
                    Assert.IsTrue(finder.FindRoute(start, room).Success, $"{start} -> {room}");
        }

        [Test]
        public void LeftWing_HallCorridorAlsoContinuesTowardsTheLowerLeftBlock()
        {
            BuildingData b = Load();
            FloorLayout layout = FloorLayout.Build(b, b.Floors[0]);
            // The bend before L1 is a junction: to L1, back to the hall, and on towards the lower-left block.
            string[] links = b.Edges.Where(e => e.From == "CORR_LB1" || e.To == "CORR_LB1").Select(e => e.From == "CORR_LB1" ? e.To : e.From).ToArray();
            CollectionAssert.AreEquivalent(new[] { "HALL_W", "L1_C120", "CORR_LBLOCK_END" }, links);
            // The branch runs below L1 without touching its ring or centre, and has no invented rooms.
            NodeData end = b.FindNode("CORR_LBLOCK_END");
            Assert.IsTrue(layout.IsWalkable(new Vector2(end.Position.X, end.Position.Z)));
            Assert.IsFalse(b.Rooms.Any(r => r.Doors.Any(d => d.AccessNodeId == "CORR_LBLOCK_END")));
            SpaceData branch = b.FindSpace("CORR_LB1_BLOCK");
            foreach (SpaceData ring in b.Spaces.Where(s => s.Id.StartsWith("L1#")))
                foreach (Vector2 p in branch.Polygon)
                    Assert.IsFalse(Polygon2D.ContainsConvex(ring.Polygon, p), "branch corner inside L1");
            Assert.IsTrue(new RouteFinder(b).ShortestDistance("HALL_MAIN_ENT", "CORR_LBLOCK_END").HasValue);
        }
    }
}
