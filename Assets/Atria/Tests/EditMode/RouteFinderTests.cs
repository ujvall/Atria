using System.Linq;
using Atria.Core.Data;
using Atria.Core.Navigation;
using NUnit.Framework;

namespace Atria.Core.Tests
{
    public class RouteFinderTests
    {
        [Test]
        public void StraightRoute_HasNoTurnsAndCorrectDistance()
        {
            var finder = new RouteFinder(TestData.Plus());
            RouteResult r = finder.FindRoute("SOUTH", "R-N");

            Assert.IsTrue(r.Success, r.Failure.ToString());
            CollectionAssert.AreEqual(new[] { "S", "X", "N" }, r.Nodes.Select(n => n.Id).ToArray());
            Assert.AreEqual(2, r.Edges.Count);
            Assert.AreEqual(40f, r.DistanceMetres, 1e-4f);
            Assert.AreEqual(40f / 1.25f, r.EstimatedSeconds, 1e-4f);
        }

        [Test]
        public void DifferentEntrances_ReachSameRoom()
        {
            var finder = new RouteFinder(TestData.Plus());
            RouteResult fromWest = finder.FindRoute("WEST", "R-N");
            RouteResult fromEast = finder.FindRoute("EAST", "R-N");

            Assert.IsTrue(fromWest.Success && fromEast.Success);
            Assert.AreEqual("W", fromWest.Nodes[0].Id);
            Assert.AreEqual("E", fromEast.Nodes[0].Id);
            Assert.AreEqual("N", fromWest.Nodes.Last().Id);
            Assert.AreEqual("N", fromEast.Nodes.Last().Id);
            Assert.AreEqual(40f, fromWest.DistanceMetres, 1e-4f);
        }

        [Test]
        public void UnknownRoom_FailsGracefully()
        {
            RouteResult r = new RouteFinder(TestData.Plus()).FindRoute("SOUTH", "NOPE-999");
            Assert.IsFalse(r.Success);
            Assert.AreEqual(RouteFailure.UnknownRoom, r.Failure);
            Assert.IsEmpty(r.Nodes);
        }

        [Test]
        public void UnknownEntrance_FailsGracefully()
        {
            RouteResult r = new RouteFinder(TestData.Plus()).FindRoute("ROOF", "R-N");
            Assert.AreEqual(RouteFailure.UnknownEntrance, r.Failure);
        }

        [Test]
        public void DisconnectedDestination_IsUnreachable()
        {
            RouteResult r = new RouteFinder(TestData.Plus()).FindRoute("SOUTH", "R-ISO");
            Assert.AreEqual(RouteFailure.Unreachable, r.Failure);
            Assert.IsEmpty(r.Nodes);
        }

        [Test]
        public void NonNavigableEdge_IsNeverUsed()
        {
            BuildingData b = TestData.Plus();
            b.Edges.First(e => e.From == "X" && e.To == "N").Navigable = false;
            RouteResult r = new RouteFinder(b).FindRoute("SOUTH", "R-N");
            Assert.AreEqual(RouteFailure.Unreachable, r.Failure);
        }

        [Test]
        public void MissingAccessNode_ReportsMissingNode()
        {
            BuildingData b = TestData.Plus();
            b.FindRoom("R-N").AccessNodeId = "GHOST";
            Assert.AreEqual(RouteFailure.MissingNode, new RouteFinder(b).FindRoute("SOUTH", "R-N").Failure);
        }

        [Test]
        public void ShortestPath_PrefersShorterDetourOverLongerDirectEdge()
        {
            BuildingData b = TestData.Plus();
            // A measured direct S->N link that is longer than going via X must not be chosen.
            b.Edges.Add(new EdgeData { From = "S", To = "N", MeasuredDistance = 55f });
            RouteResult r = new RouteFinder(b).FindRoute("SOUTH", "R-N");
            Assert.AreEqual(40f, r.DistanceMetres, 1e-4f);

            b.Edges[b.Edges.Count - 1].MeasuredDistance = 35f;
            r = new RouteFinder(b).FindRoute("SOUTH", "R-N");
            Assert.AreEqual(35f, r.DistanceMetres, 1e-4f);
            Assert.AreEqual(2, r.Nodes.Count);
        }

        [Test]
        public void WalkingSpeedOverride_ChangesEta()
        {
            RouteResult r = new RouteFinder(TestData.Plus()).FindRoute("SOUTH", "R-N", walkingSpeedMps: 2f);
            Assert.AreEqual(20f, r.EstimatedSeconds, 1e-4f);
        }

        [Test]
        public void PlaceholderData_IsFlaggedAsUnverified()
        {
            RouteResult r = new RouteFinder(TestData.Plus()).FindRoute("SOUTH", "R-N");
            Assert.IsTrue(r.UsesUnverifiedData);
        }

        [Test]
        public void ShippedSample_MultiTurnRoute()
        {
            BuildingData b = TestData.LoadShippedSample();
            RouteResult r = new RouteFinder(b).FindRoute("ENT_A", "DEMO-122");
            Assert.IsTrue(r.Success, r.Failure.ToString());
            Assert.AreEqual("N_ENT_A", r.Nodes[0].Id);
            Assert.AreEqual("N_C3_46", r.Nodes.Last().Id);
            Assert.AreEqual(73f, r.DistanceMetres, 1e-3f);
            CollectionAssert.Contains(r.Nodes.Select(n => n.Id).ToList(), "N_J1");
            CollectionAssert.Contains(r.Nodes.Select(n => n.Id).ToList(), "N_J2");
        }

        [Test]
        public void ShippedSample_EveryEntranceReachesEveryRoom()
        {
            BuildingData b = TestData.LoadShippedSample();
            var finder = new RouteFinder(b);
            foreach (EntranceData e in b.Entrances)
                foreach (RoomData room in b.Rooms)
                    Assert.IsTrue(finder.FindRoute(e.Id, room.Id).Success, $"{e.Id} -> {room.Id}");
        }
    }
}
