// World-space text helpers built on TextMeshPro. A single shared font asset is
// created once at boot from Unity's built-in runtime font (so no font is ever
// fetched at runtime and no editor "Import TMP Essentials" step is required —
// the carried-over pitfall where a CDN font once suspended the whole scene).
//
// Sizing convention: the text object is scaled to 0.1, and fontSize is
// requestedMetres*100, so a fontSize argument expressed in metres yields text
// that is ~that many metres tall in world space.
using TMPro;
using UnityEngine;

namespace CameraObscura
{
    public static class UIText
    {
        /// <summary>Shared runtime font asset (set by Bootstrap). Null → TMP default.</summary>
        public static TMP_FontAsset Font;

        const float SCALE = 0.1f;

        public static TextMeshPro World(Transform parent, Vector3 localPos, string text, float sizeMeters,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float wrapWidthMeters = 2.2f,
            float lineSpacing = 0f, bool rich = false)
        {
            var go = new GameObject("label");
            go.transform.SetParent(parent, false);
            // TextMeshPro renders readable on its local −Z face. Our groups put +Z
            // toward the viewer, so spin the label 180° about Y to face the viewer
            // (otherwise the text shows mirrored). The spin flips local X, so we
            // pivot on X-centre and mirror the horizontal alignment to compensate.
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = Vector3.one * SCALE;

            var tmp = go.AddComponent<TextMeshPro>();
            if (Font != null) tmp.font = Font;
            tmp.text = text ?? "";
            tmp.fontSize = Mathf.Max(0.01f, sizeMeters) * 100f; // world height ≈ sizeMeters at SCALE 0.1
            tmp.color = color;
            tmp.alignment = MirrorAlign(align);
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.lineSpacing = lineSpacing;
            tmp.richText = rich;

            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(0.1f, wrapWidthMeters) / SCALE, 60f);
            float py = align.ToString().Contains("Top") ? 1f : (align.ToString().Contains("Bottom") ? 0f : 0.5f);
            rt.pivot = new Vector2(0.5f, py);
            return tmp;
        }

        /// <summary>Text that reads as laser-engraving on metal: a dark incised body
        /// over a faint, slightly-offset catch-light copy (the chiselled-bevel trick).</summary>
        public static void Engrave(Transform parent, Vector3 localPos, string text, float sizeMeters,
            Color ink, TextAlignmentOptions align = TextAlignmentOptions.Center, float wrapWidthMeters = 1f,
            float lineSpacing = 0f)
        {
            // Catch-light, set a hair behind and down-right.
            World(parent, localPos + new Vector3(0.004f, -0.004f, -0.001f), text, sizeMeters,
                new Color(1f, 0.93f, 0.72f, 0.45f), align, wrapWidthMeters, lineSpacing);
            // Dark incised body in front.
            World(parent, localPos, text, sizeMeters, ink, align, wrapWidthMeters, lineSpacing);
        }

        static TextAlignmentOptions MirrorAlign(TextAlignmentOptions a)
        {
            switch (a)
            {
                case TextAlignmentOptions.TopLeft: return TextAlignmentOptions.TopRight;
                case TextAlignmentOptions.TopRight: return TextAlignmentOptions.TopLeft;
                case TextAlignmentOptions.Left: return TextAlignmentOptions.Right;
                case TextAlignmentOptions.Right: return TextAlignmentOptions.Left;
                case TextAlignmentOptions.BottomLeft: return TextAlignmentOptions.BottomRight;
                case TextAlignmentOptions.BottomRight: return TextAlignmentOptions.BottomLeft;
                default: return a;
            }
        }
    }
}
