// Single-row hang. Every work hangs in ONE row at a common centre-line height —
// no stacking — so a wing reads as a clean, uncluttered band rather than a busy
// salon grid. Works are distributed across the three display walls (back + two
// sides) in proportion to wall length; each wall lays its share out as a centred
// row of evenly-spaced cells. Rooms are sized (in Layout) so the whole collection
// fits in a single row, so no image is ever dropped.
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
        public const float ROW_H = 3.2f;        // uniform display height for every work
        public const float MAXW = 4.4f;         // widest a (landscape) work may render
        public const float GAP = 1.2f;          // clear gap between cells
        public const float CELL = MAXW + GAP;   // centre-to-centre spacing in a row
        public const float EDGE = 1.6f;         // horizontal inset from room corners
        public const float CENTER_Y = 3.2f;     // common centre-line — same height everywhere

        public static List<PlacedArtwork> Place(RoomLayout room)
        {
            var placed = new List<PlacedArtwork>();
            var arts = room.artist.artworks;
            if (arts == null || arts.Count == 0) return placed;

            float cx = room.cx, cz = room.cz, D = room.width, L = room.length;
            int total = arts.Count;

            // How many cells fit in a single centred row on each display wall, then
            // hand each wall a share proportional to its length (capped to capacity,
            // overflow absorbed by the longer walls) so every work is placed.
            int[] caps = { WallCapacity(L), WallCapacity(D), WallCapacity(D) };
            float[] lens = { L, D, D };
            int[] n = Distribute(total, lens, caps);

            var back = arts.GetRange(0, n[0]);
            var south = arts.GetRange(n[0], n[1]);
            var north = arts.GetRange(n[0] + n[1], n[2]);

            bool west = room.doorSide == "west";
            float backX = west ? cx - D / 2f + 0.2f : cx + D / 2f - 0.2f;
            Vector2 backFace = west ? new Vector2(1, 0) : new Vector2(-1, 0);
            float southZ = cz - L / 2f + 0.2f;   // faces +Z
            float northZ = cz + L / 2f - 0.2f;   // faces −Z

            // Back wall — varies along Z, centred on cz.
            Row(placed, back, backFace, along => new Vector3(backX, CENTER_Y, cz + along));
            // South wall — varies along X, centred on cx, faces +Z.
            Row(placed, south, new Vector2(0, 1), along => new Vector3(cx + along, CENTER_Y, southZ));
            // North wall — varies along X, centred on cx, faces −Z.
            Row(placed, north, new Vector2(0, -1), along => new Vector3(cx + along, CENTER_Y, northZ));

            return placed;
        }

        /// <summary>Cells that fit in a single centred row on a wall of this length.</summary>
        public static int WallCapacity(float wallLen)
        {
            float usable = wallLen - 2f * EDGE;
            // n cells span n·CELL − GAP, so n ≤ (usable + GAP) / CELL.
            return Mathf.Max(0, Mathf.FloorToInt((usable + GAP) / CELL));
        }

        // Largest-remainder apportionment proportional to wall length, clamped to
        // each wall's capacity, with any remainder pushed onto walls that still have
        // room. Layout guarantees total capacity ≥ total works, so all are placed.
        static int[] Distribute(int total, float[] lens, int[] caps)
        {
            int W = lens.Length;
            var assign = new int[W];
            var frac = new float[W];
            float sum = 0f;
            for (int i = 0; i < W; i++) sum += lens[i];

            int placed = 0;
            for (int i = 0; i < W; i++)
            {
                float ideal = sum > 0f ? total * lens[i] / sum : 0f;
                int floor = Mathf.FloorToInt(ideal);
                assign[i] = Mathf.Min(caps[i], floor);
                frac[i] = ideal - floor;
                placed += assign[i];
            }

            int rem = total - placed;
            while (rem > 0)
            {
                int best = -1;
                float bf = -1f;
                for (int i = 0; i < W; i++)
                    if (assign[i] < caps[i] && frac[i] > bf) { bf = frac[i]; best = i; }
                if (best < 0) break; // no spare capacity anywhere (shouldn't happen)
                assign[best]++; frac[best] = -1f; rem--;
            }
            return assign;
        }

        static void Row(List<PlacedArtwork> placed, List<Artwork> works, Vector2 facing,
            System.Func<float, Vector3> posFn)
        {
            int n = works.Count;
            if (n == 0) return;
            float start = -(n - 1) * CELL * 0.5f; // centre the row on the wall
            for (int c = 0; c < n; c++)
            {
                placed.Add(new PlacedArtwork
                {
                    artwork = works[c],
                    position = posFn(start + c * CELL),
                    facing = facing,
                    maxW = MAXW,
                    maxH = ROW_H,
                });
            }
        }
    }
}
