// Headless visual-verification harness. When the app is launched with the
// environment variable CO_SHOTS=1, after the museum builds this teleports the
// camera to a set of vantage points, lets distance-gated art load, captures a
// screenshot at each, then quits. Used to iterate on the look without a human
// at the keyboard. No effect in a normal launch.
using System.Collections;
using System.IO;
using UnityEngine;

namespace CameraObscura
{
    public class ScreenshotTour : MonoBehaviour
    {
        Camera _cam;
        PlayerController _player;
        MuseumLayout _L;
        string _dir;

        public static bool Requested =>
            System.Environment.GetEnvironmentVariable("CO_SHOTS") == "1";

        public void Begin(Camera cam, PlayerController player, MuseumLayout L)
        {
            _cam = cam; _player = player; _L = L;
            _dir = System.Environment.GetEnvironmentVariable("CO_SHOTS_DIR");
            if (string.IsNullOrEmpty(_dir)) _dir = "/tmp/co_shots";
            Directory.CreateDirectory(_dir);
            StartCoroutine(Run());
        }

        struct Shot { public string name; public Vector3 pos; public Vector3 look; }

        IEnumerator Run()
        {
            if (_player != null) _player.TourActive = true; // freeze normal control
            yield return new WaitForSeconds(0.6f);

            var shots = BuildShots();
            int i = 0;
            foreach (var s in shots)
            {
                _cam.transform.position = s.pos;
                Vector3 fwd = (s.look - s.pos);
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                _cam.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                MuseumRefs.Player = _cam.transform;

                // Let nearby rooms stream their art in and lights settle.
                yield return new WaitForSeconds(1.6f);
                ScanForErrorShaders();
                string path = Path.Combine(_dir, $"shot_{i:00}_{s.name}.png");
                ScreenCapture.CaptureScreenshot(path, 1);
                Debug.Log($"[ScreenshotTour] captured {path}");
                yield return new WaitForSeconds(0.5f);
                i++;
            }

            Debug.Log("[ScreenshotTour] done.");
            yield return new WaitForSeconds(0.3f);
            Application.Quit();
        }

        readonly System.Collections.Generic.HashSet<string> _logged = new System.Collections.Generic.HashSet<string>();
        void ScanForErrorShaders()
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                var sh = m != null ? m.shader : null;
                bool bad = sh == null || sh.name.Contains("InternalError") || sh.name.Contains("Hidden/");
                if (!bad) continue;
                string p = HierarchyPath(r.transform);
                if (_logged.Add(p))
                    Debug.Log($"[MAGENTA] {p}  shader={(sh != null ? sh.name : "NULL")}  mat={(m != null ? m.name : "NULL")}");
            }
        }
        static string HierarchyPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        System.Collections.Generic.List<Shot> BuildShots()
        {
            var list = new System.Collections.Generic.List<Shot>();
            float eye = PlayerController.EYE_HEIGHT;

            // 0 — atrium, looking down the timeline.
            list.Add(new Shot { name = "atrium", pos = new Vector3(_L.atriumMinX + 3f, eye, 0), look = new Vector3(_L.atriumMinX + 40f, 3f, 0) });

            // 1 — fountain showpiece.
            list.Add(new Shot { name = "fountain", pos = new Vector3(_L.poolX - 8f, eye, 0.5f), look = new Vector3(_L.poolX, 2.2f, 0) });

            // 2 — a movement gateway sign (checks text facing).
            if (_L.gateways.Count > 0)
            {
                var g = _L.gateways[0];
                list.Add(new Shot { name = "gateway", pos = new Vector3(g.x, eye, -_L.atriumHalf + 6f), look = new Vector3(g.x, Layout.WALL_H - 1.3f, -_L.atriumHalf) });
            }

            // 3 — down a corridor.
            if (_L.corridors.Count > 0)
            {
                var c = _L.corridors[0];
                list.Add(new Shot { name = "corridor", pos = new Vector3(c.x, eye, -_L.atriumHalf - 3f), look = new Vector3(c.x, 3f, c.zNorth) });
            }

            // Rooms: the first populated room + Kyle Thompson's contemporary wing.
            void RoomShot(RoomLayout r)
            {
                if (r == null || r.artist == null) return;
                bool west = r.doorSide == "west";
                float backX = west ? r.cx - r.width / 2f : r.cx + r.width / 2f;
                float standX = west ? r.cx + r.width / 2f - 9f : r.cx - r.width / 2f + 9f;
                list.Add(new Shot { name = $"room_{r.artist.id}", pos = new Vector3(standX, eye, r.cz), look = new Vector3(backX, 6f, r.cz) });
            }
            RoomShot(_L.rooms.Find(r => r.artist != null && r.artist.id != "kyle-thompson"));
            RoomShot(_L.rooms.Find(r => r.artist != null && r.artist.id == "erwin-olaf"));
            RoomShot(_L.rooms.Find(r => r.artist != null && r.artist.id == "kyle-thompson"));
            RoomShot(_L.rooms.Find(r => r.artist != null && r.artist.artworks != null && r.artist.artworks.Exists(w => w.mature)));

            return list;
        }
    }
}
