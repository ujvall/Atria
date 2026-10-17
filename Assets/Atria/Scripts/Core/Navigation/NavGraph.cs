using System.Collections.Generic;
using System.Numerics;
using Atria.Core.Data;

namespace Atria.Core.Navigation
{
    /// <summary>Undirected weighted graph built from the building's nodes and navigable edges.</summary>
    public sealed class NavGraph
    {
        public sealed class Link
        {
            public int To;
            public float Distance;
            public EdgeData Edge;
        }

        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>();
        private readonly List<NodeData> _nodes = new List<NodeData>();
        private readonly List<List<Link>> _adjacency = new List<List<Link>>();

        public int NodeCount => _nodes.Count;
        public NodeData Node(int index) => _nodes[index];
        public IReadOnlyList<Link> Links(int index) => _adjacency[index];

        public bool TryGetIndex(string nodeId, out int index)
        {
            if (nodeId != null && _indexById.TryGetValue(nodeId, out index)) return true;
            index = -1;
            return false;
        }

        public static NavGraph Build(BuildingData building)
        {
            var g = new NavGraph();
            foreach (NodeData n in building.Nodes)
            {
                if (g._indexById.ContainsKey(n.Id)) continue; // duplicates are reported by the validator
                g._indexById[n.Id] = g._nodes.Count;
                g._nodes.Add(n);
                g._adjacency.Add(new List<Link>());
            }

            foreach (EdgeData e in building.Edges)
            {
                // Unconfirmed or closed connections are never routable.
                if (!e.Navigable) continue;
                if (!g.TryGetIndex(e.From, out int a) || !g.TryGetIndex(e.To, out int b) || a == b) continue;
                float d = EdgeLength(e, g._nodes[a], g._nodes[b]);
                g._adjacency[a].Add(new Link { To = b, Distance = d, Edge = e });
                g._adjacency[b].Add(new Link { To = a, Distance = d, Edge = e });
            }
            return g;
        }

        /// <summary>Measured distance when available, otherwise the 3D straight-line length.</summary>
        public static float EdgeLength(EdgeData e, NodeData from, NodeData to) =>
            e.MeasuredDistance ?? Vector3.Distance(from.Position, to.Position);
    }
}
