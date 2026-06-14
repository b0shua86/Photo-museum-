// A fixed pool of 8 point lights reassigned to the anchors nearest the player —
// ported from the web prototype's Lighting.tsx. Keeps the lit light-count tiny
// (so shaders/perf stay bounded) while every wing still warms up as you approach.
using System.Collections.Generic;
using UnityEngine;

namespace CameraObscura
{
    public class HallLighting : MonoBehaviour
    {
        const int POOL = 8;

        struct Anchor { public Vector3 pos; public float intensity; }
        readonly List<Anchor> _anchors = new List<Anchor>();
        Light[] _lights;
        float _timer;

        public MoodController Mood;

        public void Setup(MuseumLayout L)
        {
            _anchors.Clear();
            float span = L.atriumMaxX - L.atriumMinX;
            int count = Mathf.Max(2, Mathf.RoundToInt(span / 12f));
            float step = span / count;
            for (int i = 0; i <= count; i++)
                _anchors.Add(new Anchor { pos = new Vector3(L.atriumMinX + i * step, 3.6f, 0), intensity = 1.4f });
            foreach (var r in L.rooms)
                _anchors.Add(new Anchor { pos = new Vector3(r.cx, r.length > 10 ? 3.6f : 3.2f, r.cz), intensity = r.artist.featured ? 1.8f : 1.1f });

            _lights = new Light[POOL];
            for (int i = 0; i < POOL; i++)
            {
                var go = new GameObject($"hall-light-{i}");
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.953f, 0.875f);
                l.range = 22f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                _lights[i] = l;
            }
        }

        void Update()
        {
            if (_lights == null || MuseumRefs.Player == null) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.25f;

            Vector3 p = MuseumRefs.Player.position;
            float mul = Mood != null ? Mood.PointMul : 1f;
            _anchors.Sort((a, b) =>
                ((a.pos.x - p.x) * (a.pos.x - p.x) + (a.pos.z - p.z) * (a.pos.z - p.z))
                .CompareTo((b.pos.x - p.x) * (b.pos.x - p.x) + (b.pos.z - p.z) * (b.pos.z - p.z)));

            for (int i = 0; i < POOL; i++)
            {
                var l = _lights[i];
                if (i < _anchors.Count)
                {
                    l.transform.position = _anchors[i].pos;
                    l.intensity = _anchors[i].intensity * mul;
                    l.enabled = true;
                }
                else l.enabled = false;
            }
        }
    }
}
