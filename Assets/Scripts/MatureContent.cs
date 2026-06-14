// Global mature-content (nudity) gate. Flagged works render a discreet cover on
// the wall until the visitor opts in (press N or the HUD toggle). Rooms register
// their (photo, cover) pairs as they stream in; destroyed pairs are pruned.
using System.Collections.Generic;
using UnityEngine;

namespace CameraObscura
{
    public static class MatureContent
    {
        public static bool Shown { get; private set; }
        public static event System.Action OnChanged;

        struct Pair { public GameObject photo; public GameObject cover; }
        static readonly List<Pair> _items = new List<Pair>();

        public static void Register(GameObject photo, GameObject cover)
        {
            _items.Add(new Pair { photo = photo, cover = cover });
            Apply(photo, cover);
        }

        public static void Toggle()
        {
            Shown = !Shown;
            _items.RemoveAll(p => p.photo == null && p.cover == null);
            foreach (var p in _items) Apply(p.photo, p.cover);
            OnChanged?.Invoke();
        }

        static void Apply(GameObject photo, GameObject cover)
        {
            if (photo != null) photo.SetActive(Shown);
            if (cover != null) cover.SetActive(!Shown);
        }
    }
}
