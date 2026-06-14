// Animated fountain spray — droplets launched up the centrepiece on ballistic
// arcs, recycled when they fall back to the water. Ported from the points
// animation in the web prototype's Fountain.tsx (local space; parent sits at the
// pool centre). Uses a small pool of emissive spheres (no particle shader needed).
using UnityEngine;

namespace CameraObscura
{
    public class FountainSpray : MonoBehaviour
    {
        const int COUNT = 24;
        const float G = 9.8f;
        const float WATER_Y = 0.34f;

        struct Seed { public float a, speed, vy, t, tier; }
        Seed[] _seeds;
        Transform[] _drops;

        void Awake()
        {
            var rng = new System.Random(12345);
            var mat = MaterialLibrary.Emissive(new Color(0.875f, 0.945f, 1f), 1.8f);
            _seeds = new Seed[COUNT];
            _drops = new Transform[COUNT];
            for (int i = 0; i < COUNT; i++)
            {
                _seeds[i] = new Seed
                {
                    a = (float)rng.NextDouble() * Mathf.PI * 2f,
                    speed = 0.4f + (float)rng.NextDouble() * 0.5f,
                    vy = 3.2f + (float)rng.NextDouble() * 1.8f,
                    t = (float)rng.NextDouble() * 2f,
                    tier = rng.NextDouble() < 0.6 ? 2.35f : 3.3f,
                };
                var d = GO.Sphere(transform, Vector3.zero, 0.035f, mat, "drop");
                _drops[i] = d.transform;
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int i = 0; i < COUNT; i++)
            {
                var s = _seeds[i];
                s.t += dt;
                float vy = s.vy - G * s.t;
                float y = s.tier + s.vy * s.t - 0.5f * G * s.t * s.t;
                if (y < WATER_Y || vy < -6f) s.t = 0f;
                float rad = 0.15f + s.speed * s.t * 0.9f;
                _seeds[i] = s;
                _drops[i].localPosition = new Vector3(
                    Mathf.Cos(s.a) * rad,
                    Mathf.Max(WATER_Y, y),
                    Mathf.Sin(s.a) * rad);
            }
        }
    }
}
