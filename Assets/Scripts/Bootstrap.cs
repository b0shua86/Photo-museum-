// The single scene component. Builds the entire museum at runtime from data —
// content → layout → player rig → procedural museum → lighting/mood/audio →
// tours → UI → post-processing — so the project produces everything even though
// it was authored without ever opening the Editor's scene view.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using TMPro;

namespace CameraObscura
{
    public class Bootstrap : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("[Bootstrap] Camera Obscura starting…");

            // 1. Content.
            var db = gameObject.AddComponent<ContentDatabase>();
            db.Load();
            if (!db.Loaded || db.Artists.Count == 0)
                Debug.LogError("[Bootstrap] content failed to load — check StreamingAssets/content.");

            // 2. Shared font (built-in runtime font → TMP asset; never fetched).
            BuildFont();

            // 3. Materials + layout.
            MaterialLibrary.Init();
            var layout = Layout.Build(db);
            MuseumRefs.DB = db;
            MuseumRefs.Layout = layout;

            // 4. Player rig + camera.
            var cam = BuildPlayerRig(layout, db, out var player);

            // 5. Procedural museum.
            var museumRoot = MuseumBuilder.Build(layout, db);

            // 5b. Post-processing (built before mood so mood can drive exposure).
            var colorAdj = BuildPostFX(cam);

            // 6. Lighting + mood.
            var mood = gameObject.AddComponent<MoodController>();
            mood.ColorAdj = colorAdj;
            mood.Setup(cam);
            var hall = new GameObject("HallLighting").AddComponent<HallLighting>();
            hall.Mood = mood;
            hall.Setup(layout);

            // 7. Audio.
            var audio = new GameObject("Audio").AddComponent<AudioManager>();
            audio.Setup(layout, db, cam.transform);

            // 7b. Render the static atrium/corridor reflection probes now that the
            // sun, ambient and hall lights exist (room probes render with their art).
            foreach (var probe in museumRoot.GetComponentsInChildren<ReflectionProbe>())
                probe.RenderProbe();

            // 8. Tours.
            var tour = gameObject.AddComponent<TourSystem>();
            tour.Setup(player, cam);

            // 9. UI + EventSystem.
            EnsureEventSystem();
            var ui = new GameObject("MuseumUI").AddComponent<MuseumUI>();
            ui.Player = player; ui.Tour = tour; ui.Audio = audio; ui.Mood = mood; ui.DB = db; ui.L = layout; ui.Cam = cam;
            ui.Setup();

            // 10. Optional headless screenshot tour (CO_SHOTS=1).
            if (ScreenshotTour.Requested)
                new GameObject("ScreenshotTour").AddComponent<ScreenshotTour>().Begin(cam, player, layout);

            Debug.Log($"[Bootstrap] loaded — {db.Artists.Count} wings, {layout.rooms.Count} rooms. Walk east; press H for help.");
        }

        void BuildFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (f != null)
            {
                try { UIText.Font = TMP_FontAsset.CreateFontAsset(f); }
                catch (System.Exception e) { Debug.LogWarning($"[Bootstrap] runtime TMP font failed ({e.Message}); using TMP default."); }
            }
            else Debug.LogWarning("[Bootstrap] no built-in font found; TMP will use its default asset.");
        }

        Camera BuildPlayerRig(MuseumLayout layout, ContentDatabase db, out PlayerController player)
        {
            var playerGo = new GameObject("Player");
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
            cc.slopeLimit = 89f; cc.stepOffset = 0.3f;

            var camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 220f; cam.fieldOfView = 65f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camData.renderShadows = true;

            player = playerGo.AddComponent<PlayerController>();
            player.Cam = cam;

            // Anchors for focus / reading, computed deterministically from placement.
            var anchors = new List<ArtAnchor>();
            foreach (var room in layout.rooms)
            {
                foreach (var pa in Placement.Place(room))
                {
                    anchors.Add(new ArtAnchor
                    {
                        artwork = pa.artwork,
                        artistId = room.artist.id,
                        artistName = room.artist.name,
                        movementId = room.movement != null ? room.movement.id : null,
                        accent = ColorUtil.Hex(room.movement != null ? room.movement.color : "#8a7355"),
                        artistMusic = room.artist.music,
                        pos = pa.position,
                        facing = new Vector3(pa.facing.x, 0, pa.facing.y),
                    });
                }
            }

            MuseumRefs.Player = cam.transform;
            player.Setup(layout.spawn, layout.walkables, anchors);
            return cam;
        }

        void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Warm, opulent "grand gallery" grade. Returns the ColorAdjustments so the
        // MoodController can drive day/dusk/night through post-exposure only.
        ColorAdjustments BuildPostFX(Camera cam)
        {
            var go = new GameObject("PostVolume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            // Neutral tonemap keeps the UNLIT photographs colour-accurate; the warmth
            // comes from the grade + white balance, not a filmic curve crushing prints.
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.Neutral;

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.overrideState = true; color.postExposure.value = 0f;
            color.contrast.overrideState = true; color.contrast.value = 12f;
            color.saturation.overrideState = true; color.saturation.value = 6f;
            color.colorFilter.overrideState = true; color.colorFilter.value = ColorUtil.Hex("#FFF3E2");

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.overrideState = true; wb.temperature.value = 12f;
            wb.tint.overrideState = true; wb.tint.value = 3f;

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.85f;
            bloom.threshold.overrideState = true; bloom.threshold.value = 1.05f;
            bloom.scatter.overrideState = true; bloom.scatter.value = 0.62f;
            bloom.tint.overrideState = true; bloom.tint.value = ColorUtil.Hex("#FFE6C0");

            var vig = profile.Add<Vignette>(true);
            vig.intensity.overrideState = true; vig.intensity.value = 0.26f;
            vig.smoothness.overrideState = true; vig.smoothness.value = 0.5f;
            vig.color.overrideState = true; vig.color.value = ColorUtil.Hex("#120B05");

            var grain = profile.Add<FilmGrain>(true);
            grain.type.overrideState = true; grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.overrideState = true; grain.intensity.value = 0.18f;
            grain.response.overrideState = true; grain.response.value = 0.8f;

            // Gaussian DoF that only softens the deep distance (>16 m) — near works
            // stay crisp, far halls get cinematic depth.
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.overrideState = true; dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.overrideState = true; dof.gaussianStart.value = 16f;
            dof.gaussianEnd.overrideState = true; dof.gaussianEnd.value = 48f;
            dof.gaussianMaxRadius.overrideState = true; dof.gaussianMaxRadius.value = 0.8f;

            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.overrideState = true; ca.intensity.value = 0.06f;

            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null) camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            return color;
        }
    }
}
