// Shared URP materials for the whole museum. Built once at boot from the
// procedural marble/plaster/sandstone surfaces. Photos use the Unlit shader so
// they display at true brightness regardless of lighting (the carried-over
// pitfall: photographs must be unlit/emissive). Glow elements use Lit emission
// so the Bloom pass catches them.
using UnityEngine;

namespace CameraObscura
{
    public static class MaterialLibrary
    {
        public static Material Floor, AtriumFloor, Wall, Ceiling;
        public static Material FrameOuter, FrameInner, Plaque, BioPanel;
        public static Material Marble, DarkStone, Terracotta, Leaf, Leaf2, Bronze, Brass, Water, Glass;
        // Grand-gallery palette.
        public static Material Gold, AntiqueGold, Walnut, LinenMat, Botticino, Nero, Sandstone, MarblePolished;

        static Shader _lit, _unlit;
        public static bool Ready { get; private set; }

        public static void Init()
        {
            if (Ready) return;
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            _unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (_lit == null) { _lit = Shader.Find("Standard"); Debug.LogWarning("[MaterialLibrary] URP/Lit not found, using Standard."); }
            if (_unlit == null) { _unlit = Shader.Find("Unlit/Texture"); Debug.LogWarning("[MaterialLibrary] URP/Unlit not found, using Unlit/Texture."); }

            // Floors — polished Carrara: HIGH smoothness, LOW normal relief so the
            // reflection probes read as a wet-marble mirror, not bumpy plastic.
            var floorMaps = ProceduralTextures.Build(new SurfaceOpts { size = 256, baseRGB = new Vector3(196, 190, 182), amp = 0.12f, vein = 0.45f, veinRGB = new Vector3(74, 70, 80), normalScale = 0.6f }, 1001);
            Floor = LitTextured(floorMaps, 6f, smoothness: 0.82f, metallic: 0f);

            var atriumMaps = ProceduralTextures.Build(new SurfaceOpts { size = 512, baseRGB = new Vector3(180, 172, 160), amp = 0.14f, vein = 0.55f, veinRGB = new Vector3(74, 70, 80), normalScale = 0.5f }, 1002);
            AtriumFloor = LitTextured(atriumMaps, 8f, smoothness: 0.90f, metallic: 0f);

            // Walls — TRUE MATTE warm limewash so lit art pops off them.
            var wallMaps = ProceduralTextures.Build(new SurfaceOpts { size = 256, baseRGB = new Vector3(230, 220, 198), amp = 0.05f, vein = 0f, normalScale = 0.8f }, 1003);
            Wall = LitTextured(wallMaps, 3f, smoothness: 0.06f, metallic: 0f);

            var ceilMaps = ProceduralTextures.Build(new SurfaceOpts { size = 256, baseRGB = new Vector3(208, 196, 176), amp = 0.10f, vein = 0f, normalScale = 1.2f }, 1004);
            Ceiling = LitTextured(ceilMaps, 4f, smoothness: 0.04f, metallic: 0f);

            // Grand-gallery palette.
            Gold = LitGold(ColorUtil.Hex("#C9A24B"), 0.84f, ColorUtil.Hex("#3A2C0E") * 0.35f);
            AntiqueGold = Lit(ColorUtil.Hex("#B8860B"), smoothness: 0.60f, metallic: 1f);
            Nero = Lit(ColorUtil.Hex("#232021"), smoothness: 0.50f, metallic: 0f);
            Botticino = Lit(ColorUtil.Hex("#D8CBA8"), smoothness: 0.20f, metallic: 0f);
            Sandstone = Lit(ColorUtil.Hex("#CEC4B2"), smoothness: 0.10f, metallic: 0f);
            Walnut = Lit(ColorUtil.Hex("#2A1E14"), smoothness: 0.40f, metallic: 0f);
            LinenMat = Lit(ColorUtil.Hex("#E9E0CF"), smoothness: 0.08f, metallic: 0f);
            MarblePolished = LitTexturedNormalOnly(floorMaps.normal, ColorUtil.Hex("#EDEAE2"), 0.50f, 0.6f);
            Bronze = Lit(ColorUtil.Hex("#6E5A33"), smoothness: 0.55f, metallic: 1f);

            // Props.
            FrameOuter = Walnut;
            FrameInner = AntiqueGold;
            Plaque = Bronze;
            BioPanel = Lit(ColorUtil.Hex("#F0E8D6"), smoothness: 0.06f, metallic: 0f);

            Marble = MarblePolished;
            DarkStone = Nero;
            Terracotta = Lit(new Color(0.612f, 0.353f, 0.235f), smoothness: 0.2f, metallic: 0f);
            Leaf = Lit(new Color(0.247f, 0.42f, 0.227f), smoothness: 0.2f, metallic: 0f);
            Leaf2 = Lit(new Color(0.31f, 0.49f, 0.267f), smoothness: 0.15f, metallic: 0f);
            Brass = Gold;
            Water = LitGold(ColorUtil.Hex("#1A2A33"), 0.95f, new Color(0.02f, 0.03f, 0.04f));
            if (Water.HasProperty("_Metallic")) Water.SetFloat("_Metallic", 0.6f);
            Glass = Transparent(new Color(0.86f, 0.92f, 0.96f, 0.18f), smoothness: 0.97f);

            Ready = true;
        }

        public static Material Lit(Color c, float smoothness, float metallic)
        {
            var m = new Material(_lit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }

        static Material LitTextured(SurfaceMaps maps, float tiling, float smoothness, float metallic)
        {
            var m = Lit(Color.white, smoothness, metallic);
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", maps.color);
                m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            m.mainTexture = maps.color;
            m.mainTextureScale = new Vector2(tiling, tiling);
            if (m.HasProperty("_BumpMap") && maps.normal != null)
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", maps.normal);
                m.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
            }
            return m;
        }

        /// <summary>Lit metal (metallic 1) with a subtle emission so gilt still glints
        /// in the Night mood and Bloom kisses it.</summary>
        static Material LitGold(Color c, float smoothness, Color emission)
        {
            var m = Lit(c, smoothness, 1f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        /// <summary>Lit material with only a normal map (no base map) — for marble
        /// props so they aren't billiard-ball smooth.</summary>
        static Material LitTexturedNormalOnly(Texture normal, Color c, float smoothness, float normalScale)
        {
            var m = Lit(c, smoothness, 0f);
            if (m.HasProperty("_BumpMap") && normal != null)
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", normal);
                m.SetTextureScale("_BumpMap", new Vector2(2f, 2f));
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", normalScale);
            }
            return m;
        }

        /// <summary>An Unlit material that shows a texture at true brightness (photos).</summary>
        public static Material Unlit(Texture tex, Color tint)
        {
            var m = new Material(_unlit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            m.color = tint;
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                m.mainTexture = tex;
            }
            return m;
        }

        /// <summary>A bright emissive Lit material so Bloom blooms it (bulbs, picture lights).</summary>
        public static Material Emissive(Color c, float intensity)
        {
            var m = new Material(_lit);
            Color e = c * intensity;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", e);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        static Material Transparent(Color c, float smoothness)
        {
            var m = new Material(_lit);
            // Switch URP Lit to transparent surface.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }
    }
}
