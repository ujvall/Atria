using System.Collections.Generic;
using Atria.Core.Navigation;
using UnityEngine;

namespace Atria.App
{
    /// <summary>Floor guide line along the current route plus a highlight around the destination door.</summary>
    public sealed class RouteVisuals
    {
        private readonly Transform _parent;
        private readonly Material _material;
        private GameObject _object;
        private Mesh _mesh;

        public Color32 LineColor = new Color32(30, 120, 230, 255);
        public Color32 DestinationColor = new Color32(40, 190, 90, 255);
        public float LineWidth = 0.3f;

        public RouteVisuals(Transform parent, Material material)
        {
            _parent = parent;
            _material = material;
        }

        public void Show(RoutePath path, DoorVisual destinationDoor)
        {
            Clear();
            var mb = new MeshBuilder();
            IReadOnlyList<System.Numerics.Vector3> pts = path.Points;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 a = ToUnity(pts[i]) + Vector3.up * 0.012f, b = ToUnity(pts[i + 1]) + Vector3.up * 0.012f;
                Vector3 dir = b - a;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-8f) continue;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * (LineWidth * 0.5f);
                mb.AddFacing(a - side, b - side, b + side, a + side, Vector3.up, LineColor);
            }

            if (destinationDoor != null)
            {
                DoorVisual d = destinationDoor;
                // Green frame proud of the wall around the destination doorway, plus a floor patch.
                const float bar = 0.08f;
                Vector3 face = d.Center + d.Inward * 0.03f;
                foreach (float s in new[] { -1f, 1f })
                    mb.AddBox(face + d.Along * (s * (d.Width * 0.5f + bar * 0.5f)), d.Along, d.Inward, new Vector3(bar, 2.2f, 0.04f), DestinationColor);
                mb.AddBox(face + Vector3.up * 2.2f, d.Along, d.Inward, new Vector3(d.Width + 2f * bar, bar, 0.04f), DestinationColor);
                mb.AddBox(d.Center + d.Inward * 0.6f + Vector3.up * 0.008f, d.Along, d.Inward, new Vector3(d.Width + 0.2f, 0.008f, 1.0f), DestinationColor);
            }

            _mesh = mb.ToMesh("Route");
            _object = MeshBuilder.CreateObject("Route", _parent, _mesh, _material);
        }

        public void Clear()
        {
            if (_object != null) Object.Destroy(_object);
            if (_mesh != null) Object.Destroy(_mesh); // meshes are not freed with their GameObject
            _object = null;
            _mesh = null;
        }

        public static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
