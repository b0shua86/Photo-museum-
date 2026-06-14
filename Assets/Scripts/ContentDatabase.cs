// Loads and indexes all museum content from StreamingAssets/content/*.json.
// On macOS standalone the streaming-assets path is a normal on-disk folder, so
// the files are read synchronously — no network, no UnityWebRequest, fully
// offline. Mirrors the ordering helpers in the web prototype's data/museum.ts.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace CameraObscura
{
    public class ContentDatabase : MonoBehaviour
    {
        public static ContentDatabase Instance { get; private set; }

        public Manifest Manifest { get; private set; }
        public List<Movement> Movements { get; private set; } = new List<Movement>();
        public List<Artist> Artists { get; private set; } = new List<Artist>();
        public List<ThemedTour> Tours { get; private set; } = new List<ThemedTour>();
        public List<TechMilestone> Technology { get; private set; } = new List<TechMilestone>();
        public List<InfluenceEdge> Influences { get; private set; } = new List<InfluenceEdge>();

        /// <summary>Movements that actually have at least one wing, timeline order.</summary>
        public List<Movement> PopulatedMovements { get; private set; } = new List<Movement>();

        readonly Dictionary<string, Artist> _artistById = new Dictionary<string, Artist>();
        readonly Dictionary<string, Movement> _movementById = new Dictionary<string, Movement>();

        public bool Loaded { get; private set; }

        public string ContentDir => Path.Combine(Application.streamingAssetsPath, "content");

        public void Load()
        {
            Instance = this;
            string dir = ContentDir;
            Debug.Log($"[ContentDatabase] Loading content from {dir}");

            Manifest = ReadJson<Manifest>(dir, "manifest.json") ?? new Manifest();
            Movements = ReadJson<List<Movement>>(dir, "movements.json") ?? new List<Movement>();
            Artists = ReadJson<List<Artist>>(dir, "artists.json") ?? new List<Artist>();
            Tours = ReadJson<List<ThemedTour>>(dir, "tours.json") ?? new List<ThemedTour>();
            Technology = ReadJson<List<TechMilestone>>(dir, "technology.json") ?? new List<TechMilestone>();
            Influences = ReadJson<List<InfluenceEdge>>(dir, "influences.json") ?? new List<InfluenceEdge>();

            _movementById.Clear();
            foreach (var m in Movements)
                if (m != null && !string.IsNullOrEmpty(m.id)) _movementById[m.id] = m;

            _artistById.Clear();
            foreach (var a in Artists)
            {
                if (a == null || string.IsNullOrEmpty(a.id)) continue;
                a.artworks ??= new List<Artwork>();
                _artistById[a.id] = a;
            }

            // populatedMovements: movements (in timeline order) that have ≥1 artist.
            PopulatedMovements = Movements
                .Where(m => Artists.Any(a => a.movementId == m.id))
                .ToList();

            Loaded = true;
            Debug.Log($"[ContentDatabase] loaded {Artists.Count} artists, " +
                      $"{Movements.Count} movements ({PopulatedMovements.Count} populated), " +
                      $"{Tours.Count} tours, {Technology.Count} tech milestones, " +
                      $"{Influences.Count} influence edges, " +
                      $"{Artists.Sum(a => a.artworks.Count)} artworks.");
        }

        static T ReadJson<T>(string dir, string file) where T : class
        {
            string path = Path.Combine(dir, file);
            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogError($"[ContentDatabase] missing {path}");
                    return null;
                }
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ContentDatabase] failed to parse {file}: {e.Message}");
                return null;
            }
        }

        public Artist ArtistById(string id) =>
            id != null && _artistById.TryGetValue(id, out var a) ? a : null;

        public Movement MovementById(string id) =>
            id != null && _movementById.TryGetValue(id, out var m) ? m : null;

        /// <summary>Artists in a movement, in the same chronological order as Artists.</summary>
        public List<Artist> ArtistsInMovement(string movementId) =>
            Artists.Where(a => a.movementId == movementId).ToList();

        /// <summary>Accent colour for a movement as a Unity Color (falls back to a warm grey).</summary>
        public Color AccentOf(string movementId)
        {
            var m = MovementById(movementId);
            return ColorUtil.Hex(m != null ? m.color : "#8a7355");
        }
    }

    /// <summary>Small hex-colour helper shared across the build.</summary>
    public static class ColorUtil
    {
        public static Color Hex(string hex, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(hex)) return new Color(0.5f, 0.45f, 0.4f, alpha);
            if (ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c))
            {
                c.a = alpha;
                return c;
            }
            return new Color(0.5f, 0.45f, 0.4f, alpha);
        }
    }
}
