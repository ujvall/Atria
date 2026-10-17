using System;
using System.Collections.Generic;
using Atria.Core.Data;

namespace Atria.Core.Navigation
{
    public enum RouteFailure
    {
        None,
        UnknownEntrance,
        UnknownRoom,
        /// <summary>The entrance or room references a node that is missing from the graph.</summary>
        MissingNode,
        /// <summary>No chain of navigable, established edges connects the two points.</summary>
        Unreachable,
    }

    public sealed class RouteResult
    {
        public RouteFailure Failure;
        public EntranceData Entrance;
        public RoomData Room;
        /// <summary>Ordered nodes from the entrance node to the room's access node.</summary>
        public readonly List<NodeData> Nodes = new List<NodeData>();
        /// <summary>Edges between consecutive <see cref="Nodes"/> (Count = Nodes.Count - 1).</summary>
        public readonly List<EdgeData> Edges = new List<EdgeData>();
        public float DistanceMetres;
        public float WalkingSpeedMps;

        public bool Success => Failure == RouteFailure.None;
        public float EstimatedSeconds => WalkingSpeedMps > 0f ? DistanceMetres / WalkingSpeedMps : 0f;
        /// <summary>True if any part of the route relies on unmeasured data.</summary>
        public bool UsesUnverifiedData { get; internal set; }
    }

    /// <summary>Dijkstra shortest path over <see cref="NavGraph"/>. Pure C#: no rendering required.</summary>
    public sealed class RouteFinder
    {
        private readonly BuildingData _building;
        private readonly NavGraph _graph;

        public RouteFinder(BuildingData building)
        {
            _building = building ?? throw new ArgumentNullException(nameof(building));
            _graph = NavGraph.Build(building);
        }

        public NavGraph Graph => _graph;

        /// <param name="walkingSpeedMps">Overrides the building default when &gt; 0.</param>
        public RouteResult FindRoute(string entranceId, string roomId, float walkingSpeedMps = 0f)
        {
            var result = new RouteResult
            {
                WalkingSpeedMps = walkingSpeedMps > 0f ? walkingSpeedMps : _building.WalkingSpeedMps,
                Entrance = _building.FindEntrance(entranceId),
                Room = _building.FindRoom(roomId),
            };
            if (result.Entrance == null) { result.Failure = RouteFailure.UnknownEntrance; return result; }
            if (result.Room == null) { result.Failure = RouteFailure.UnknownRoom; return result; }
            if (!_graph.TryGetIndex(result.Entrance.NodeId, out int start) ||
                !_graph.TryGetIndex(result.Room.AccessNodeId, out int goal))
            {
                result.Failure = RouteFailure.MissingNode;
                return result;
            }

            List<int> path = ShortestPath(start, goal, out List<NavGraph.Link> links, out float distance);
            if (path == null) { result.Failure = RouteFailure.Unreachable; return result; }

            bool unverified = result.Entrance.Provenance != Provenance.Measured || result.Room.Provenance != Provenance.Measured;
            foreach (int i in path)
            {
                NodeData n = _graph.Node(i);
                result.Nodes.Add(n);
                unverified |= n.Provenance != Provenance.Measured;
            }
            foreach (NavGraph.Link l in links)
            {
                result.Edges.Add(l.Edge);
                unverified |= l.Edge.Provenance != Provenance.Measured;
            }
            result.DistanceMetres = distance;
            result.UsesUnverifiedData = unverified;
            return result;
        }

        private List<int> ShortestPath(int start, int goal, out List<NavGraph.Link> links, out float distance)
        {
            int n = _graph.NodeCount;
            var dist = new float[n];
            var prevNode = new int[n];
            var prevLink = new NavGraph.Link[n];
            for (int i = 0; i < n; i++) { dist[i] = float.PositiveInfinity; prevNode[i] = -1; }
            dist[start] = 0f;

            // Priority queue: sorted set keyed by (distance, node); stale entries are removed on relaxation.
            var open = new SortedSet<(float, int)>();
            open.Add((0f, start));
            while (open.Count > 0)
            {
                (float d, int u) = open.Min;
                open.Remove(open.Min);
                if (u == goal) break;
                if (d > dist[u]) continue;
                foreach (NavGraph.Link link in _graph.Links(u))
                {
                    float nd = d + link.Distance;
                    if (nd >= dist[link.To]) continue;
                    if (!float.IsPositiveInfinity(dist[link.To])) open.Remove((dist[link.To], link.To));
                    dist[link.To] = nd;
                    prevNode[link.To] = u;
                    prevLink[link.To] = link;
                    open.Add((nd, link.To));
                }
            }

            links = new List<NavGraph.Link>();
            distance = dist[goal];
            if (float.IsPositiveInfinity(distance)) return null;

            var path = new List<int>();
            for (int v = goal; v != -1; v = prevNode[v])
            {
                path.Add(v);
                if (prevLink[v] != null) links.Add(prevLink[v]);
            }
            path.Reverse();
            links.Reverse();
            return path;
        }
    }
}
