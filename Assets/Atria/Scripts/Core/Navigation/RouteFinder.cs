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
        /// <summary>Index into <see cref="RoomData.Doors"/> of the door the route reaches (the nearest one), or -1.</summary>
        public int DoorIndex = -1;
        public RoomDoor Door => DoorIndex >= 0 ? Room.Doors[DoorIndex] : null;
        /// <summary>Ordered nodes from the entrance node to the chosen door's access node.</summary>
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
            // A room may have several doors; route to whichever is nearest.
            var goals = new Dictionary<int, int>(); // graph node -> door index
            for (int d = 0; d < result.Room.Doors.Count; d++)
                if (_graph.TryGetIndex(result.Room.Doors[d].AccessNodeId, out int g) && !goals.ContainsKey(g)) goals[g] = d;
            if (!_graph.TryGetIndex(result.Entrance.NodeId, out int start) || goals.Count == 0)
            {
                result.Failure = RouteFailure.MissingNode;
                return result;
            }

            List<int> path = ShortestPath(start, goals, out List<NavGraph.Link> links, out float distance);
            if (path == null) { result.Failure = RouteFailure.Unreachable; return result; }
            result.DoorIndex = goals[path[path.Count - 1]];

            bool unverified = result.Entrance.Provenance != Provenance.Measured || result.Room.Provenance != Provenance.Measured
                || result.Door.Provenance != Provenance.Measured;
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

        /// <summary>Shortest walkable distance between two nodes, or null if either is unknown or they are not connected.</summary>
        public float? ShortestDistance(string fromNodeId, string toNodeId)
        {
            if (!_graph.TryGetIndex(fromNodeId, out int a) || !_graph.TryGetIndex(toNodeId, out int b)) return null;
            return ShortestPath(a, new Dictionary<int, int> { [b] = 0 }, out _, out float d) == null ? (float?)null : d;
        }

        private List<int> ShortestPath(int start, Dictionary<int, int> goals, out List<NavGraph.Link> links, out float distance)
        {
            int n = _graph.NodeCount;
            var dist = new float[n];
            var prevNode = new int[n];
            var prevLink = new NavGraph.Link[n];
            for (int i = 0; i < n; i++) { dist[i] = float.PositiveInfinity; prevNode[i] = -1; }
            dist[start] = 0f;
            int goal = -1;

            // Priority queue: sorted set keyed by (distance, node); stale entries are removed on relaxation.
            var open = new SortedSet<(float, int)>();
            open.Add((0f, start));
            while (open.Count > 0)
            {
                (float d, int u) = open.Min;
                open.Remove(open.Min);
                if (goals.ContainsKey(u)) { goal = u; break; } // first goal popped is the nearest
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
            distance = goal >= 0 ? dist[goal] : float.PositiveInfinity;
            if (goal < 0) return null;

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
