// Procedural meshes that aren't built-in primitives: barrel-vault ceilings (with
// half-moon lunette end caps), tori (fountain rim, chandelier rings) and a quad
// with a guaranteed +Z facing (used for photos, plaques, signs and floor runners
// so orientation is unambiguous regardless of Unity's primitive conventions).
using System.Collections.Generic;
using UnityEngine;

namespace CameraObscura
{
    public static class MeshFactory
    {
        static Mesh _quad;

        /// <summary>Unit quad in the local XY plane, centred, FRONT FACE toward +Z
        /// (so a group oriented with local +Z toward the viewer shows it). UVs are
        /// set so an image reads un-mirrored when viewed from the +Z side.</summary>
        public static Mesh Quad()
        {
            if (_quad != null) return _quad;
            var m = new Mesh { name = "co_quad" };
            //        v0 BL        v1 BR        v2 TR        v3 TL
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0),
            };
            // Viewed from +Z (camera looking −Z), +X appears on the viewer's left,
            // so put U=0 on the +X verts to keep the image un-mirrored.
            m.uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            // Double-sided (front winding + reversed back) so it's never culled; the
            // viewer on the +Z side sees the correctly-oriented, un-mirrored front.
            m.triangles = new[] { 0, 2, 1, 0, 3, 2, /* back */ 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            _quad = m;
            return m;
        }

        /// <summary>A barrel vault: semicircular shell (radius R, spanning local X
        /// in [-R,R], top at +Y) extruded along local Z by <paramref name="length"/>,
        /// closed at both ends by lunette half-discs. Normals face inward/down.</summary>
        public static Mesh BarrelVault(float R, float length, int seg = 24)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            float hz = length / 2f;

            // Shell: ring of points over the semicircle at each z end.
            int ringStart = verts.Count;
            for (int e = 0; e < 2; e++)
            {
                float z = e == 0 ? -hz : hz;
                for (int j = 0; j <= seg; j++)
                {
                    float a = Mathf.PI * j / seg;       // 0 → π  (from +X over the top to −X)
                    float cx = Mathf.Cos(a) * R, cy = Mathf.Sin(a) * R;
                    verts.Add(new Vector3(cx, cy, z));
                    norms.Add(new Vector3(-Mathf.Cos(a), -Mathf.Sin(a), 0)); // inward
                }
            }
            int per = seg + 1;
            for (int j = 0; j < seg; j++)
            {
                int a0 = ringStart + j;
                int a1 = ringStart + j + 1;
                int b0 = ringStart + per + j;
                int b1 = ringStart + per + j + 1;
                // inward-facing winding
                tris.Add(a0); tris.Add(a1); tris.Add(b0);
                tris.Add(a1); tris.Add(b1); tris.Add(b0);
            }

            // Lunette caps (half discs) at each end, facing inward along ∓Z.
            for (int e = 0; e < 2; e++)
            {
                float z = e == 0 ? -hz : hz;
                Vector3 nrm = e == 0 ? Vector3.forward : Vector3.back; // inward
                int center = verts.Count;
                verts.Add(new Vector3(0, 0, z)); norms.Add(nrm);
                int arcStart = verts.Count;
                for (int j = 0; j <= seg; j++)
                {
                    float a = Mathf.PI * j / seg;
                    verts.Add(new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, z));
                    norms.Add(nrm);
                }
                for (int j = 0; j < seg; j++)
                {
                    int p0 = arcStart + j, p1 = arcStart + j + 1;
                    if (e == 0) { tris.Add(center); tris.Add(p0); tris.Add(p1); }
                    else { tris.Add(center); tris.Add(p1); tris.Add(p0); }
                }
            }

            var m = new Mesh { name = "co_vault" };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Torus(float radius, float tube, int radialSeg = 28, int tubeSeg = 12)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= radialSeg; i++)
            {
                float u = (float)i / radialSeg * Mathf.PI * 2f;
                Vector3 center = new Vector3(Mathf.Cos(u) * radius, 0, Mathf.Sin(u) * radius);
                for (int j = 0; j <= tubeSeg; j++)
                {
                    float v = (float)j / tubeSeg * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    verts.Add(center + dir * tube);
                    norms.Add(dir);
                }
            }
            int ring = tubeSeg + 1;
            for (int i = 0; i < radialSeg; i++)
                for (int j = 0; j < tubeSeg; j++)
                {
                    int a = i * ring + j;
                    int b = (i + 1) * ring + j;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            var m = new Mesh { name = "co_torus" };
            m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Small helpers for spawning primitive geometry without stray colliders.</summary>
    public static class GO
    {
        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material mat, string name = "box", bool collider = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Strip(g, collider);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            SetMat(g, mat);
            return g;
        }

        public static GameObject Cylinder(Transform parent, Vector3 pos, float radius, float height, Material mat, string name = "cyl")
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            Strip(g, false);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = new Vector3(radius * 2, height / 2f, radius * 2);
            SetMat(g, mat);
            return g;
        }

        public static GameObject Sphere(Transform parent, Vector3 pos, float radius, Material mat, string name = "sphere")
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            Strip(g, false);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = Vector3.one * radius * 2;
            SetMat(g, mat);
            return g;
        }

        public static GameObject MeshObj(Transform parent, Vector3 pos, Mesh mesh, Material mat, string name = "mesh")
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        /// <summary>A flat quad facing local +Z (photos, plaques, signs, runners).</summary>
        public static GameObject Quad(Transform parent, Vector3 pos, Vector2 size, Material mat, string name = "quad")
        {
            var g = MeshObj(parent, pos, MeshFactory.Quad(), mat, name);
            g.transform.localScale = new Vector3(size.x, size.y, 1f);
            return g;
        }

        static void Strip(GameObject g, bool keepCollider)
        {
            var c = g.GetComponent<Collider>();
            if (c != null && !keepCollider) Object.Destroy(c);
        }

        static void SetMat(GameObject g, Material mat)
        {
            var r = g.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = mat;
        }
    }
}
