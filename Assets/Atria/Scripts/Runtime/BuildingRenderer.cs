using System.Collections;
using System.Collections.Generic;
using Atria.Core.Data;
using Atria.Core.Geometry;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace Atria.App
{
    [System.Serializable]
    public sealed class BuildingVisualSettings
    {
        public float WallThickness = 0.15f;
        public float DoorHeight = 2.1f;
        public float EntranceHeight = 2.5f;
        public float WallSignHeight = 1.6f;
        public float FlagSignHeight = 2.3f;
        public bool Ceiling = true;

        public Color32 FloorColor = new Color32(176, 172, 164, 255);
        public Color32 CeilingColor = new Color32(236, 236, 232, 255);
        public Color32 WallColor = new Color32(226, 219, 205, 255);
        public Color32 DoorColor = new Color32(120, 84, 56, 255);
        public Color32 FrameColor = new Color32(70, 70, 76, 255);
        public Color32 SignPlateColor = new Color32(22, 48, 92, 255);
        public Color32 SignTextColor = new Color32(255, 255, 255, 255);
        public Color32 EntranceColor = new Color32(70, 150, 90, 255);
        public Color32 GroundColor = new Color32(150, 165, 135, 255);
    }

    /// <summary>A door's placement in Unity world space, used to highlight the destination.</summary>
    public sealed class DoorVisual
    {
        public Vector3 Center;  // at floor level, on the wall line
        public Vector3 Inward;  // unit vector into the corridor
        public Vector3 Along;   // unit vector along the wall
        public float Width;
        public float WallHeight;
    }

    public sealed class BuildingView
    {
        public GameObject Root;
        public readonly Dictionary<string, DoorVisual> Doors = new Dictionary<string, DoorVisual>();
    }

    /// <summary>
    /// Procedurally turns <see cref="BuildingData"/> into low-poly geometry: floors, ceilings,
    /// walls (from <see cref="FloorLayout"/>), doors, entrance markers and room-number signs.
    /// Contains no navigation logic.
    /// </summary>
    public static class BuildingRenderer
    {
        public static BuildingView Build(BuildingData building, Transform parent, Material surface, Material signText, Font font, BuildingVisualSettings s)
        {
            var view = new BuildingView { Root = new GameObject("Building: " + building.BuildingId) };
            view.Root.transform.SetParent(parent, false);
            var bounds = new Bounds();
            bool hasBounds = false;
            float minElevation = float.MaxValue;

            foreach (FloorData floor in building.Floors)
            {
                FloorLayout layout = FloorLayout.Build(building, floor);
                var mb = new MeshBuilder();
                var floorRoot = new GameObject("Floor " + floor.Id).transform;
                floorRoot.SetParent(view.Root.transform, false);
                float y0 = floor.Elevation, h = floor.WallHeight;
                minElevation = Mathf.Min(minElevation, y0);

                foreach (SpaceData space in layout.Spaces)
                {
                    var pts = new List<Vector3>();
                    foreach (NVector2 p in space.Polygon) pts.Add(World(p, y0));
                    mb.AddPolygon(pts, Vector3.up, s.FloorColor);
                    if (s.Ceiling)
                    {
                        var top = new List<Vector3>();
                        foreach (Vector3 p in pts) top.Add(p + Vector3.up * h);
                        mb.AddPolygon(top, Vector3.down, s.CeilingColor);
                    }
                    foreach (Vector3 p in pts)
                    {
                        if (!hasBounds) { bounds = new Bounds(p, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(p);
                    }
                }

                foreach (WallSegment w in layout.Walls)
                    AddWallPiece(mb, World(w.A, y0), World(w.B, y0), Dir(w.Inward), 0f, h, s.WallThickness, s.WallColor);

                foreach (RoomData room in building.Rooms)
                {
                    if (room.FloorId != floor.Id || !layout.Doors.TryGetValue(room.Id, out OpeningPlacement door) || !door.Placed) continue;
                    DoorVisual dv = ToVisual(door, y0, h);
                    view.Doors[room.Id] = dv;
                    AddDoor(mb, dv, s);
                    AddRoomSigns(mb, floorRoot, dv, room.Number, signText, font, s);
                }

                foreach (EntranceData entrance in building.Entrances)
                {
                    if (!layout.Entrances.TryGetValue(entrance.Id, out OpeningPlacement opening) || !opening.Placed) continue;
                    DoorVisual ev = ToVisual(opening, y0, h);
                    AddWallPiece(mb, ev.Center - ev.Along * (ev.Width * 0.5f), ev.Center + ev.Along * (ev.Width * 0.5f), ev.Inward,
                        s.EntranceHeight, h, s.WallThickness, s.WallColor);
                    // Floor mat just inside the entrance.
                    Vector3 matCenter = ev.Center + ev.Inward * 0.75f + Vector3.up * 0.005f;
                    mb.AddBox(matCenter, ev.Along, ev.Inward, new Vector3(ev.Width, 0.01f, 1.4f), s.EntranceColor);
                    // Label above the opening, readable from inside the building.
                    CreateSign(floorRoot, entrance.Label, ev.Center + ev.Inward * 0.03f + Vector3.up * (s.EntranceHeight + 0.2f),
                        ev.Inward, Mathf.Max(1.5f, ev.Width), 0.18f, signText, font, s.SignTextColor);
                    mb.AddBox(ev.Center + ev.Inward * 0.015f + Vector3.up * (s.EntranceHeight + 0.06f), ev.Along, ev.Inward,
                        new Vector3(Mathf.Max(1.6f, ev.Width + 0.1f), 0.28f, 0.02f), s.EntranceColor);
                }

                MeshBuilder.CreateObject("Geometry", floorRoot, mb.ToMesh("Floor " + floor.Id), surface);
            }

            // Ground plane outside the building so open entrances do not look into a void.
            if (hasBounds)
            {
                var ground = new MeshBuilder();
                Vector3 c = bounds.center, e = bounds.extents + new Vector3(60f, 0f, 60f);
                float gy = minElevation - 0.03f;
                ground.AddFacing(new Vector3(c.x - e.x, gy, c.z - e.z), new Vector3(c.x + e.x, gy, c.z - e.z),
                    new Vector3(c.x + e.x, gy, c.z + e.z), new Vector3(c.x - e.x, gy, c.z + e.z), Vector3.up, s.GroundColor);
                MeshBuilder.CreateObject("Ground", view.Root.transform, ground.ToMesh("Ground"), surface);
            }
            return view;
        }

        /// <summary>Wall box whose inner face lies exactly on the wall line, spanning heights y0..y1 above the floor.</summary>
        private static void AddWallPiece(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 inward, float bottom, float top, float thickness, Color32 color)
        {
            Vector3 along = b - a;
            float len = along.magnitude;
            if (len < 1e-3f || top <= bottom) return;
            along /= len;
            Vector3 center = (a + b) * 0.5f - inward * (thickness * 0.5f) + Vector3.up * bottom;
            mb.AddBox(center, along, -inward, new Vector3(len, top - bottom, thickness), color);
        }

        private static void AddDoor(MeshBuilder mb, DoorVisual d, BuildingVisualSettings s)
        {
            float t = s.WallThickness;
            Vector3 a = d.Center - d.Along * (d.Width * 0.5f), b = d.Center + d.Along * (d.Width * 0.5f);
            AddWallPiece(mb, a, b, d.Inward, s.DoorHeight, d.WallHeight, t, s.WallColor); // lintel
            // Closed door panel, recessed to the middle of the wall so the opening reads as a doorway.
            mb.AddBox(d.Center - d.Inward * (t * 0.5f), d.Along, d.Inward, new Vector3(d.Width - 0.04f, s.DoorHeight - 0.02f, 0.05f), s.DoorColor);
            // Dark frame on both jambs and the head.
            const float frame = 0.06f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 jamb = d.Center + d.Along * (side * (d.Width * 0.5f - frame * 0.5f)) - d.Inward * (t * 0.5f - 0.01f);
                mb.AddBox(jamb, d.Along, d.Inward, new Vector3(frame, s.DoorHeight, t + 0.02f), s.FrameColor);
            }
            mb.AddBox(d.Center - d.Inward * (t * 0.5f - 0.01f) + Vector3.up * (s.DoorHeight - frame), d.Along, d.Inward,
                new Vector3(d.Width, frame, t + 0.02f), s.FrameColor);
        }

        private static void AddRoomSigns(MeshBuilder mb, Transform parent, DoorVisual d, string number, Material signText, Font font, BuildingVisualSettings s)
        {
            // Wall plate beside the door, read when facing the door.
            Vector3 plate = d.Center + d.Along * (d.Width * 0.5f + 0.4f);
            mb.AddBox(plate + d.Inward * 0.01f + Vector3.up * (s.WallSignHeight - 0.13f), d.Along, d.Inward, new Vector3(0.62f, 0.26f, 0.02f), s.SignPlateColor);
            CreateSign(parent, number, plate + d.Inward * 0.025f + Vector3.up * s.WallSignHeight, d.Inward, 0.56f, 0.15f, signText, font, s.SignTextColor);

            // Double-sided flag sign projecting into the corridor, read while walking along it.
            Vector3 root = d.Center + d.Along * (d.Width * 0.5f + 0.05f);
            Vector3 flagCenter = root + d.Inward * 0.35f;
            mb.AddBox(flagCenter + Vector3.up * (s.FlagSignHeight - 0.14f), d.Inward, d.Along, new Vector3(0.66f, 0.28f, 0.03f), s.SignPlateColor);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 facing = d.Along * side;
                CreateSign(parent, number, flagCenter + facing * 0.017f + Vector3.up * s.FlagSignHeight, facing, 0.6f, 0.16f, signText, font, s.SignTextColor);
            }
        }

        /// <summary>Creates a TextMesh label whose face points along <paramref name="facing"/> (towards the reader).</summary>
        public static GameObject CreateSign(Transform parent, string text, Vector3 position, Vector3 facing, float maxWidth, float maxHeight, Material material, Font font, Color color)
        {
            var go = new GameObject("Sign " + text);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            // TextMesh is legible when viewed along its +Z axis, i.e. by someone looking opposite to `facing`.
            go.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.02f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<SignFitter>().Configure(maxWidth, maxHeight);
            return go;
        }

        private static DoorVisual ToVisual(OpeningPlacement p, float elevation, float wallHeight)
        {
            Vector3 a = World(p.A, elevation), b = World(p.B, elevation);
            return new DoorVisual
            {
                Center = (a + b) * 0.5f,
                Inward = Dir(p.Inward),
                Along = (b - a).normalized,
                Width = Vector3.Distance(a, b),
                WallHeight = wallHeight,
            };
        }

        public static Vector3 World(NVector2 plan, float elevation) => new Vector3(plan.X, elevation, plan.Y);
        private static Vector3 Dir(NVector2 plan) => new Vector3(plan.X, 0f, plan.Y).normalized;
    }

    /// <summary>
    /// Scales a TextMesh to fit its sign once the text mesh exists, so label size does not
    /// depend on font metrics or string length.
    /// </summary>
    public sealed class SignFitter : MonoBehaviour
    {
        private float _maxWidth, _maxHeight;

        public void Configure(float maxWidth, float maxHeight)
        {
            _maxWidth = maxWidth;
            _maxHeight = maxHeight;
        }

        private IEnumerator Start()
        {
            yield return null; // TextMesh builds its mesh lazily
            var r = GetComponent<MeshRenderer>();
            Vector3 size = r != null ? r.localBounds.size : Vector3.zero;
            if (size.x > 1e-5f && size.y > 1e-5f)
            {
                float scale = Mathf.Min(_maxWidth / size.x, _maxHeight / size.y);
                transform.localScale = Vector3.one * scale;
            }
            Destroy(this);
        }
    }
}
