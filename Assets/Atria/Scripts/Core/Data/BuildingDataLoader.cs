using System;
using System.Collections.Generic;
using System.Numerics;
using Atria.Core.Geometry;
using Atria.Core.Json;

namespace Atria.Core.Data
{
    /// <summary>Converts the JSON building file (docs/DATA_FORMAT.md) into <see cref="BuildingData"/>.</summary>
    public static class BuildingDataLoader
    {
        public const int SupportedSchemaVersion = 1;

        public static BuildingData FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root))
                throw new FormatException("Building file must be a JSON object");

            var b = new BuildingData
            {
                SchemaVersion = (int)Num(root, "schemaVersion", 0),
                BuildingId = Str(root, "buildingId"),
                DisplayName = Str(root, "displayName") ?? Str(root, "buildingId"),
                DataStatus = (Str(root, "dataStatus") ?? "sample").ToLowerInvariant(),
                Notes = Str(root, "notes"),
                WalkingSpeedMps = (float)Num(root, "walkingSpeedMps", 1.2),
                DefaultCorridorWidth = (float)Num(root, "defaultCorridorWidth", 2.4),
                DefaultDoorWidth = (float)Num(root, "defaultDoorWidth", 1.0),
            };
            if (b.SchemaVersion != SupportedSchemaVersion)
                throw new FormatException($"Unsupported schemaVersion {b.SchemaVersion} (expected {SupportedSchemaVersion})");

            foreach (var f in Objects(root, "floors"))
            {
                b.Floors.Add(new FloorData
                {
                    Id = Req(f, "id"),
                    Name = Str(f, "name") ?? Req(f, "id"),
                    Elevation = (float)Num(f, "elevation", 0),
                    WallHeight = (float)Num(f, "wallHeight", 3.0),
                });
            }

            foreach (var s in Objects(root, "spaces"))
            {
                string id = Req(s, "id");
                var polygons = new List<List<Vector2>>();
                if (Str(s, "shape") == "hexRing")
                {
                    // A hexagonal corridor around a central void is not convex: expand it into six convex pieces.
                    if (!s.TryGetValue("center", out object c)) throw new FormatException($"hexRing space '{id}' needs a center");
                    polygons.AddRange(Polygon2D.HexRing(Point(c, "space " + id), (float)Num(s, "outerRadius", 0),
                        (float)Num(s, "innerRadius", 0), (float)Num(s, "rotation", 0)));
                }
                else if (Str(s, "shape") == "corridor")
                {
                    // Straight corridor given by its centre line: a rectangle at any angle.
                    if (!s.TryGetValue("from", out object f) || !s.TryGetValue("to", out object t)) throw new FormatException($"corridor space '{id}' needs from and to");
                    Vector2 a = Point(f, "space " + id), c = Point(t, "space " + id);
                    float half = (float)Num(s, "width", b.DefaultCorridorWidth) * 0.5f;
                    if (Vector2.Distance(a, c) < 1e-3f || half <= 0f) throw new FormatException($"corridor space '{id}' needs distinct ends and a positive width");
                    Vector2 dir = Vector2.Normalize(c - a), side = new Vector2(-dir.Y, dir.X) * half;
                    polygons.Add(new List<Vector2> { a - side, c - side, c + side, a + side });
                }
                else
                {
                    var poly = new List<Vector2>();
                    if (s.TryGetValue("polygon", out object raw) && raw is List<object> pts)
                        foreach (object p in pts) poly.Add(Point(p, "space " + id));
                    polygons.Add(poly);
                }

                for (int k = 0; k < polygons.Count; k++)
                {
                    var space = new SpaceData
                    {
                        Id = polygons.Count == 1 ? id : id + "#" + k,
                        FloorId = Req(s, "floor"),
                        Kind = Str(s, "kind") ?? "corridor",
                        Provenance = Prov(s),
                    };
                    space.Polygon.AddRange(polygons[k]);
                    Polygon2D.MakeCounterClockwise(space.Polygon);
                    b.Spaces.Add(space);
                }
            }

            foreach (var n in Objects(root, "nodes"))
            {
                string floorId = Req(n, "floor");
                FloorData floor = b.FindFloor(floorId);
                float y = n.ContainsKey("y") ? (float)Num(n, "y", 0) : (floor?.Elevation ?? 0f);
                b.Nodes.Add(new NodeData
                {
                    Id = Req(n, "id"),
                    FloorId = floorId,
                    Kind = Str(n, "kind") ?? "corridor",
                    Position = new Vector3((float)Num(n, "x", 0), y, (float)Num(n, "z", 0)),
                    Provenance = Prov(n),
                });
            }

            foreach (var e in Objects(root, "edges"))
            {
                b.Edges.Add(new EdgeData
                {
                    From = Req(e, "from"),
                    To = Req(e, "to"),
                    Navigable = Bool(e, "navigable", true),
                    Kind = Str(e, "kind") ?? "corridor",
                    MeasuredDistance = e.ContainsKey("distance") ? (float?)Num(e, "distance", 0) : null,
                    Width = e.ContainsKey("width") ? (float?)Num(e, "width", 0) : null,
                    Provenance = Prov(e),
                });
            }

            foreach (var en in Objects(root, "entrances"))
            {
                b.Entrances.Add(new EntranceData
                {
                    Id = Req(en, "id"),
                    Label = Str(en, "label") ?? Req(en, "id"),
                    NodeId = Req(en, "node"),
                    Opening = Opening(en, "opening", b.DefaultDoorWidth),
                    Provenance = Prov(en),
                });
            }

            foreach (var r in Objects(root, "rooms"))
            {
                var room = new RoomData
                {
                    Id = Req(r, "id"),
                    Number = Str(r, "number") ?? Req(r, "id"),
                    Name = Str(r, "name"),
                    FloorId = Req(r, "floor"),
                    Provenance = Prov(r),
                };
                if (r.ContainsKey("doors"))
                {
                    foreach (var d in Objects(r, "doors"))
                        room.Doors.Add(new RoomDoor
                        {
                            AccessNodeId = Req(d, "accessNode"),
                            // Position stays unknown (null) unless both coordinates were recorded.
                            Opening = d.ContainsKey("x") && d.ContainsKey("z") ? OpeningFrom(d, b.DefaultDoorWidth) : null,
                            Provenance = d.ContainsKey("provenance") ? Prov(d) : room.Provenance,
                        });
                }
                else if (Str(r, "accessNode") != null)
                {
                    // Single-door shorthand: "accessNode" + optional "door": {x, z, width}.
                    room.Doors.Add(new RoomDoor { AccessNodeId = Str(r, "accessNode"), Opening = Opening(r, "door", b.DefaultDoorWidth), Provenance = room.Provenance });
                }
                if (r.TryGetValue("aliases", out object al) && al is List<object> aliases)
                    foreach (object a in aliases)
                        if (a is string s) room.Aliases.Add(s);
                b.Rooms.Add(room);
            }

            return b;
        }

        private static IEnumerable<Dictionary<string, object>> Objects(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out object v) || v == null) yield break;
            if (!(v is List<object> list)) throw new FormatException($"'{key}' must be an array");
            foreach (object item in list)
            {
                if (!(item is Dictionary<string, object> d)) throw new FormatException($"Entries of '{key}' must be objects");
                yield return d;
            }
        }

        private static string Str(Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out object v) ? v as string : null;

        private static string Req(Dictionary<string, object> o, string key)
        {
            string s = Str(o, key);
            if (string.IsNullOrEmpty(s)) throw new FormatException($"Missing required string '{key}'");
            return s;
        }

        private static double Num(Dictionary<string, object> o, string key, double fallback)
        {
            if (!o.TryGetValue(key, out object v) || v == null) return fallback;
            if (v is double d) return d;
            throw new FormatException($"'{key}' must be a number");
        }

        private static bool Bool(Dictionary<string, object> o, string key, bool fallback) =>
            o.TryGetValue(key, out object v) && v is bool b ? b : fallback;

        private static Provenance Prov(Dictionary<string, object> o)
        {
            string s = Str(o, "provenance");
            if (string.IsNullOrEmpty(s)) return Provenance.Placeholder; // unknown data is never treated as verified
            switch (s.ToLowerInvariant())
            {
                case "measured": return Provenance.Measured;
                case "estimated": return Provenance.Estimated;
                case "placeholder": return Provenance.Placeholder;
                default: throw new FormatException($"Unknown provenance '{s}'");
            }
        }

        private static Vector2 Point(object p, string context)
        {
            if (p is List<object> xy && xy.Count == 2 && xy[0] is double x && xy[1] is double z)
                return new Vector2((float)x, (float)z);
            throw new FormatException($"Expected [x, z] point in {context}");
        }

        private static OpeningData Opening(Dictionary<string, object> o, string key, float defaultWidth)
        {
            return o.TryGetValue(key, out object v) && v is Dictionary<string, object> d ? OpeningFrom(d, defaultWidth) : null;
        }

        private static OpeningData OpeningFrom(Dictionary<string, object> d, float defaultWidth)
        {
            return new OpeningData
            {
                Center = new Vector2((float)Num(d, "x", 0), (float)Num(d, "z", 0)),
                Width = (float)Num(d, "width", defaultWidth),
            };
        }
    }
}
