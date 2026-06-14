// Day / dusk / night moods — ported from the web prototype's moods.ts. Drives
// the sun, ambient, fog and camera background, plus a point-light multiplier the
// hall lighting reads. Cycle with the UI button or the 'L' key.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CameraObscura
{
    public class MoodController : MonoBehaviour
    {
        struct Mood { public Color bg; public float sun, point, exposure; }

        // Warm-light / cool-shadow gallery. Brightness is driven mainly by
        // post-exposure (the master knob), not by flattening the light rig.
        static readonly Mood[] Moods =
        {
            new Mood { bg = ColorHex(0x14, 0x11, 0x0d), sun = 0.70f, point = 1.0f, exposure = -0.40f }, // dusk (default)
            new Mood { bg = ColorHex(0xbf, 0xc4, 0xcc), sun = 1.15f, point = 0.7f, exposure =  0.15f }, // day
            new Mood { bg = ColorHex(0x05, 0x05, 0x09), sun = 0.40f, point = 1.3f, exposure = -1.10f }, // night
        };
        static readonly string[] Names = { "Dusk", "Day", "Night" };

        Light _sun;
        Camera _cam;
        int _index;

        public ColorAdjustments ColorAdj;
        public float PointMul { get; private set; } = 1f;
        public string CurrentName => Names[_index];

        public void Setup(Camera cam)
        {
            _cam = cam;
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            // Rake through the glass roof onto the atrium floor (the hero shaft).
            go.transform.rotation = Quaternion.Euler(55f, -28f, 0f);
            _sun = go.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = ColorUtil.Hex("#FFF1DC");
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.55f;

            // Trilight ambient: cool skylight from above, warm stone bounce, dark floor.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.31f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.18f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.05f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            Apply(0);
        }

        public void Cycle() => Apply((_index + 1) % Moods.Length);

        public void Apply(int index)
        {
            _index = (index % Moods.Length + Moods.Length) % Moods.Length;
            var m = Moods[_index];
            PointMul = m.point;

            RenderSettings.fogColor = m.bg;
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 75f;
            if (_sun != null) _sun.intensity = m.sun;
            if (_cam != null)
            {
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = m.bg;
            }
            if (ColorAdj != null) { ColorAdj.postExposure.overrideState = true; ColorAdj.postExposure.value = m.exposure; }
        }

        void Update()
        {
            if (!UIState.InputBlocked && Input.GetKeyDown(KeyCode.L)) Cycle();
        }

        static Color ColorHex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);
    }
}
