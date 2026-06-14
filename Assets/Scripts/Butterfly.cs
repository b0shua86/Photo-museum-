// A drifting butterfly with flapping wings (atrium only). Ported from the web
// prototype's Butterfly in Decor.tsx. Builds its own two-winged body on Awake;
// MuseumBuilder just supplies a Seed.
using UnityEngine;

namespace CameraObscura
{
    public class Butterfly : MonoBehaviour
    {
        public int Seed = 1;
        static readonly Color[] WingColors =
        {
            new Color(0.91f, 0.537f, 0.231f), new Color(0.357f, 0.498f, 0.78f),
            new Color(0.839f, 0.325f, 0.416f), new Color(0.886f, 0.753f, 0.267f),
            new Color(0.553f, 0.373f, 0.69f),
        };

        Transform _lw, _rw;
        float _cx, _ax, _az, _ay, _sp, _ph, _base;

        void Awake()
        {
            int s = Seed;
            _cx = s * 13.1f;
            _ax = 6 + (s % 4) * 2;
            _az = 2.5f;
            _ay = 1.2f;
            _sp = 0.4f + (s % 5) * 0.06f;
            _ph = s * 1.7f;
            _base = 2.6f + (s % 3) * 0.6f;

            var color = WingColors[s % WingColors.Length];
            var mat = MaterialLibrary.Emissive(color, 0.6f);

            _lw = MakeWing(-1, mat);
            _rw = MakeWing(1, mat);
        }

        Transform MakeWing(int dir, Material mat)
        {
            var wing = new GameObject(dir < 0 ? "wingL" : "wingR").transform;
            wing.SetParent(transform, false);
            GO.Box(wing, new Vector3(dir * 0.11f, 0, 0), new Vector3(0.22f, 0.16f, 0.01f), mat, "wing");
            return wing;
        }

        void Update()
        {
            float time = Time.time;
            float t = time * _sp + _ph;
            transform.localPosition = new Vector3(
                _cx + Mathf.Sin(t) * _ax,
                _base + Mathf.Sin(t * 1.7f) * _ay,
                Mathf.Cos(t * 0.8f) * _az);
            float yaw = Mathf.Atan2(Mathf.Cos(t) * _ax, -Mathf.Sin(t * 0.8f) * _az * 0.8f) + Mathf.PI / 2f;
            transform.localRotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);

            float flap = (Mathf.Sin(time * 14f + _ph) * 0.9f + 0.4f) * Mathf.Rad2Deg;
            if (_lw != null) _lw.localRotation = Quaternion.Euler(0, flap, 0);
            if (_rw != null) _rw.localRotation = Quaternion.Euler(0, -flap, 0);
        }
    }
}
