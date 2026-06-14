// Procedural décor — chandeliers, marble busts, potted plants, the tiered atrium
// fountain and the arched glass roof. Ports the look of the web prototype's
// Chandelier / Decor / Fountain / GlassRoof components with procedural geometry
// only (no GLB dependency, so it always builds; the scanned-bust and Khronos
// plant GLBs still ship in StreamingAssets/models for an optional upgrade).
using UnityEngine;

namespace CameraObscura
{
    public static class DecorBuilder
    {
        static Material _chainMat, _ringMat, _bulbMat, _dropMat;

        public static void Chandelier(Transform parent, Vector3 pos, Color accent)
        {
            if (_chainMat == null)
            {
                _chainMat = MaterialLibrary.Lit(new Color(0.16f, 0.14f, 0.10f), 0.6f, 0.9f);
                _ringMat = MaterialLibrary.Lit(new Color(0.227f, 0.184f, 0.11f), 0.7f, 0.95f);
                _bulbMat = MaterialLibrary.Emissive(new Color(1f, 0.85f, 0.63f), 4f);
            }
            var g = new GameObject("chandelier").transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            const float R = 0.55f;

            GO.Cylinder(g, new Vector3(0, 0.9f, 0), 0.015f, 1.8f, _chainMat, "chain");
            GO.MeshObj(g, new Vector3(0, 0f, 0), MeshFactory.Torus(R, 0.03f, 24, 8), _ringMat, "ring0");
            GO.MeshObj(g, new Vector3(0, -0.35f, 0), MeshFactory.Torus(R * 0.6f, 0.03f, 24, 8), _ringMat, "ring1");
            for (int i = 0; i < 8; i++)
            {
                float a = (i / 8f) * Mathf.PI * 2f;
                GO.Sphere(g, new Vector3(Mathf.Cos(a) * R, 0.06f, Mathf.Sin(a) * R), 0.07f, _bulbMat, "bulb");
            }
            var drop = MaterialLibrary.Emissive(accent, 2.5f);
            GO.Sphere(g, new Vector3(0, -0.55f, 0), 0.1f, drop, "drop");
        }

        static readonly string[] StatueModels = { "statue_draped", "statue_seated", "bust_classical", "urn" };

        public static void Sculpture(Transform parent, Vector3 pos, float rotYDeg, int kind)
        {
            string model = StatueModels[kind % StatueModels.Length];
            if (ModelLibrary.Available(model))
            {
                var g = new GameObject("statue").transform;
                g.SetParent(parent, false);
                g.localPosition = pos;
                g.localRotation = Quaternion.Euler(0, rotYDeg, 0);
                bool tall = model == "statue_draped" || model == "statue_seated";
                float pedH = tall ? 1.4f : 3.2f;              // full figures get a low plinth; busts/urns a tall pedestal
                float modelH = tall ? 5.5f : (model == "urn" ? 3.4f : 2.6f);
                GO.Box(g, new Vector3(0, 0.25f, 0), new Vector3(2.4f, 0.5f, 2.4f), MaterialLibrary.Nero, "plinth");
                GO.Cylinder(g, new Vector3(0, pedH / 2f + 0.5f, 0), 0.85f, pedH, MaterialLibrary.MarblePolished, "pedestal");
                float topY = pedH + 0.5f;
                ModelLibrary.Place(g, model, new Vector3(0, topY, 0), 0f, modelH);
                return;
            }
            SculptureProcedural(parent, pos, rotYDeg, kind);
        }

        static void SculptureProcedural(Transform parent, Vector3 pos, float rotYDeg, int kind)
        {
            var g = new GameObject("sculpture").transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, rotYDeg, 0);
            float headY = 1.55f + (kind % 2) * 0.1f;
            var marble = MaterialLibrary.Marble;
            var dark = MaterialLibrary.DarkStone;

            GO.Box(g, new Vector3(0, 0.08f, 0), new Vector3(0.78f, 0.16f, 0.78f), dark, "plinth");
            GO.Cylinder(g, new Vector3(0, 0.66f, 0), 0.31f, 1.0f, marble, "pedestal");
            GO.Box(g, new Vector3(0, 1.2f, 0), new Vector3(0.66f, 0.12f, 0.5f), marble, "cap");
            Squash(GO.Sphere(g, new Vector3(0, 1.42f, 0), 0.34f, marble, "chest"), new Vector3(1f, 0.7f, 0.7f));
            GO.Cylinder(g, new Vector3(0, 1.6f, 0.02f), 0.115f, 0.2f, marble, "neck");
            Squash(GO.Sphere(g, new Vector3(0, headY + 0.16f, 0.03f), 0.2f, marble, "head"), new Vector3(0.92f, 1.1f, 1f));
        }

