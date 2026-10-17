using System.Collections.Generic;
using System.Numerics;
using Atria.Core.Data;

namespace Atria.Core.Geometry
{
    /// <summary>Generated plan geometry for one floor: walls with door/entrance gaps cut out.</summary>
    public sealed class FloorLayout
    {
        public FloorData Floor;
        public readonly List<SpaceData> Spaces = new List<SpaceData>();
        public List<WallSegment> Walls = new List<WallSegment>();
        /// <summary>Door placements keyed by <see cref="DoorKey"/>.</summary>
        public readonly Dictionary<string, OpeningPlacement> Doors = new Dictionary<string, OpeningPlacement>();
        /// <summary>Entrance placements keyed by entrance id.</summary>
        public readonly Dictionary<string, OpeningPlacement> Entrances = new Dictionary<string, OpeningPlacement>();

        public const string DoorTagPrefix = "door:";
        public const string EntranceTagPrefix = "entrance:";

        /// <summary>Key of a room's door: "ROOM#0", "ROOM#1", ... in the order the doors are listed.</summary>
        public static string DoorKey(string roomId, int doorIndex) => roomId + "#" + doorIndex;

        public static FloorLayout Build(BuildingData building, FloorData floor)
        {
            var layout = new FloorLayout { Floor = floor };
            var polys = new List<IList<Vector2>>();
            foreach (SpaceData s in building.Spaces)
            {
                if (s.FloorId != floor.Id || s.Polygon.Count < 3) continue;
                layout.Spaces.Add(s);
                polys.Add(s.Polygon);
            }
            layout.Walls = WallGenerator.BuildOutline(polys);

            var requests = new List<OpeningRequest>();
            foreach (RoomData r in building.Rooms)
                for (int i = 0; i < r.Doors.Count; i++)
                    if (r.FloorId == floor.Id && r.Doors[i].Opening != null)
                        requests.Add(new OpeningRequest { Tag = DoorTagPrefix + DoorKey(r.Id, i), Center = r.Doors[i].Opening.Center, Width = r.Doors[i].Opening.Width });
            foreach (EntranceData e in building.Entrances)
            {
                NodeData node = building.FindNode(e.NodeId);
                if (e.Opening != null && node != null && node.FloorId == floor.Id)
                    requests.Add(new OpeningRequest { Tag = EntranceTagPrefix + e.Id, Center = e.Opening.Center, Width = e.Opening.Width });
            }

            foreach (OpeningPlacement p in WallGenerator.CutOpenings(layout.Walls, requests))
            {
                if (p.Tag.StartsWith(DoorTagPrefix)) layout.Doors[p.Tag.Substring(DoorTagPrefix.Length)] = p;
                else layout.Entrances[p.Tag.Substring(EntranceTagPrefix.Length)] = p;
            }
            return layout;
        }

        public bool IsWalkable(Vector2 p)
        {
            foreach (SpaceData s in Spaces)
                if (Polygon2D.ContainsConvex(s.Polygon, p, 1e-3f)) return true;
            return false;
        }
    }
}
