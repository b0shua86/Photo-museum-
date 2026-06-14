// Loads the Meshy-generated hero models (imported by glTFast into
// Assets/Resources/MeshyModels) and places them fitted to a target height and
// seated on the floor. If a model isn't present (glTFast not installed, or the
// GLB missing) Place() returns null and the caller falls back to procedural
// geometry — so the museum always builds.
using System.Collections.Generic;
using UnityEngine;

namespace CameraObscura
{
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();
        static readonly HashSet<string> _missing = new HashSet<string>();

        public static bool Available(string name) => Load(name) != null;

        static GameObject Load(string name)
        {
            if (_cache.TryGetValue(name, out var p)) return p;
            if (_missing.Contains(name)) return null;
            var prefab = Resources.Load<GameObject>("MeshyModels/" + name);
            if (prefab == null) { _missing.Add(name); return null; }
            _cache[name] = prefab;
            return prefab;
        }

        /// <summary>Instantiate <paramref name="name"/> under parent at localPos, fitted
        /// so its tallest dimension is targetHeight and its base sits on the floor,
        /// horizontally centred, rotated by yawDeg (+ optional model-specific tweak).
        /// Returns the holder, or null if the model isn't available.</summary>
        public static GameObject Place(Transform parent, string name, Vector3 localPos, float yawDeg,
            float targetHeight, float extraYaw = 0f, float extraPitch = 0f)
        {
            var prefab = Load(name);
            if (prefab == null) return null;

            var holder = new GameObject("model-" + name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;
            holder.transform.localRotation = Quaternion.Euler(extraPitch, yawDeg + extraYaw, 0);

            var inst = Object.Instantiate(prefab, holder.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return holder;

            // Fit to height.
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = Mathf.Max(b.size.x, b.size.y, b.size.z);
            // prefer vertical fit for tall props
            float fit = b.size.y > 0.001f ? targetHeight / b.size.y : (h > 0.001f ? targetHeight / h : 1f);
            inst.transform.localScale = Vector3.one * fit;

            // Seat base on floor + centre horizontally (recompute world bounds after scale).
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            Vector3 hp = holder.transform.position;
            Vector3 delta = new Vector3(hp.x - b.center.x, hp.y - b.min.y, hp.z - b.center.z);
            inst.transform.position += delta;

            return holder;
        }

        /// <summary>Mount a flat model (a plaque/tablet) so its thinnest axis faces
        /// the holder's +Z, fit to targetW × targetH, centred at localPos (NOT floor
        /// seated). Returns (holder, faceZ) where faceZ is the local +Z of the front
        /// face — put engraved text just in front of that. Holder is null if absent.</summary>
        public static GameObject PlaceFlat(Transform parent, string name, Vector3 localPos, float yawDeg,
            float targetW, float targetH, out float faceZ)
        {
            faceZ = 0.03f;
            var prefab = Load(name);
            if (prefab == null) return null;

            var holder = new GameObject("plaque-" + name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;
            holder.transform.localRotation = Quaternion.Euler(0, yawDeg, 0);

            var inst = Object.Instantiate(prefab, holder.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            Bounds b = BoundsIn(inst, holder.transform);
            Vector3 sz = b.size;
            // Rotate the thinnest axis to Z (so the flat face points along Z).
            if (sz.z <= sz.x && sz.z <= sz.y) { /* already */ }
            else if (sz.x <= sz.y) inst.transform.localRotation = Quaternion.Euler(0, 90, 0);
            else inst.transform.localRotation = Quaternion.Euler(90, 0, 0);

            b = BoundsIn(inst, holder.transform);
            float sx = b.size.x > 1e-4f ? targetW / b.size.x : 1f;
            float sy = b.size.y > 1e-4f ? targetH / b.size.y : 1f;
            float s = Mathf.Min(sx, sy);
            inst.transform.localScale = inst.transform.localScale * s;

            b = BoundsIn(inst, holder.transform);
            inst.transform.localPosition -= b.center;  // centre on holder
            faceZ = b.size.z * 0.5f + 0.01f;
            return holder;
        }

        static Bounds BoundsIn(GameObject root, Transform frame)
        {
            bool has = false; Bounds b = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var t = mf.transform;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + new Vector3(
                        ((i & 1) == 0 ? -1 : 1) * mb.extents.x,
                        ((i & 2) == 0 ? -1 : 1) * mb.extents.y,
                        ((i & 4) == 0 ? -1 : 1) * mb.extents.z);
                    Vector3 local = frame.InverseTransformPoint(t.TransformPoint(c));
                    if (!has) { b = new Bounds(local, Vector3.zero); has = true; } else b.Encapsulate(local);
                }
            }
            return b;
        }
    }
}