        public static void Plant(Transform parent, Vector3 pos, float scale)
        {
            var g = new GameObject("plant").transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            g.localScale = Vector3.one * scale;
            GO.Cylinder(g, new Vector3(0, 0.28f, 0), 0.3f, 0.56f, MaterialLibrary.Terracotta, "pot");
            GO.Cylinder(g, new Vector3(0, 0.55f, 0), 0.3f, 0.04f, MaterialLibrary.DarkStone, "soil");
            var rng = new System.Random(pos.GetHashCode());
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float t = 0.3f + (float)rng.NextDouble() * 0.5f;
                float h = 0.7f + (float)rng.NextDouble() * 0.7f;
                var blade = GO.Cylinder(g, new Vector3(Mathf.Cos(a) * t, 0.6f + h / 2f, Mathf.Sin(a) * t),
                    0.04f, h, (i % 2 == 0) ? MaterialLibrary.Leaf : MaterialLibrary.Leaf2, "blade");
                blade.transform.localRotation = Quaternion.Euler(t * 34f, a * Mathf.Rad2Deg, 0);
            }
        }

        public static void Fountain(Transform parent, Vector3 pos, float r)
        {
            var g = new GameObject("fountain").transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            var marble = MaterialLibrary.Marble;

            // Reflecting pool: dark mirror water + a low marble kerb.
            GO.Cylinder(g, new Vector3(0, 0.1f, 0), r, 0.1f, MaterialLibrary.Water, "water");
            GO.MeshObj(g, new Vector3(0, 0.2f, 0), MeshFactory.Torus(r, 0.4f, 64, 14), marble, "kerb");

            // Physical barrier so the player walks around the pool.
            var col = new GameObject("pool-collider");
            col.transform.SetParent(g, false);
            var cc = col.AddComponent<CapsuleCollider>();
            cc.radius = r; cc.height = 4f; cc.center = new Vector3(0, 1.6f, 0); cc.direction = 1;

            // Animated spray.
            var spray = new GameObject("spray");
            spray.transform.SetParent(g, false);
            spray.AddComponent<FountainSpray>();

            // Meshy hero fountain centrepiece (procedural tiers as fallback).
            if (ModelLibrary.Place(g, "fountain", new Vector3(0, 0.15f, 0), 0f, 8f) != null) return;

            GO.Cylinder(g, new Vector3(0, 0.45f, 0), r * 0.55f, 0.9f, MaterialLibrary.DarkStone, "basin");
            GO.Cylinder(g, new Vector3(0, 1.8f, 0), 0.84f, 2.2f, marble, "t1");
            GO.Cylinder(g, new Vector3(0, 3.0f, 0), 2.5f, 0.24f, marble, "t2");
            GO.Cylinder(g, new Vector3(0, 4.2f, 0), 0.54f, 1.8f, marble, "t3");
            GO.Cylinder(g, new Vector3(0, 5.1f, 0), 1.4f, 0.2f, marble, "t4");
            GO.Sphere(g, new Vector3(0, 6.3f, 0), 0.36f, marble, "finial");
        }

        /// <summary>A fluted column (Meshy model, procedural cylinder fallback).</summary>
        public static void Column(Transform parent, Vector3 pos, float height)
        {
            if (ModelLibrary.Place(parent, "column", pos, 0f, height) != null) return;
            var g = new GameObject("column").transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            GO.Box(g, new Vector3(0, 0.15f, 0), new Vector3(1.0f, 0.3f, 1.0f), MaterialLibrary.Nero, "base");
            GO.Cylinder(g, new Vector3(0, height * 0.5f, 0), 0.42f, height * 0.9f, MaterialLibrary.MarblePolished, "shaft");
            GO.Box(g, new Vector3(0, height - 0.2f, 0), new Vector3(1.0f, 0.4f, 1.0f), MaterialLibrary.Botticino, "capital");
            GO.MeshObj(g, new Vector3(0, height - 0.45f, 0), MeshFactory.Torus(0.5f, 0.06f, 24, 8), MaterialLibrary.Gold, "astragal");
        }

        /// <summary>A grand chandelier (Meshy model, scaled procedural fallback).</summary>
        public static void GrandChandelier(Transform parent, Vector3 pos, float dropHeight)
        {
            if (ModelLibrary.Available("chandelier"))
            {
                // Chain from the ceiling.
                GO.Cylinder(parent, pos + new Vector3(0, dropHeight * 0.5f, 0), 0.08f, dropHeight, MaterialLibrary.Bronze, "chain");
                ModelLibrary.Place(parent, "chandelier", pos, 0f, 6f);
                return;
            }
            Chandelier(parent, pos, new Color(0.79f, 0.64f, 0.29f));
        }

        /// <summary>A gallery bench (Meshy model only; skipped if unavailable).</summary>
        public static void Bench(Transform parent, Vector3 pos, float rotYDeg)
        {
            ModelLibrary.Place(parent, "bench", pos, rotYDeg, 1.6f);
        }

        public static void GlassRoof(Transform parent, float minX, float maxX, float half, float springY)
        {
            float cx = (minX + maxX) / 2f;
            float len = maxX - minX;
            var g = new GameObject("glass-roof").transform;
            g.SetParent(parent, false);

            // Glass barrel along X (rotate the Z-extruded vault 90° about Y).
            var vault = MeshFactory.BarrelVault(half, len, 28);
            var go = GO.MeshObj(g, new Vector3(cx, springY, 0), vault, MaterialLibrary.Glass, "glass");
            go.transform.localRotation = Quaternion.Euler(0, 90, 0);

            // Ridge beam + tie beams (bronze structure).
            GO.Box(g, new Vector3(cx, springY + half, 0), new Vector3(len, 0.12f, 0.12f), MaterialLibrary.Bronze, "ridge");
            for (float x = minX + 3; x <= maxX - 3; x += 6)
            {
                GO.Box(g, new Vector3(x, springY + 0.06f, 0), new Vector3(0.12f, 0.12f, half * 2), MaterialLibrary.Bronze, "tie");
            }

            // Warm fill light under the apex (soft; the main sun is the MoodController's).
            var lightGo = new GameObject("roof-light");
            lightGo.transform.SetParent(g, false);
            lightGo.transform.localPosition = new Vector3(cx, springY + half - 1f, 0);
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.92f, 0.95f, 1f);
            pl.intensity = 2.2f;
            pl.range = 45f;
            pl.shadows = LightShadows.None;
        }

        static void Squash(GameObject sphere, Vector3 factor)
        {
            // GO.Sphere set localScale to uniform diameter; multiply per-axis.
            var s = sphere.transform.localScale;
            sphere.transform.localScale = new Vector3(s.x * factor.x, s.y * factor.y, s.z * factor.z);
        }
    }
}
