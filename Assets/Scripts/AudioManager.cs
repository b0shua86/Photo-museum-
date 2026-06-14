// Local-MP3 audio, fully offline. Plays a looping era ambience track per hall
// (StreamingAssets/audio/movements/<movementId>.mp3), crossfading as the player
// moves between movements, and Kyle Thompson's wing plays his 5-track playlist
// (audio/kyle-thompson/1.mp3 … 5.mp3). Missing files simply play silence — the
// owner drops their own MP3s in per README. Nothing is ever downloaded.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace CameraObscura
{
    public class AudioManager : MonoBehaviour
    {
        MuseumLayout _L;
        ContentDatabase _db;
        Transform _player;

        AudioSource _a, _b;
        bool _aActive = true;
        string _playingPath = "";
        string _targetPath = "";
        bool _targetLoop = true;
        float _scanTimer;
        int _kyleIndex;
        bool _inKyle;

        public bool MusicOn = true;
        public string NowPlayingTitle { get; private set; } = "";

        readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public void Setup(MuseumLayout L, ContentDatabase db, Transform player)
        {
            _L = L; _db = db; _player = player;
            _a = gameObject.AddComponent<AudioSource>();
            _b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _a, _b })
            {
                s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f; s.volume = 0f;
            }
        }

        AudioSource Active => _aActive ? _a : _b;
        AudioSource Idle => _aActive ? _b : _a;

        void Update()
        {
            if (_L == null || _player == null) return;
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 0.5f;
                Recompute();
            }

            // Advance the Kyle playlist when a (non-looping) track finishes.
            if (_inKyle && MusicOn && !string.IsNullOrEmpty(_playingPath) && Active.clip != null && !Active.isPlaying && Active.time <= 0f)
            {
                _kyleIndex = (_kyleIndex % 5) + 1;
                SetTarget($"audio/kyle-thompson/{_kyleIndex}.mp3", loop: false, KyleTitle());
            }

            // Volume easing toward target (crossfade + mute).
            float want = MusicOn ? 0.55f : 0f;
            Active.volume = Mathf.MoveTowards(Active.volume, want, Time.deltaTime * 0.7f);
            Idle.volume = Mathf.MoveTowards(Idle.volume, 0f, Time.deltaTime * 0.7f);
        }

        void Recompute()
        {
            string movementId; bool isKyle;
            Context(out movementId, out isKyle);

            if (isKyle)
            {
                if (!_inKyle) { _inKyle = true; _kyleIndex = 1; SetTarget("audio/kyle-thompson/1.mp3", loop: false, KyleTitle()); }
            }
            else
            {
                _inKyle = false;
                var m = _db.MovementById(movementId);
                string title = m != null && m.music != null ? m.music.title : (m != null ? m.name : "");
                SetTarget($"audio/movements/{movementId}.mp3", loop: true, title);
            }
        }

        string KyleTitle()
        {
            var k = _db.ArtistById("kyle-thompson");
            return k != null && k.music != null ? $"{k.music.title} ({_kyleIndex}/5)" : $"Kyle Thompson — track {_kyleIndex}/5";
        }

        void SetTarget(string path, bool loop, string title)
        {
            if (path == _targetPath) return;
            _targetPath = path;
            _targetLoop = loop;
            NowPlayingTitle = title;
            StartCoroutine(SwitchTo(path, loop));
        }

        IEnumerator SwitchTo(string path, bool loop)
        {
            AudioClip clip = null;
            if (_cache.TryGetValue(path, out clip)) { /* cached */ }
            else
            {
                string full = Path.Combine(Application.streamingAssetsPath, path);
                if (File.Exists(full))
                {
                    string url = new System.Uri(full).AbsoluteUri;
                    using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
                    {
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            clip = DownloadHandlerAudioClip.GetContent(req);
                            _cache[path] = clip;
                        }
                    }
                }
                else _cache[path] = null; // remember absence → silence, no retry storm
            }

            // The target may have changed while loading; bail if so.
            if (_targetPath != path) yield break;
            _playingPath = path;
            if (clip == null) { NowPlayingTitle += "  (no file)"; yield break; }

            var next = Idle;
            next.clip = clip;
            next.loop = loop;
            next.volume = 0f;
            next.Play();
            _aActive = !_aActive; // 'next' becomes Active; Update() fades it in and the other out
        }

        void Context(out string movementId, out bool isKyle)
        {
            movementId = _L.gateways.Count > 0 ? _L.gateways[0].movementId : "origins";
            isKyle = false;
            Vector3 p = _player.position;

            // In a corridor/room band (north of the atrium): use the nearest room.
            if (p.z < -_L.atriumHalf - 0.4f && _L.rooms.Count > 0)
            {
                RoomLayout nearest = null; float bestSq = float.MaxValue;
                foreach (var r in _L.rooms)
                {
                    float dx = p.x - r.cx, dz = p.z - r.cz, sq = dx * dx + dz * dz;
                    if (sq < bestSq) { bestSq = sq; nearest = r; }
                }
                if (nearest != null)
                {
                    movementId = nearest.movement != null ? nearest.movement.id : movementId;
                    float reach = Mathf.Max(nearest.width, nearest.length) / 2f + 6f;
                    isKyle = nearest.artist != null && nearest.artist.id == "kyle-thompson" && bestSq < reach * reach;
                    return;
                }
            }

            // Atrium: nearest gateway along the timeline.
            float bd = float.MaxValue;
            foreach (var gw in _L.gateways)
            {
                float d = Mathf.Abs(p.x - gw.x);
                if (d < bd) { bd = d; movementId = gw.movementId; }
            }
        }
    }
}
