using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Atria.App
{
    /// <summary>
    /// Accumulates flat-shaded, vertex-coloured geometry into one mesh so the whole building
    /// renders with a single material and very few draw calls.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();

        public int VertexCount => _vertices.Count;

        /// <summary>
        /// Adds a quad whose corners run clockwise when viewed from the side it faces (Unity's
        /// front-face winding). For such a quad Cross(b - a, d - a) points towards the viewer.
        /// </summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int i = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
            for (int k = 0; k < 4; k++) { _normals.Add(n); _colors.Add(color); }
            _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
            _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 3);
        }

        /// <summary>Adds a convex polygon (fan triangulated) facing <paramref name="up"/>-wards.</summary>
        public void AddPolygon(IList<Vector3> points, Vector3 normal, Color32 color)
        {
            if (points.Count < 3) return;
            int start = _vertices.Count;
            foreach (Vector3 p in points)
            {
                _vertices.Add(p);
                _normals.Add(normal);
                _colors.Add(color);
            }
            // Decide winding so the face is visible from the normal's side.
            Vector3 faceNormal = Vector3.Cross(points[1] - points[0], points[2] - points[0]);
            bool flip = Vector3.Dot(faceNormal, normal) < 0f; // given order is counter-clockwise from the normal side
            for (int k = 1; k + 1 < points.Count; k++)
            {
                if (flip) { _triangles.Add(start); _triangles.Add(start + k + 1); _triangles.Add(start + k); }
                else { _triangles.Add(start); _triangles.Add(start + k); _triangles.Add(start + k + 1); }
            }
        }

        /// <summary>
        /// Adds an oriented box. <paramref name="right"/> and <paramref name="forward"/> must be
        /// horizontal unit vectors; <paramref name="size"/> is (width along right, height, depth along forward).
        /// </summary>
        public void AddBox(Vector3 baseCenter, Vector3 right, Vector3 forward, Vector3 size, Color32 color, bool includeBottom = false)
        {
            Vector3 up = Vector3.up;
            Vector3 r = right * (size.x * 0.5f), f = forward * (size.z * 0.5f), h = up * size.y;
            Vector3 b0 = baseCenter - r - f, b1 = baseCenter + r - f, b2 = baseCenter + r + f, b3 = baseCenter - r + f;
            Vector3 t0 = b0 + h, t1 = b1 + h, t2 = b2 + h, t3 = b3 + h;

            AddFacing(b0, b1, t1, t0, -forward, color); // back
            AddFacing(b1, b2, t2, t1, right, color);    // right
            AddFacing(b2, b3, t3, t2, forward, color);  // front
            AddFacing(b3, b0, t0, t3, -right, color);   // left
            AddFacing(t0, t1, t2, t3, up, color);       // top
            if (includeBottom) AddFacing(b0, b1, b2, b3, -up, color);
        }

        /// <summary>Adds a quad and makes sure it faces <paramref name="outward"/>.</summary>
        public void AddFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color32 color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), outward) > 0f) AddQuad(a, b, c, d, color);
            else AddQuad(a, d, c, b, color);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true); // geometry never changes after creation: free the CPU copy
            return mesh;
        }

        public static GameObject CreateObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }
    }
}
