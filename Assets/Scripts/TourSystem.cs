// Guided tours — a Cinemachine-free camera lerp through wing-by-wing waypoints,
// with on-screen narration. "The Grand Survey" visits every wing in order; the
// themed tours follow a curated list of artist ids (missing ids are skipped, as
// in the web prototype's tours.ts). Eases the camera like Player.tsx's tour path.
using System.Collections.Generic;
using UnityEngine;

namespace CameraObscura
{
    public class TourSystem : MonoBehaviour
    {
        struct Waypoint { public Vector3 pos; public Vector3 look; public string narration; }

        PlayerController _player;
        Camera _cam;
        readonly List<Waypoint> _stops = new List<Waypoint>();
        int _index = -1;
        float _dwell;
        const float EYE = PlayerController.EYE_HEIGHT;
        const float DWELL_TIME = 7f;

        public bool Active { get; private set; }
        public string Narration { get; private set; } = "";
        public string TourName { get; private set; } = "";

        public void Setup(PlayerController player, Camera cam)
        {
            _player = player; _cam = cam;
        }

        public void StartTour(ThemedTour tour)
        {
            BuildStops(tour);
            if (_stops.Count == 0) return;
            TourName = tour.name;
            _index = 0;
            _dwell = DWELL_TIME;
            Active = true;
            if (_player != null) _player.TourActive = true;
            ApplyNarration();
        }

        public void Next() { if (Active) { _index = Mathf.Min(_index + 1, _stops.Count); _dwell = DWELL_TIME; if (_index >= _stops.Count) Stop(); else ApplyNarration(); } }
        public void Prev() { if (Active) { _index = Mathf.Max(_index - 1, 0); _dwell = DWELL_TIME; ApplyNarration(); } }

        public void Stop()
        {
            if (!Active) return;
            Active = false;
            Narration = "";
            if (_player != null && _cam != null)
            {
                Vector3 p = _cam.transform.position;
                Vector3 f = _cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
                f.Normalize();
                _player.TourActive = false;
                _player.TeleportTo(p.x, p.z, new Vector2(f.x, f.z));
            }
        }

        void Update()
        {
            if (!Active || _cam == null || _index < 0 || _index >= _stops.Count) return;
            var s = _stops[_index];
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, s.pos, Mathf.Min(1f, dt * 2.2f));
            Quaternion target = Quaternion.LookRotation((s.look - _cam.transform.position).normalized, Vector3.up);
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, target, Mathf.Min(1f, dt * 2.5f));

            _dwell -= dt;
            if (_dwell <= 0f) Next();
        }

        void ApplyNarration()
        {
            if (_index >= 0 && _index < _stops.Count) Narration = _stops[_index].narration;
        }

        void BuildStops(ThemedTour tour)
        {
            _stops.Clear();
            var L = MuseumRefs.Layout;
            if (L == null) return;
            IEnumerable<RoomLayout> rooms;
            if (tour.artistIds == null || tour.artistIds.Count == 0)
                rooms = L.rooms;
            else
            {
                var list = new List<RoomLayout>();
                foreach (var id in tour.artistIds)
                {
                    var r = L.rooms.Find(rm => rm.artist != null && rm.artist.id == id);
                    if (r != null) list.Add(r);
                }
                rooms = list;
            }

            foreach (var r in rooms)
            {
                bool west = r.doorSide == "west";
                float insideX = west ? r.corridorX - 1.0f : r.corridorX + 1.0f;
                float backX = west ? r.cx - r.width / 2f : r.cx + r.width / 2f;
                var wp = new Waypoint
                {
                    pos = new Vector3(insideX, EYE, r.cz),
                    look = new Vector3(backX, EYE, r.cz),
                    narration = $"{r.artist.name}  ·  {(r.movement != null ? r.movement.name : "")}\n{r.artist.tagline}",
                };
                _stops.Add(wp);
            }
        }
    }
}
