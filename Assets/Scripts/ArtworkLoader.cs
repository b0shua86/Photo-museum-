// Distance-gated per-room artwork + bio loader. Mirrors the web prototype's
// Room.tsx + Artwork.tsx: when the player approaches, the room loads its framed
// UNLIT photos (true-brightness), TMP plaques and a bio kiosk; when they leave,
// it tears them down and frees the textures so 131 rooms stay cheap on an M4.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

namespace CameraObscura
{
    public class RoomController : MonoBehaviour
    {
        const float MAX_DIM = 9.6f;   // longest side of a framed work (monumental)
        const float FRAME_PAD = 0.4f;

        RoomLayout _room;
        float _gateSq, _unloadSq;
        bool _built;
        Coroutine _build;
        readonly List<Texture2D> _textures = new List<Texture2D>();
        readonly List<Material> _materials = new List<Material>();
        GameObject _content;

        public RoomLayout Room => _room;

        public void Init(RoomLayout room)
        {
            _room = room;
            float gate = Mathf.Max(room.width, room.length) / 2f + 22f;
            _gateSq = gate * gate;
            float unload = gate * 1.35f;
            _unloadSq = unload * unload;
        }

        void Update()
        {
            if (MuseumRefs.Player == null) return;
            Vector3 p = MuseumRefs.Player.position;
            float dx = p.x - _room.cx, dz = p.z - _room.cz;
            float dSq = dx * dx + dz * dz;

            if (!_built && dSq < _gateSq) Build();
            else if (_built && dSq > _unloadSq) Unload();
        }

        void Build()
        {
            _built = true;
            _content = new GameObject("content");
            _content.transform.SetParent(transform, false);
            if (_build != null) StopCoroutine(_build);
            _build = StartCoroutine(BuildRoutine());
        }

        IEnumerator BuildRoutine()
        {
            var placed = Placement.Place(_room);
            Vector3 center = new Vector3(_room.cx, 0, _room.cz);
            string accentHex = _room.movement != null ? _room.movement.color : "#8a7355";
            Color accent = ColorUtil.Hex(accentHex);

            foreach (var pa in placed)
            {
                BuildArtwork(pa, center, accent);
                yield return null; // spread across frames to avoid a hitch
            }

            BuildBio(center);

            // Box-projected reflection probe for this room (rendered once, now that
            // its art + lights exist) so the polished floor mirrors the wing.
            ReflectionProbes.Create(_content.transform,
                new Vector3(_room.cx, 2.2f, _room.cz),
                new Vector3(_room.width, Layout.WALL_H + _room.width / 2f, _room.length), 64, true);
            _build = null;
        }

        void BuildArtwork(PlacedArtwork pa, Vector3 roomCenter, Color accent)
        {
            var grp = new GameObject($"art-{pa.artwork.id}").transform;
            grp.SetParent(_content.transform, false);
            grp.localPosition = pa.position - roomCenter;
            Vector3 face = new Vector3(pa.facing.x, 0, pa.facing.y);
            grp.localRotation = Quaternion.LookRotation(face, Vector3.up);

            Texture2D tex = LoadTexture(pa.artwork.image);
            float aspect = (tex != null && tex.height > 0) ? (float)tex.width / tex.height : 1.4f;
            // Fit the framed work inside its salon-grid cell, preserving aspect.
            float boxAspect = pa.maxW / Mathf.Max(0.01f, pa.maxH);
            float w, h;
            if (aspect >= boxAspect) { w = pa.maxW; h = w / aspect; }
            else { h = pa.maxH; w = h * aspect; }
            float pad = Mathf.Clamp(Mathf.Min(w, h) * 0.08f, 0.06f, 0.3f);

            // Gilded ogee frame: walnut back rail + gold ovolo + recessed linen mat.
            GO.Box(grp, new Vector3(0, 0, -0.06f), new Vector3(w + pad * 2.6f, h + pad * 2.6f, 0.14f), MaterialLibrary.FrameOuter, "frame");
            GO.Box(grp, new Vector3(0, 0, 0f), new Vector3(w + pad * 1.7f, h + pad * 1.7f, 0.07f), MaterialLibrary.FrameInner, "ovolo");
            GO.Box(grp, new Vector3(0, 0, 0.025f), new Vector3(w + pad * 0.7f, h + pad * 0.7f, 0.03f), MaterialLibrary.LinenMat, "mat");

            // Photo (UNLIT, true brightness) or a CLEAR placeholder for wings whose
            // (owner-supplied) photographs aren't installed yet — e.g. Kyle Thompson.
            bool hasPhoto = tex != null;
            Material photoMat = hasPhoto ? MaterialLibrary.Unlit(tex, Color.white) : MaterialLibrary.Lit(accent * 0.5f, 0.12f, 0f);
            _materials.Add(photoMat);
            var photoGo = GO.Quad(grp, new Vector3(0, 0, 0.045f), new Vector2(w, h), photoMat, "photo");
            if (!hasPhoto)
            {
                UIText.World(grp, new Vector3(0, h * 0.05f, 0.06f), pa.artwork.title, Mathf.Min(0.55f, h * 0.1f), new Color(0.96f, 0.94f, 0.9f), TextAlignmentOptions.Center, w * 0.82f);
                UIText.World(grp, new Vector3(0, -h * 0.16f, 0.06f), pa.artwork.year + "   ·   photograph to be installed", Mathf.Min(0.26f, h * 0.045f), new Color(0.85f, 0.82f, 0.76f), TextAlignmentOptions.Center, w * 0.82f);
            }

            // Mature-content cover: a discreet panel shown until the visitor opts in.
            if (pa.artwork.mature && hasPhoto)
            {
                var cover = new GameObject("cover");
                cover.transform.SetParent(grp, false);
                cover.transform.localPosition = new Vector3(0, 0, 0.05f);
                GO.Quad(cover.transform, Vector3.zero, new Vector2(w, h), MaterialLibrary.Lit(new Color(0.09f, 0.08f, 0.07f), 0.1f, 0f), "coverpanel");
                UIText.World(cover.transform, new Vector3(0, h * 0.05f, 0.02f), "Mature content", Mathf.Min(0.6f, h * 0.1f), new Color(0.86f, 0.83f, 0.77f), TextAlignmentOptions.Center, w * 0.7f);
                UIText.World(cover.transform, new Vector3(0, -h * 0.13f, 0.02f), "press  N  to reveal", Mathf.Min(0.3f, h * 0.05f), new Color(0.72f, 0.69f, 0.62f), TextAlignmentOptions.Center, w * 0.7f);
                MatureContent.Register(photoGo, cover);
            }
        }

