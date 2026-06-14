# Camera Obscura — Unity build guide

A walkable 3D museum of the history of photography, rebuilt as a **native,
fully-offline macOS app** with **Unity 6 + URP**. The whole museum — 131
photographers' rooms off 8 movement halls, the atrium with its glass roof,
fountain, statuary and butterflies — is generated **procedurally at runtime from
data**, so the project produces everything from a single bootstrap scene.

- Engine: **Unity 6000.4.11f1** (Unity 6 LTS), **Universal Render Pipeline**
- Target: **macOS Apple Silicon (arm64) standalone**, Mono scripting backend
- Content: `Assets/StreamingAssets/content/*.json` — 131 artists, 8 movements,
  419 artworks (extracted from the web prototype)
- All assets ship inside the app (`StreamingAssets`): photos, models, fonts.
  **No internet is needed to launch or explore.**

---

## Fastest path — build it with one command

From a terminal (Unity 6000.4.11f1 must be installed via Unity Hub; **Rosetta 2**
is required — `softwareupdate --install-rosetta --agree-to-license`):

```bash
/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /Users/miller/Desktop/camera-obscura \
  -executeMethod CameraObscura.BuildScript.BuildMac \
  -logFile -
```

The first run downloads the URP / Newtonsoft packages (the **only** time the
internet is touched — at build time, never at app launch), compiles, configures
URP + the scene, and writes **`Build/Camera Obscura.app`**.

## Or build from the Editor (GUI)

1. Open **Unity Hub → Open → select this folder** (`camera-obscura`). Let it
   open with **6000.4.11f1**. First open resolves packages (needs internet once).
2. The project self-configures on load (URP asset, color space, the `Main` scene).
   If you ever need to redo it: menu **Camera Obscura → Configure Project**.
3. Build: menu **Camera Obscura → Build macOS App** (writes `Build/Camera Obscura.app`),
   **or** File → Build Profiles → macOS → Build.
4. To preview in the Editor: open `Assets/Scenes/Main.unity` and press **Play**.

## First launch (unsigned app)

The `.app` is unsigned, so Gatekeeper blocks a normal double-click the first time:

> **Right-click the app → Open → Open.** (Once. After that, double-click works.)

Then walk: **WASD / arrows** to move, **mouse** to look, **Esc** to free the
cursor, **H** for the in-app help. No network required — try it with Wi‑Fi off.

---

## What's in the project

```
Assets/
  Scripts/                     all C# (Assembly-CSharp)
    Bootstrap.cs               scene entry — builds everything at runtime
    ContentDatabase.cs         loads StreamingAssets/content/*.json (Newtonsoft)
    DataTypes.cs               typed content model
    Layout.cs / Placement.cs   floor-plan + artwork placement (ported from web)
    MuseumBuilder.cs           walls, floors, vaults, gateways, rooms, atrium
    ArtworkLoader.cs           distance-gated framed UNLIT photos + plaques + bios
    MaterialLibrary.cs         shared URP materials
    ProceduralTextures.cs      value-noise marble/plaster/sandstone + normals
    MeshFactory.cs             barrel vaults, tori, quads
    DecorBuilder.cs            chandeliers, statuary, plants, fountain, glass roof
    Butterfly.cs FountainSpray.cs   animated atrium life
    PlayerController.cs        FPS CharacterController + mouse-look + walkable clamp
    HallLighting.cs            8-light pool following the player
    MoodController.cs          day / dusk / night
    AudioManager.cs            local-MP3 ambience + Kyle wing playlist (offline)
    TourSystem.cs              guided tours + narration (Cinemachine-free lerp)
    MuseumUI.cs UIBuilder.cs UIText.cs   HUD, scrubber, search, reading, compare,
                               minimap, tours, and the 2D companion screens
    Editor/BuildScript.cs      URP/scene/shader/build automation
  Settings/                    URP-Pipeline.asset + URP-Renderer.asset (auto-created)
  Scenes/Main.unity            single bootstrap scene (auto-created)
  StreamingAssets/
    content/*.json             museum data (committed)
    art/<id>/*.jpg             391 photos, 146 MB (on disk; git-ignored — see below)
    models/statue.glb plant.glb   scanned bust + Khronos plant (optional upgrade)
    fonts/LiberationSerif.ttf  optional serif (the app uses a built-in font by default)
    audio/                     drop your MP3s here (see audio/README.txt)
Packages/manifest.json         URP 17.4.0, uGUI 2.0.0, Newtonsoft 3.2.1 + modules
```

