// Salon-style hang. Works are distributed across the three display walls (back +
// two sides) in proportion to wall length, then each wall arranges its share in a
// centred grid (multiple rows, packed and centred), sized to fit its cell. This
// fills the tall grand-gallery walls and shows large collections like a real
// salon, instead of one billboard per wall.
using System.Collections.Generic;
using UnityEngine;

namespace CameraObscura
{
    public struct PlacedArtwork
    {
        public Artwork artwork;
        public Vector3 position;   // world centre of the work
        public Vector2 facing;     // (x,z) unit direction the work faces, into the room
        public float maxW, maxH;   // cell box the framed work must fit within
    }

    public static class Placement
    {
        const float BOTTOM = 1.4f;     // lowest a work hangs
        const float TOP_MARGIN = 3.0f; // bare wall reserved at the top (cornice zone)
        const float EDGE = 1.6f;       // horizontal inset from room corners
        const float GAP = 0.7f;        // gap between works in the grid
        const float CAP = 5.2f;        // largest a single work may be

        public static List<PlacedArtwork> Place(RoomLayout room)
        {
            var placed = new List<PlacedArtwork>();
            var arts = room.artist.artworks;
            if (arts == null || arts.Count == 0) return placed;

            float cx = room.cx, cz = room.cz, D = room.width, L = room.length;
            int total = arts.Count;

            // Distribute across back (length L) and the two side walls (length D each).
            float wsum = L + 2 * D;
            int nBack = Mathf.Clamp(Mathf.RoundToInt(total * L / wsum), 1, total);
            int rem = total - nBack;
            int nSouth = Mathf.CeilToInt(rem / 2f);
            int nNorth = rem - nSouth;

            var back = arts.GetRange(0, nBack);
            var south = arts.GetRange(nBack, nSouth);
            var north = arts.GetRange(nBack + nSouth, nNorth);

            bool west = room.doorSide == "west";
            float backX = west ? cx - D / 2f + 0.2f : cx + D / 2f - 0.2f;
            Vector2 backFace = west ? new Vector2(1, 0) : new Vector2(-1, 0);
            float southZ = cz - L / 2f + 0.2f;   // faces +Z
            float northZ = cz + L / 2f - 0.2f;   // faces −Z

            // Back wall — varies along Z, centred on cz.
            Grid(placed, back, L - 2 * EDGE, backFace, (along, y) => new Vector3(backX, y, cz + along));
            // South wall — varies along X, centred on cx, faces +Z.
            Grid(placed, south, D - 2 * EDGE, new Vector2(0, 1), (along, y) => new Vector3(cx + along, y, southZ));
            // North wall — varies along X, centred on cx, faces −Z.
            Grid(placed, north, D - 2 * EDGE, new Vector2(0, -1), (along, y) => new Vector3(cx + along, y, northZ));

            return placed;
        }

        static void Grid(List<PlacedArtwork> placed, List<Artwork> works, float wallLen, Vector2 facing,
            System.Func<float, float, Vector3> posFn)
        {
            int n = works.Count;
            if (n == 0) return;
            float Hu = Mathf.Max(2f, Layout.WALL_H - TOP_MARGIN - BOTTOM);

            // Choose a grid roughly matching the wall's aspect.
            int cols = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(n * wallLen / Hu)), 1, n);
            int rows = Mathf.CeilToInt((float)n / cols);

            float cellW = wallLen / cols;
            float cellH = Mathf.Min(Hu / rows, CAP + GAP);
            float maxW = Mathf.Min(cellW - GAP, CAP);
            float maxH = Mathf.Min(cellH - GAP, CAP);
            float blockH = rows * cellH;
            float baseY = BOTTOM + (Hu - blockH) * 0.5f; // bottom of the centred block

            int idx = 0;
            for (int r = 0; r < rows; r++)
            {
                int inRow = Mathf.Min(cols, n - r * cols);
                float rowW = inRow * cellW;
                float startAlong = -rowW * 0.5f + cellW * 0.5f;          // centre each row
                float y = baseY + blockH - (r + 0.5f) * cellH;            // row 0 = top
                for (int c = 0; c < inRow; c++)
                {
                    float along = startAlong + c * cellW;
                    placed.Add(new PlacedArtwork
                    {
                        artwork = works[idx++],
                        position = posFn(along, y),
                        facing = facing,
                        maxW = maxW,
                        maxH = maxH,
                    });
                }
            }
        }
    }
}
