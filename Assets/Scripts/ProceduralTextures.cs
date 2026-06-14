// Canvas-style value-noise surfaces, ported from the web prototype's
// src/museum/materials.ts. Generates a tileable colour map and a derived normal
// map at runtime so the museum has hand-finished marble/plaster/sandstone with
// zero image assets. Deterministic (seeded) so the look is stable across runs.
using UnityEngine;

namespace CameraObscura
{
    public struct SurfaceMaps
    {
        public Texture2D color;
        public Texture2D normal;
    }

    public struct SurfaceOpts
    {
        public int size;
        public Vector3 baseRGB;     // 0..255
        public float amp;
        public float vein;          // 0 = none
        public Vector3 veinRGB;     // 0..255
        public float normalScale;
    }

    public static class ProceduralTextures
    {
        // A tiny seeded RNG so textures are identical every launch.
        class Rng
        {
            uint s;
            public Rng(uint seed) { s = seed == 0 ? 1u : seed; }
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xffffff) / (float)0x1000000; }
        }

        static System.Func<float, float, float> ValueNoise(int grid, Rng rng)
        {
            var v = new float[grid * grid];
            for (int i = 0; i < v.Length; i++) v[i] = rng.Next();
            return (u, w) =>
            {
                float x = u * grid, y = w * grid;
                int x0 = ((Mathf.FloorToInt(x) % grid) + grid) % grid;
                int y0 = ((Mathf.FloorToInt(y) % grid) + grid) % grid;
                int x1 = (x0 + 1) % grid;
                int y1 = (y0 + 1) % grid;
                float fx = x - Mathf.Floor(x);
                float fy = y - Mathf.Floor(y);
                System.Func<float, float> sm = t => t * t * (3 - 2 * t);
                float a = v[y0 * grid + x0], b = v[y0 * grid + x1];
                float c = v[y1 * grid + x0], d = v[y1 * grid + x1];
                float top = a + (b - a) * sm(fx);
                float bot = c + (d - c) * sm(fx);
                return top + (bot - top) * sm(fy);
            };
        }

        public static SurfaceMaps Build(SurfaceOpts o, uint seed)
        {
            int size = o.size > 0 ? o.size : 256;
            var rng = new Rng(seed);
            var oct1 = ValueNoise(5, rng);
            var oct2 = ValueNoise(13, rng);
            var oct3 = ValueNoise(37, rng);

            var H = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, w = y / (float)size;
                    float n = 0.55f * oct1(u, w) + 0.3f * oct2(u, w) + 0.15f * oct3(u, w);
                    if (o.vein > 0)
                    {
                        float t = oct1(u * 2 + 0.2f, w * 2) + 0.5f * oct2(u * 2, w * 2 + 0.4f);
                        float vein = Mathf.Pow(Mathf.Abs(Mathf.Sin((u * 6 + t) * Mathf.PI)), 14);
                        n = n * (1 - o.vein) + vein * o.vein;
                    }
                    H[y * size + x] = n;
                }

            var colPix = new Color32[size * size];
            var nrmPix = new Color32[size * size];
            float ns = o.normalScale > 0 ? o.normalScale : 2f;
            System.Func<int, int, float> at = (x, y) => H[(((y % size) + size) % size) * size + (((x % size) + size) % size)];

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float h = H[i];
                    float l = (h - 0.5f) * o.amp;
                    float vmix = o.vein > 0 ? Mathf.Pow(h, 3) * o.vein : 0f;
                    byte R = (byte)Mathf.Clamp((o.baseRGB.x + l * 255f) * (1 - vmix) + o.veinRGB.x * vmix, 0, 255);
                    byte G = (byte)Mathf.Clamp((o.baseRGB.y + l * 255f) * (1 - vmix) + o.veinRGB.y * vmix, 0, 255);
                    byte B = (byte)Mathf.Clamp((o.baseRGB.z + l * 255f) * (1 - vmix) + o.veinRGB.z * vmix, 0, 255);
                    colPix[i] = new Color32(R, G, B, 255);

                    float dx = (at(x + 1, y) - at(x - 1, y)) * ns;
                    float dy = (at(x, y + 1) - at(x, y - 1)) * ns;
                    float len = Mathf.Sqrt(dx * dx + dy * dy + 1f);
                    nrmPix[i] = new Color32(
                        (byte)(((-dx / len) * 0.5f + 0.5f) * 255f),
                        (byte)(((-dy / len) * 0.5f + 0.5f) * 255f),
                        (byte)((1f / len) * 255f), 255);
                }

            var color = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            color.wrapMode = TextureWrapMode.Repeat; color.anisoLevel = 8;
            color.SetPixels32(colPix); color.Apply(true);

            var normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            normal.wrapMode = TextureWrapMode.Repeat; normal.anisoLevel = 8;
            normal.SetPixels32(nrmPix); normal.Apply(true);

            return new SurfaceMaps { color = color, normal = normal };
        }
    }
}
