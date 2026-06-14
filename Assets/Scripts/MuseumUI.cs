// The 2D interface: HUD (crosshair, focus prompt, controls, now-playing), the
// timeline scrubber, search-&-teleport, the look-closer reading panel, compare,
// a schematic minimap, guided-tour controls with narration, day/night + music
// toggles, and the four companion screens (Influence Map, Gallery, Technology,
// World Map). Built entirely at runtime with UIBuilder. Keyboard shortcuts drive
// it during play (cursor is captured for look); Esc frees the cursor to click.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CameraObscura
{
    public class MuseumUI : MonoBehaviour
    {
        public PlayerController Player;
        public TourSystem Tour;
        public AudioManager Audio;
        public MoodController Mood;
        public ContentDatabase DB;
        public MuseumLayout L;
        public Camera Cam;

        Canvas _hud, _overlay;
        TextMeshProUGUI _focusPrompt, _nowPlaying, _moodLabel, _musicLabel, _tourBanner, _compareLabel, _matureLabel;
        GameObject _tourControls;
        string _active;
        readonly Dictionary<string, GameObject> _panels = new Dictionary<string, GameObject>();
        readonly List<ArtAnchor> _compare = new List<ArtAnchor>();

        RawImage _minimap; RectTransform _playerDot;
        float _mapMinX, _mapMaxX, _mapMinZ, _mapMaxZ;

        public void Setup()
        {
            _hud = UIBuilder.CreateCanvas("HUD", 10);
            _overlay = UIBuilder.CreateCanvas("Overlays", 20);
            BuildHUD();
            BuildScrubber();
            BuildMinimap();
            // Companion panels built lazily on open.
            ApplyBlocked();
        }

        // ───────────────────────────── HUD ─────────────────────────────
        void BuildHUD()
        {
            // Crosshair.
            var ch = UIBuilder.Box(_hud.transform, "crosshair", new Color(1, 1, 1, 0.5f));
            UIBuilder.At(ch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5));

            // Title.
            var title = UIBuilder.Label(_hud.transform, "Camera Obscura", 34, UIBuilder.Ink, TextAlignmentOptions.TopLeft);
            UIBuilder.At(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -18), new Vector2(560, 48));
            var sub = UIBuilder.Label(_hud.transform, "A walkable history of photography", 18, UIBuilder.Sub, TextAlignmentOptions.TopLeft);
            UIBuilder.At(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -56), new Vector2(560, 28));

            // Focus prompt (centre-lower).
            _focusPrompt = UIBuilder.Label(_hud.transform, "", 22, UIBuilder.Ink, TextAlignmentOptions.Center);
            UIBuilder.At(_focusPrompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(900, 60));

            // Controls hint.
            var hint = UIBuilder.Label(_hud.transform,
                "WASD / arrows move   ·   Mouse look   ·   E read   ·   C compare   ·   / search   ·   T tours   ·   L day/night   ·   P music   ·   H help   ·   Esc cursor",
                16, UIBuilder.Sub, TextAlignmentOptions.Center);
            UIBuilder.At(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(1700, 24));

            // Top-right buttons.
            var row = UIBuilder.Rect(_hud.transform, "buttons");
            UIBuilder.At(row, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(720, 40));
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6; hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false;
            AddTopButton(row, "Search", () => Open("search"));
            AddTopButton(row, "Tours", () => Open("tours"));
            AddTopButton(row, "Influence", () => Open("influence"));
            AddTopButton(row, "Gallery", () => Open("gallery"));
            AddTopButton(row, "Tech", () => Open("tech"));
            AddTopButton(row, "World", () => Open("world"));
            _musicLabel = AddTopButton(row, "Music: On", () => ToggleMusic());
            _moodLabel = AddTopButton(row, "Dusk", () => Mood.Cycle());
            _matureLabel = AddTopButton(row, "Mature: Off", () => MatureContent.Toggle());
            AddTopButton(row, "Help", () => Open("help"));

            // Now playing.
            _nowPlaying = UIBuilder.Label(_hud.transform, "", 16, UIBuilder.Sub, TextAlignmentOptions.BottomRight);
            UIBuilder.At(_nowPlaying.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 44), new Vector2(520, 26));

            // Compare tray label.
            _compareLabel = UIBuilder.Label(_hud.transform, "", 16, UIBuilder.Sub, TextAlignmentOptions.BottomLeft);
            UIBuilder.At(_compareLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 44), new Vector2(520, 26));

            // Tour banner + controls.
            _tourBanner = UIBuilder.Label(_hud.transform, "", 24, UIBuilder.Ink, TextAlignmentOptions.Top);
            UIBuilder.At(_tourBanner.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1100, 90));
            _tourControls = UIBuilder.Rect(_hud.transform, "tour-controls").gameObject;
            var trc = (RectTransform)_tourControls.transform;
            UIBuilder.At(trc, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(360, 44));
            var trl = _tourControls.AddComponent<HorizontalLayoutGroup>();
            trl.spacing = 8; trl.childAlignment = TextAnchor.MiddleCenter; trl.childForceExpandWidth = true; trl.childControlWidth = true; trl.childControlHeight = true;
            UIBuilder.Button(trc, "‹ Prev", UIBuilder.Chip, () => Tour.Prev());
            UIBuilder.Button(trc, "Exit", UIBuilder.Chip, () => Tour.Stop());
            UIBuilder.Button(trc, "Next ›", UIBuilder.Chip, () => Tour.Next());
            _tourControls.SetActive(false);
        }

        TextMeshProUGUI AddTopButton(Transform row, string text, System.Action onClick)
        {
            var btn = UIBuilder.Button(row, text, UIBuilder.Chip, onClick, 18);
            UIBuilder.Height(btn.gameObject, 36);
            var le = btn.GetComponent<LayoutElement>(); le.minWidth = 78; le.preferredWidth = text.Length * 11 + 22;
            return btn.GetComponentInChildren<TextMeshProUGUI>();
        }

        void BuildScrubber()
        {
            var bar = UIBuilder.Rect(_hud.transform, "scrubber");
            UIBuilder.At(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(1500, 34));
            var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = true;
            foreach (var gw in L.gateways)
            {
                string id = gw.movementId;
                var c = ColorUtil.Hex(gw.color); c.a = 0.92f;
                var b = UIBuilder.Button(bar, $"{gw.name}\n{gw.period}", c, () => TeleportToGateway(gw), 14);
                UIBuilder.Height(b.gameObject, 34);
            }
        }

        // ─────────────────────────── Minimap ───────────────────────────
        void BuildMinimap()
        {
            _mapMinX = L.atriumMinX - 4; _mapMaxX = L.atriumMaxX + 4;
            _mapMinZ = float.MaxValue; _mapMaxZ = L.atriumHalf + 4;
            foreach (var r in L.rooms) _mapMinZ = Mathf.Min(_mapMinZ, r.cz - r.length / 2);
            _mapMinZ -= 4;

            var tex = RenderMinimapTexture(256);
            var holder = UIBuilder.Box(_hud.transform, "minimap-bg", new Color(0.05f, 0.05f, 0.06f, 0.85f));
            UIBuilder.At(holder.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 78), new Vector2(220, 220));
            var raw = UIBuilder.Rect(holder.transform, "minimap");
            UIBuilder.Stretch(raw, 4, 4, 4, 4);
            _minimap = raw.gameObject.AddComponent<RawImage>();
            _minimap.texture = tex;

            var dot = UIBuilder.Box(raw, "player-dot", new Color(1f, 0.85f, 0.3f, 1f));
            _playerDot = dot.rectTransform;
            _playerDot.anchorMin = _playerDot.anchorMax = new Vector2(0, 0);
            _playerDot.pivot = new Vector2(0.5f, 0.5f);
            _playerDot.sizeDelta = new Vector2(8, 8);
        }

        Texture2D RenderMinimapTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color32(20, 19, 18, 200);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            float w = _mapMaxX - _mapMinX, h = _mapMaxZ - _mapMinZ;
            System.Action<WalkRect, Color32> fill = (r, col) =>
            {
                int x0 = Mathf.Clamp(Mathf.RoundToInt((r.minX - _mapMinX) / w * size), 0, size - 1);
                int x1 = Mathf.Clamp(Mathf.RoundToInt((r.maxX - _mapMinX) / w * size), 0, size - 1);
                int z0 = Mathf.Clamp(Mathf.RoundToInt((r.minZ - _mapMinZ) / h * size), 0, size - 1);
                int z1 = Mathf.Clamp(Mathf.RoundToInt((r.maxZ - _mapMinZ) / h * size), 0, size - 1);
                for (int y = z0; y <= z1; y++)
                    for (int x = x0; x <= x1; x++)
                        px[y * size + x] = col;
            };
            var floorCol = new Color32(150, 140, 125, 255);
            foreach (var r in L.walkables) fill(r, floorCol);
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }

        void UpdateMinimap()
        {
            if (_playerDot == null || Player == null) return;
            Vector3 p = Player.transform.position;
            float w = _mapMaxX - _mapMinX, h = _mapMaxZ - _mapMinZ;
            float u = Mathf.Clamp01((p.x - _mapMinX) / w);
            float v = Mathf.Clamp01((p.z - _mapMinZ) / h);
            var parent = (RectTransform)_minimap.transform;
            _playerDot.anchoredPosition = new Vector2(u * parent.rect.width, v * parent.rect.height);
        }

        // ─────────────────────────── Update ───────────────────────────
        void Update()
        {
            // HUD refresh.
            if (Player != null)
            {
                var f = Player.CurrentFocus;
                _focusPrompt.text = f.HasValue ? $"{f.Value.artwork.title}\n[E] read closer    ·    [C] compare" : "";
            }
            if (Audio != null) _nowPlaying.text = Audio.MusicOn && !string.IsNullOrEmpty(Audio.NowPlayingTitle) ? "Now playing:  " + Audio.NowPlayingTitle : "";
            if (Mood != null) _moodLabel.text = Mood.CurrentName;
            if (Audio != null) _musicLabel.text = Audio.MusicOn ? "Music: On" : "Music: Off";
            if (_matureLabel != null) _matureLabel.text = MatureContent.Shown ? "Mature: Shown" : "Mature: Hidden";
            _compareLabel.text = _compare.Count > 0 ? $"Compare: {_compare.Count}/2  [C] add  [X] clear" : "";
            bool tourActive = Tour != null && Tour.Active;
            _tourBanner.text = tourActive ? Tour.Narration : "";
            if (_tourControls.activeSelf != tourActive) _tourControls.SetActive(tourActive);

            UpdateMinimap();
            HandleKeys();
        }

        void HandleKeys()
        {
            // Esc closes an open overlay.
            if (Input.GetKeyDown(KeyCode.Escape) && _active != null) { Close(); return; }
            if (UIState.InputBlocked) return; // an overlay/field owns input

            if (Input.GetKeyDown(KeyCode.E) && Player != null && Player.CurrentFocus.HasValue) OpenReading(Player.CurrentFocus.Value);
            if (Input.GetKeyDown(KeyCode.C) && Player != null && Player.CurrentFocus.HasValue) AddCompare(Player.CurrentFocus.Value);
            if (Input.GetKeyDown(KeyCode.X)) { _compare.Clear(); }
            if (Input.GetKeyDown(KeyCode.Slash)) Open("search");
            if (Input.GetKeyDown(KeyCode.T)) Open("tours");
            if (Input.GetKeyDown(KeyCode.I)) Open("influence");
            if (Input.GetKeyDown(KeyCode.G)) Open("gallery");
            if (Input.GetKeyDown(KeyCode.J)) Open("tech");
            if (Input.GetKeyDown(KeyCode.B)) Open("world");
            if (Input.GetKeyDown(KeyCode.H)) Open("help");
            if (Input.GetKeyDown(KeyCode.P)) ToggleMusic();
            if (Input.GetKeyDown(KeyCode.N)) MatureContent.Toggle();
        }

        void ToggleMusic() { if (Audio != null) Audio.MusicOn = !Audio.MusicOn; }

        // ─────────────────────── Overlay framework ───────────────────────
        GameObject NewPanel(string name, string title)
        {
            var scrim = UIBuilder.Box(_overlay.transform, name, UIBuilder.Scrim);
            UIBuilder.Stretch(scrim.rectTransform);
            // Click scrim to close.
            var btn = scrim.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Close);

            var card = UIBuilder.Box(scrim.transform, "card", UIBuilder.Panel);
            UIBuilder.At(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 820));
            // Swallow clicks on the card so they don't close.
            var cardBtn = card.gameObject.AddComponent<Button>(); cardBtn.transition = Selectable.Transition.None;

            var head = UIBuilder.Label(card.transform, title, 30, UIBuilder.Ink, TextAlignmentOptions.TopLeft);
            UIBuilder.At(head.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(900, 44));
            var close = UIBuilder.Button(card.transform, "Close  (Esc)", UIBuilder.Chip, Close, 18);
            UIBuilder.At((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(150, 40));

            scrim.gameObject.SetActive(false);
            _panels[name] = scrim.gameObject;
            return card.gameObject;
        }

        RectTransform PanelBody(GameObject card)
        {
            var body = UIBuilder.Rect(card.transform, "body");
            UIBuilder.Stretch(body, 24, 78, 24, 24);
            return body;
        }

        void Open(string name)
        {
            if (!_panels.ContainsKey(name)) BuildPanel(name);
            if (_active != null && _panels.TryGetValue(_active, out var prev)) prev.SetActive(false);
            _active = name;
            if (_panels.TryGetValue(name, out var go)) { go.SetActive(true); Populate(name); }
            ApplyBlocked();
        }

        void Close()
        {
            if (_active != null && _panels.TryGetValue(_active, out var go)) go.SetActive(false);
            _active = null;
            ApplyBlocked();
        }

        void ApplyBlocked()
        {
            bool open = _active != null;
            UIState.InputBlocked = open;
            if (open && Player != null) Player.LockCursor(false);
            // When closing we leave the cursor free; the player clicks to re-capture (web parity).
        }

        // ─────────────────────────── Panels ───────────────────────────
        void BuildPanel(string name)
        {
            switch (name)
            {
                case "search": BuildSearch(); break;
                case "tours": BuildTours(); break;
                case "influence": NewPanel("influence", "Influence Map"); break;
                case "gallery": NewPanel("gallery", "Gallery — every wing"); break;
                case "tech": NewPanel("tech", "A Parallel Timeline of Camera Technology"); break;
                case "world": NewPanel("world", "World Map — where photography clustered"); break;
                case "help": BuildHelp(); break;
                case "reading": /* built on demand */ break;
            }
        }

        void Populate(string name)
        {
            switch (name)
            {
                case "influence": FillInfluence(); break;
                case "gallery": FillGallery(); break;
                case "tech": FillTech(); break;
                case "world": FillWorld(); break;
                case "search": FocusSearch(); break;
            }
        }

        // Search.
        TMP_InputField _searchField; RectTransform _searchResults; ScrollRect _searchScroll;
        void BuildSearch()
        {
            var card = NewPanel("search", "Search & teleport to any photographer");
            var body = PanelBody(card);
            _searchField = UIBuilder.Input(body, "Type a name…  (Enter jumps to the first match)");
            UIBuilder.At(_searchField.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1100, 54));
            _searchField.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            _searchField.GetComponent<RectTransform>().offsetMin = new Vector2(0, -54);
            _searchField.GetComponent<RectTransform>().offsetMax = new Vector2(0, 0);
            _searchField.onValueChanged.AddListener(_ => RefreshSearch());
            _searchField.onSubmit.AddListener(_ => SubmitSearch());

            var listHolder = UIBuilder.Rect(body, "list");
            UIBuilder.Stretch(listHolder, 0, 66, 0, 0);
            _searchResults = UIBuilder.ScrollList(listHolder, out _searchScroll);
            UIBuilder.Stretch((RectTransform)_searchScroll.transform);
            RefreshSearch();
        }
        void FocusSearch() { if (_searchField != null) { _searchField.text = ""; _searchField.ActivateInputField(); RefreshSearch(); } }
        void RefreshSearch()
        {
            if (_searchResults == null) return;
            foreach (Transform c in _searchResults) Destroy(c.gameObject);
            string q = (_searchField != null ? _searchField.text : "").Trim().ToLowerInvariant();
            var matches = DB.Artists.Where(a => string.IsNullOrEmpty(q) ||
                a.name.ToLowerInvariant().Contains(q) || (a.nationality ?? "").ToLowerInvariant().Contains(q) ||
                (a.movementId ?? "").ToLowerInvariant().Contains(q)).Take(80);
            foreach (var a in matches)
            {
                var mv = DB.MovementById(a.movementId);
                var btn = UIBuilder.Button(_searchResults, $"{a.name}    —    {a.lifespan} · {a.nationality} · {(mv != null ? mv.name : "")}",
                    UIBuilder.Chip, () => { TeleportToArtist(a.id); Close(); }, 20);
                UIBuilder.Height(btn.gameObject, 44);
                btn.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
            }
        }
        void SubmitSearch()
        {
            string q = (_searchField != null ? _searchField.text : "").Trim().ToLowerInvariant();
            var a = DB.Artists.FirstOrDefault(x => string.IsNullOrEmpty(q) ? false : x.name.ToLowerInvariant().Contains(q));
            if (a != null) { TeleportToArtist(a.id); Close(); }
        }

        // Tours.
        void BuildTours()
        {
            var card = NewPanel("tours", "Guided tours");
            var body = PanelBody(card);
            var content = UIBuilder.ScrollList(body, out _);
            UIBuilder.Stretch((RectTransform)content.parent);
            foreach (var t in DB.Tours)
            {
                var row = UIBuilder.Button(content, $"{t.name}\n{t.blurb}", UIBuilder.Chip, () => { Tour.StartTour(t); Close(); }, 20);
                UIBuilder.Height(row.gameObject, 70);
                row.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
            }
        }

        void FillInfluence()
        {
            var card = _panels["influence"].transform.Find("card").gameObject;
            ResetBody(card, out var content);
            foreach (var e in DB.Influences)
            {
                string from = NameOf(e.from), to = NameOf(e.to);
                var row = UIBuilder.Label(content, $"{from}   →   {to}" + (string.IsNullOrEmpty(e.label) ? "" : $"   ({e.label})"), 20, UIBuilder.Ink, TextAlignmentOptions.Left);
                UIBuilder.Height(row.gameObject, 30);
            }
        }
        string NameOf(string id)
        {
            var a = DB.ArtistById(id); if (a != null) return a.name;
            var m = DB.MovementById(id); if (m != null) return m.name;
            return id;
        }

        void FillGallery()
        {
            var card = _panels["gallery"].transform.Find("card").gameObject;
            ResetBody(card, out var content);
            foreach (var m in DB.PopulatedMovements)
            {
                var hdr = UIBuilder.Label(content, $"— {m.name} ({m.period}) —", 22, ColorUtil.Hex(m.color), TextAlignmentOptions.Left);
                UIBuilder.Height(hdr.gameObject, 34);
                foreach (var a in DB.ArtistsInMovement(m.id))
                {
                    var ar = a;
                    var row = UIBuilder.Button(content, $"{a.name}   ·   {a.lifespan} · {a.nationality}   ·   {a.artworks.Count} works", UIBuilder.Chip, () => { TeleportToArtist(ar.id); Close(); }, 18);
                    UIBuilder.Height(row.gameObject, 38);
                    row.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
                }
            }
        }

        void FillTech()
        {
            var card = _panels["tech"].transform.Find("card").gameObject;
            ResetBody(card, out var content);
            foreach (var t in DB.Technology)
            {
                var row = UIBuilder.Label(content, $"<b>{t.year}  ·  {t.name}</b>\n{t.blurb}", 19, UIBuilder.Ink, TextAlignmentOptions.Left);
                row.richText = true;
                UIBuilder.Height(row.gameObject, 86);
            }
        }

        void FillWorld()
        {
            var card = _panels["world"].transform.Find("card").gameObject;
            ResetBody(card, out var content);
            var counts = new Dictionary<string, int>();
            foreach (var a in DB.Artists)
            {
                string c = a.geo != null && !string.IsNullOrEmpty(a.geo.country) ? a.geo.country : "Other / unresolved";
                counts[c] = counts.TryGetValue(c, out var n) ? n + 1 : 1;
            }
            foreach (var kv in counts.OrderByDescending(k => k.Value))
            {
                var row = UIBuilder.Label(content, $"{kv.Key}", 20, UIBuilder.Ink, TextAlignmentOptions.Left);
                var bar = UIBuilder.Box(row.transform, "bar", new Color(0.6f, 0.5f, 0.35f, 0.8f));
                UIBuilder.At(bar.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(360, 0), new Vector2(kv.Value * 16, 14));
                var n = UIBuilder.Label(row.transform, kv.Value.ToString(), 18, UIBuilder.Sub, TextAlignmentOptions.Left);
                UIBuilder.At(n.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(360 + kv.Value * 16 + 10, 0), new Vector2(60, 24));
                UIBuilder.Height(row.gameObject, 30);
            }
        }

        void BuildHelp()
        {
            var card = NewPanel("help", "How to explore");
            var body = PanelBody(card);
            var text = UIBuilder.Label(body,
                "MOVE   WASD or arrow keys\n" +
                "LOOK   move the mouse (click the window to capture; Esc to free the cursor)\n\n" +
                "E   read the piece you're facing, full size\n" +
                "C   add the piece to Compare (two side-by-side);  X clears\n" +
                "/   search and teleport to any photographer\n" +
                "T   guided tours (Grand Survey + themed) with narration\n" +
                "I / G / J / B   Influence map · Gallery · Technology · World map\n" +
                "L   day / dusk / night     P   toggle era music\n\n" +
                "The atrium is the timeline — walk east to move forward through history; each\n" +
                "movement opens north as its own hall, every photographer has their own room.\n\n" +
                "Audio: drop your MP3s into StreamingAssets/audio (see README); absent files\n" +
                "simply play silence. Nothing is ever downloaded — the museum runs fully offline.",
                21, UIBuilder.Ink, TextAlignmentOptions.TopLeft);
            UIBuilder.Stretch(text.rectTransform);
        }

        void ResetBody(GameObject card, out RectTransform content)
        {
            var existing = card.transform.Find("body");
            if (existing != null) Destroy(existing.gameObject);
            var body = PanelBody(card);
            content = UIBuilder.ScrollList(body, out _);
            UIBuilder.Stretch((RectTransform)content.parent);
        }

        // ─────────────────────── Reading / Compare ───────────────────────
        void OpenReading(ArtAnchor an)
        {
            if (_panels.TryGetValue("reading", out var old)) Destroy(old);
            var card = NewPanel("reading", an.artistName);
            var body = PanelBody(card);

            var imgHolder = UIBuilder.Box(body, "imgbg", new Color(0, 0, 0, 0.4f));
            UIBuilder.At(imgHolder.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(620, 700));
            imgHolder.rectTransform.anchorMin = new Vector2(0, 0); imgHolder.rectTransform.anchorMax = new Vector2(0, 1);
            imgHolder.rectTransform.offsetMin = new Vector2(0, 0); imgHolder.rectTransform.offsetMax = new Vector2(620, 0);
            var raw = UIBuilder.Rect(imgHolder.transform, "img");
            UIBuilder.Stretch(raw, 8, 8, 8, 8);
            var ri = raw.gameObject.AddComponent<RawImage>();
            bool hide = an.artwork.mature && !MatureContent.Shown;
            var tex = hide ? null : LoadFull(an.artwork.image);
            if (tex != null) { ri.texture = tex; FitAspect(raw, (float)tex.width / tex.height); }
            else { ri.color = hide ? new Color(0.09f, 0.08f, 0.07f) : an.accent; }
            if (hide)
            {
                var ml = UIBuilder.Label(raw, "Mature content\n\npress  N  to reveal", 30, UIBuilder.Sub, TMPro.TextAlignmentOptions.Center);
                UIBuilder.Stretch(ml.rectTransform);
            }

            var info = UIBuilder.Rect(body, "info");
            info.anchorMin = new Vector2(0, 0); info.anchorMax = new Vector2(1, 1);
            info.offsetMin = new Vector2(648, 0); info.offsetMax = new Vector2(0, 0);
            var txt = UIBuilder.Label(info, "", 22, UIBuilder.Ink, TextAlignmentOptions.TopLeft);
            UIBuilder.Stretch(txt.rectTransform);
            txt.richText = true;
            string credit = string.IsNullOrEmpty(an.artwork.credit) ? "" : $"\n\n<size=70%>{an.artwork.credit}</size>";
            string series = string.IsNullOrEmpty(an.artwork.series) ? "" : $"  ·  {an.artwork.series}";
            txt.text = $"<size=150%>{an.artwork.title}</size>\n{an.artwork.year}{series}\n\n{an.artwork.description}{credit}";

            _active = "reading";
            _panels["reading"].SetActive(true);
            ApplyBlocked();
        }

        void AddCompare(ArtAnchor an)
        {
            if (_compare.Any(c => c.artwork.id == an.artwork.id)) return;
            _compare.Add(an);
            if (_compare.Count > 2) _compare.RemoveAt(0);
            if (_compare.Count == 2) OpenCompare();
        }

        void OpenCompare()
        {
            if (_panels.TryGetValue("compare", out var old)) Destroy(old);
            var card = NewPanel("compare", "Compare");
            var body = PanelBody(card);
            for (int i = 0; i < _compare.Count; i++)
            {
                var an = _compare[i];
                var col = UIBuilder.Rect(body, $"col{i}");
                col.anchorMin = new Vector2(i * 0.5f, 0); col.anchorMax = new Vector2(i * 0.5f + 0.5f, 1);
                col.offsetMin = new Vector2(8, 8); col.offsetMax = new Vector2(-8, -8);
                var raw = UIBuilder.Rect(col, "img");
                raw.anchorMin = new Vector2(0, 0.32f); raw.anchorMax = new Vector2(1, 1); raw.offsetMin = Vector2.zero; raw.offsetMax = Vector2.zero;
                var ri = raw.gameObject.AddComponent<RawImage>();
                var tex = LoadFull(an.artwork.image);
                if (tex != null) ri.texture = tex; else ri.color = an.accent;
                var label = UIBuilder.Label(col, $"<b>{an.artwork.title}</b>\n{an.artistName} · {an.artwork.year}\n\n{an.artwork.description}", 18, UIBuilder.Ink, TextAlignmentOptions.TopLeft);
                label.richText = true;
                label.rectTransform.anchorMin = new Vector2(0, 0); label.rectTransform.anchorMax = new Vector2(1, 0.3f);
                label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
            }
            _active = "compare";
            _panels["compare"].SetActive(true);
            ApplyBlocked();
        }

        void FitAspect(RectTransform raw, float aspect)
        {
            // Letterbox within its holder by adjusting anchors is complex; leave stretch
            // (URP RawImage stretches). Aspect kept for future refinement.
        }

        Texture2D LoadFull(string relPath)
        {
            try
            {
                string full = System.IO.Path.Combine(Application.streamingAssetsPath, relPath ?? "");
                if (!System.IO.File.Exists(full)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(full))) { Destroy(tex); return null; }
                return tex;
            }
            catch { return null; }
        }

        // ─────────────────────────── Teleport ───────────────────────────
        void TeleportToGateway(Gateway gw)
        {
            Player.TeleportTo(gw.x, -L.atriumHalf - 1.0f, new Vector2(0, -1));
        }

        void TeleportToArtist(string artistId)
        {
            var r = L.rooms.Find(rm => rm.artist != null && rm.artist.id == artistId);
            if (r == null) return;
            bool west = r.doorSide == "west";
            float insideX = west ? r.corridorX - 1.0f : r.corridorX + 1.0f;
            Player.TeleportTo(insideX, r.cz, new Vector2(west ? -1 : 1, 0));
        }
    }
}