        void BuildBio(Vector3 roomCenter)
        {
            var a = _room.artist;
            var m = _room.movement;
            // Tall gold info plaque (Meshy) mounted near the doorway, facing the room.
            float cornerXLocal = _room.doorSide == "west" ? _room.width / 2f - 3.0f : -_room.width / 2f + 3.0f;
            float cornerZLocal = -_room.length / 2f + 3.0f;
            var bio = new GameObject("bio").transform;
            bio.SetParent(_content.transform, false);
            bio.localPosition = new Vector3(cornerXLocal, 3.4f, cornerZLocal);
            Vector3 dir = new Vector3(-cornerXLocal, 0, -cornerZLocal);
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            bio.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            float fz;
            var go = ModelLibrary.PlaceFlat(bio, "plaque_tall", Vector3.zero, 0f, 3.8f, 5.4f, out fz);
            Transform panel;
            if (go != null) { panel = go.transform; }
            else
            {
                var q = new GameObject("panel"); q.transform.SetParent(bio, false);
                GO.Quad(q.transform, Vector3.zero, new Vector2(3.8f, 5.4f), MaterialLibrary.BioPanel, "panel");
                panel = q.transform; fz = 0.05f;
            }

            var ink = new Color(0.13f, 0.10f, 0.06f);
            var sub = new Color(0.34f, 0.28f, 0.18f);
            UIText.Engrave(panel, new Vector3(0, 2.05f, fz), a.name, 0.26f, ink, TextAlignmentOptions.Center, 3.0f);
            string meta = $"{a.lifespan}  ·  {a.nationality}";
            UIText.Engrave(panel, new Vector3(0, 1.6f, fz), meta, 0.14f, sub, TextAlignmentOptions.Center, 3.0f);
            UIText.Engrave(panel, new Vector3(0, 1.28f, fz), m != null ? m.name : "", 0.13f, sub, TextAlignmentOptions.Center, 3.0f);
            UIText.World(panel, new Vector3(0, 0.9f, fz), a.tagline, 0.135f, new Color(0.2f, 0.17f, 0.11f), TextAlignmentOptions.Top, 2.9f);
            UIText.World(panel, new Vector3(0, 0.45f, fz), a.bio, 0.1f, new Color(0.2f, 0.18f, 0.13f), TextAlignmentOptions.Top, 2.9f, lineSpacing: -6f);
        }

        Texture2D LoadTexture(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return null;
            try
            {
                string full = Path.Combine(Application.streamingAssetsPath, relPath);
                if (!File.Exists(full)) return null;
                byte[] bytes = File.ReadAllBytes(full);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!tex.LoadImage(bytes)) { Destroy(tex); return null; }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.anisoLevel = 4;
                _textures.Add(tex);
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoomController] failed to load {relPath}: {e.Message}");
                return null;
            }
        }

        void Unload()
        {
            _built = false;
            if (_build != null) { StopCoroutine(_build); _build = null; }
            if (_content != null) Destroy(_content);
            foreach (var t in _textures) if (t != null) Destroy(t);
            foreach (var mm in _materials) if (mm != null) Destroy(mm);
            _textures.Clear();
            _materials.Clear();
        }
    }
}
