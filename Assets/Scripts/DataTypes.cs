// Typed content model — mirrors the web prototype's src/data/types.ts. Parsed
// from StreamingAssets/content/*.json with Newtonsoft (handles the nested,
// optional and nullable fields JsonUtility cannot). Field names are camelCase
// to bind directly to the JSON keys.
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CameraObscura
{
    /// <summary>A music track — a single YouTube id or an ordered playlist.
    /// Used only as metadata here (the offline app plays local MP3s); the title
    /// is surfaced in the music UI.</summary>
    [System.Serializable]
    public class Track
    {
        public string youtubeId;
        public List<string> playlist;
        public string title;
        public string note;
    }

    /// <summary>A photographic movement / era — the museum's timeline spine.</summary>
    [System.Serializable]
    public class Movement
    {
        public string id;
        public string name;
        public string period;
        public int startYear;
        public int? endYear;     // null = "present"
        public string blurb;
        public string color;     // accent hex, e.g. "#8a7355"
        public Track music;
    }

    /// <summary>A single photograph hung in an artist's wing.</summary>
    [System.Serializable]
    public class Artwork
    {
        public string id;
        public string title;
        public string year;
        public string image;        // StreamingAssets-relative, e.g. "art/atget/1.jpg"
        public string description;
        public string series;       // may be null
        public string credit;       // may be null
        public bool mature;         // depicts nudity — hidden on the wall until opted in
    }

    /// <summary>A resolved country centroid for the World Map.</summary>
    [System.Serializable]
    public class Geo
    {
        public string country;
        public float lat;
        public float lng;
    }

    /// <summary>A photographer. Each artist gets a procedurally built wing.</summary>
    [System.Serializable]
    public class Artist
    {
        public string id;
        public string name;
        public string lifespan;
        public string nationality;
        public string movementId;
        public bool featured;
        public string rights;
        public string portrait;     // may be null
        public string tagline;
        public string bio;
        public Track music;         // may be null (then the era track plays)
        public Geo geo;             // may be null
        public List<Artwork> artworks = new List<Artwork>();
    }

    [System.Serializable]
    public class ThemedTour
    {
        public string id;
        public string name;
        public string blurb;
        public List<string> artistIds = new List<string>();
    }

    [System.Serializable]
    public class TechMilestone
    {
        public string year;
        public string name;
        public string blurb;
    }

    [System.Serializable]
    public class InfluenceEdge
    {
        public string from;
        public string to;
        public string kind;     // "movement" | "artist"
        public string label;
    }

    [System.Serializable]
    public class Manifest
    {
        public string title;
        public string subtitle;
        public int artistCount;
        public int movementCount;
        public List<string> populatedMovementIds = new List<string>();
        public string source;
    }
}