### The art photos and git
The 146 MB `StreamingAssets/art/` tree is **present on disk** (the project is
complete and buildable as-is) but **git-ignored** to keep the repo lean. After a
fresh `git clone`, repopulate it with:

```bash
tools/sync-media.sh /path/to/web-prototype/camera-obscura
```

---

## Performance (Apple M4)

- **StreamingAssets** holds the photos, so Unity never runs the texture-import
  pipeline on 391 images; they're loaded at runtime, **distance-gated per room**
  and **freed when you walk away**, so only the few rooms near you cost memory.
- Repeated geometry is **static-batched** (`StaticBatchingUtility.Combine`).
- Only **8 point lights** (a moving pool) are ever active; everything else is a
  baked-feel ambient + one sun. Photos are **Unlit** (true brightness, no light
  cost). Glows are emissive so **Bloom** catches them.
- Post is cheap: Bloom + Vignette + Neutral tonemap + FXAA + MSAA 4×.
- For the absolute best on M4 you can later: switch the scripting backend to
  **IL2CPP** (needs the IL2CPP module), bake lightmaps in the Editor, and add a
  Reflection Probe per hall. The runtime build already targets 60+ FPS.

---

## Triage — if something looks wrong

| Symptom | Cause / fix |
|---|---|
| **Editor won't launch in batch / "Rosetta 2 required"** | `softwareupdate --install-rosetta --agree-to-license`, then rebuild. |
| **Black screen at launch** | URP not assigned. Run menu **Camera Obscura → Configure Project** (assigns `Assets/Settings/URP-Pipeline.asset` to Graphics + Quality), then rebuild. Check the Player log: `~/Library/Logs/Camera Obscura/Player.log` for `[Bootstrap] loaded`. |
| **Everything is pink / magenta** | URP shaders weren't included. `Configure Project` adds URP/Lit, URP/Unlit and the TMP shaders to **Always Included Shaders**; rebuild. Confirm color space is **Linear** (it's set automatically). |
| **No text anywhere** | The app builds a TMP font from Unity's built-in runtime font at boot, so this is rare. If it happens, **Window → TextMeshPro → Import TMP Essential Resources** once, then rebuild. |
| **Photos are black or missing** | Confirm `Assets/StreamingAssets/art/<id>/*.jpg` exist (run `tools/sync-media.sh`). Empty frames show the movement's accent colour + title as a placeholder by design. |
| **No audio** | Expected until you add MP3s — see `Assets/StreamingAssets/audio/README.txt`. Missing files play silence; nothing errors. |
| **Compile errors about a package** | First open needs internet to resolve URP/Newtonsoft. Reconnect and reopen so Package Manager can resolve `Packages/manifest.json`. |
| **Can't click the UI while walking** | The cursor is captured for mouse-look (by design). Press **Esc** to free it, or use the keyboard shortcuts shown in the controls hint / **H**. |
| **Falls through the floor / stuck** | The player is clamped to the data-driven walkable areas and has a CharacterController; if teleporting somewhere odd, press a timeline-scrubber chip to re-seat. |

---

## How the offline guarantee holds

- Every asset is in `StreamingAssets` and read from disk with `File.*` /
  `UnityWebRequestMultimedia` against `file://` — **no remote URLs at runtime**.
- The font is built at boot from Unity's **built-in** runtime font — never fetched.
- Music is **local MP3** only; absent files play silence.
- Verify: turn Wi‑Fi **off**, then right-click → Open the app. It runs.
