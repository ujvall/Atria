using System.Collections.Generic;
using System.Numerics;

namespace Atria.Core.Data
{
    /// <summary>
    /// How trustworthy a piece of building data is. Every node, edge, space, door and room
    /// carries one so placeholder geometry can never be mistaken for surveyed geometry.
    /// </summary>
    public enum Provenance
    {
        /// <summary>Invented purely to exercise the software. Not PRP architecture.</summary>
        Placeholder,
        /// <summary>Estimated from imagery or memory; plausible but unmeasured.</summary>
        Estimated,
        /// <summary>Measured on site or taken from drawings.</summary>
        Measured,
    }

    /// <summary>
    /// Building-local coordinate system (see docs/DATA_FORMAT.md):
    /// units are metres, +X and +Z span the floor plan, +Y is up. Positions are stored as
    /// Vector3(x, elevation, z) so they map one-to-one onto Unity world space.
    /// Yaw is in degrees, 0 = facing +Z, 90 = facing +X (Unity convention).
    /// </summary>
    public sealed class BuildingData
    {
        public int SchemaVersion;
        public string BuildingId;
        public string DisplayName;
        /// <summary>"sample", "partial" or "verified". Anything but "verified" shows a warning banner.</summary>
        public string DataStatus;
        public string Notes;
        public float WalkingSpeedMps = 1.2f;
        public float DefaultCorridorWidth = 2.4f;
        public float DefaultDoorWidth = 1.0f;

        public readonly List<FloorData> Floors = new List<FloorData>();
        public readonly List<SpaceData> Spaces = new List<SpaceData>();
        public readonly List<NodeData> Nodes = new List<NodeData>();
        public readonly List<EdgeData> Edges = new List<EdgeData>();
        public readonly List<EntranceData> Entrances = new List<EntranceData>();
        public readonly List<RoomData> Rooms = new List<RoomData>();

        public bool IsVerified => DataStatus == "verified";

        public FloorData FindFloor(string id) => Floors.Find(f => f.Id == id);
        public NodeData FindNode(string id) => Nodes.Find(n => n.Id == id);
        public EntranceData FindEntrance(string id) => Entrances.Find(e => e.Id == id);
        public RoomData FindRoom(string id) => Rooms.Find(r => r.Id == id);
        public SpaceData FindSpace(string id) => Spaces.Find(s => s.Id == id);
    }

    public sealed class FloorData
    {
        public string Id;
        public string Name;
        public float Elevation;
        public float WallHeight = 3.0f;
    }

    /// <summary>
    /// A convex walkable polygon (corridor segment, lobby, junction). Walls are generated on the
    /// outline of the union of spaces; non-convex areas must be split into convex pieces.
    /// </summary>
    public sealed class SpaceData
    {
        public string Id;
        public string FloorId;
        public string Kind;
        /// <summary>Outline in plan (x, z), normalised to counter-clockwise order on load.</summary>
        public readonly List<Vector2> Polygon = new List<Vector2>();
        public Provenance Provenance;
    }

    public sealed class NodeData
    {
        public string Id;
        public string FloorId;
        public string Kind;
        public Vector3 Position;
        public Provenance Provenance;
    }

    public sealed class EdgeData
    {
        public string From;
        public string To;
        /// <summary>Only navigable edges are used by the router (closed or unconfirmed links set false).</summary>
        public bool Navigable = true;
        /// <summary>"corridor", "stairs", "lift", ...</summary>
        public string Kind = "corridor";
        /// <summary>Walked distance in metres if measured; otherwise the straight-line length is used.</summary>
        public float? MeasuredDistance;
        /// <summary>Clear width of the corridor along this edge; bounds how much the camera may cut corners.</summary>
        public float? Width;
        public Provenance Provenance;
    }

    public sealed class OpeningData
    {
        /// <summary>Centre of the opening in plan (x, z); must lie on a generated wall.</summary>
        public Vector2 Center;
        public float Width;
    }

    public sealed class EntranceData
    {
        public string Id;
        public string Label;
        public string NodeId;
        /// <summary>Gap cut in the outer wall for this entrance (optional).</summary>
        public OpeningData Opening;
        public Provenance Provenance;
    }

    public sealed class RoomData
    {
        public string Id;
        /// <summary>The number printed on the door sign.</summary>
        public string Number;
        public string Name;
        public readonly List<string> Aliases = new List<string>();
        public string FloorId;
        /// <summary>
        /// Every door of the room. Empty means the room is known to exist but is not mapped yet:
        /// it is searchable, and routing reports it as unmapped instead of guessing.
        /// </summary>
        public readonly List<RoomDoor> Doors = new List<RoomDoor>();
        public Provenance Provenance;
    }

    public sealed class RoomDoor
    {
        /// <summary>Corridor node directly in front of this door; routes to the room end here.</summary>
        public string AccessNodeId;
        /// <summary>Door gap on the wall, or null while the exact position is unknown (nothing is drawn).</summary>
        public OpeningData Opening;
        public Provenance Provenance;
    }
}
