// Procedural floor plan — CORRIDOR model. Direct port of the web prototype's
// src/museum/layout.ts.
//
// A central atrium runs west→east along X (the timeline). Each movement opens
// NORTH (−Z) off the atrium as its own corridor, and every artist gets their
// own enclosed room opening off that corridor, alternating west/east. Walking
// +X along the atrium advances through history. Collision uses axis-aligned
// "walkable" rectangles whose union the player is clamped to.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CameraObscura
{
    /// <summary>An axis-aligned walkable rectangle on the X/Z plane.</summary>
    public struct WalkRect
    {
        public float minX, maxX, minZ, maxZ;
        public WalkRect(float minX, float maxX, float minZ, float maxZ)
        { this.minX = minX; this.maxX = maxX; this.minZ = minZ; this.maxZ = maxZ; }
        public bool Contains(float x, float z) => x >= minX && x <= maxX && z >= minZ && z <= maxZ;
    }

    /// <summary>A solid wall box: centre position and full size (X,Y,Z).</summary>
    public struct WallSeg
    {
        public Vector3 position;
        public Vector3 size;
        public WallSeg(Vector3 position, Vector3 size) { this.position = position; this.size = size; }
    }

    public class RoomLayout
    {
        public Artist artist;
        public Movement movement;
        public float cx, cz;
        public float width;     // X extent (interior)
        public float length;    // Z extent (interior)
        public float corridorX;
        public string doorSide; // "east" | "west"
    }

    public class Gateway
    {
        public string movementId, name, period, color;
        public float x;
    }

    public class Corridor
    {
        public float x, half, zSouth, zNorth;
        public string color;
    }

    public class MuseumLayout
    {
        public List<RoomLayout> rooms = new List<RoomLayout>();
        public List<WallSeg> walls = new List<WallSeg>();
        public List<WalkRect> walkables = new List<WalkRect>();
        public List<Gateway> gateways = new List<Gateway>();
        public List<Corridor> corridors = new List<Corridor>();
        public float atriumMinX, atriumMaxX, atriumHalf;
        public float poolX, poolZ, poolR;
        public Vector2 spawn;
    }

    public static class Layout
    {
        // Monumental scale: cathedral-height halls and billboard-scale works.
        public const float WALL_H = 18f;      // hall wall height
        public const float WALL_T = 0.4f;
        public const float DOOR_W = 5.0f;     // doorway width
        const float ATRIUM_HALF = 16f;        // half-width of the atrium
        const float POOL_X = 18f;             // pool centre
        const float POOL_R = 6f;              // pool radius
        const float CORR_HALF = 4f;           // corridor half-width
        const float CORR_START = 4f;
        const float BAND_GAP = 3f;
        const float MIN_SIDE = 14f;           // smallest room side
        const float MAX_SIDE = 56f;           // largest room side (fits a single row of ~27 works)
        const float GAP = 8f;
        const float PAD = 8f;
        const float PLAYER_R = 0.4f;
        const float INSET = WALL_T + PLAYER_R;

        // Sized for the single-row hang: every work hangs in one un-stacked row, so
        // the room grows with the artist's body of work — enough wall length to lay
        // the whole collection out across the three display walls.
        static float RoomSide(Artist artist)
        {
            int c = Mathf.Max(1, artist.artworks != null ? artist.artworks.Count : 1);
            int perWall = Mathf.CeilToInt(c / 3f);          // share on the busiest wall
            float side = perWall * Placement.CELL - Placement.GAP + 2f * Placement.EDGE;
            return Mathf.Clamp(side, MIN_SIDE, MAX_SIDE);
        }

        /// <summary>Wall along Z at constant X, omitting doorway gaps.</summary>
        static void SideWall(float x, List<float[]> gaps, float zNorth, float zSouth, List<WallSeg> walls)
        {
            var sorted = gaps.OrderBy(g => g[0]).ToList();
            float z = zNorth;
            foreach (var g in sorted)
            {
                float lo = g[0], hi = g[1];
                if (lo > z)
                    walls.Add(new WallSeg(new Vector3(x, WALL_H / 2, (z + lo) / 2), new Vector3(WALL_T, WALL_H, lo - z)));
                z = Mathf.Max(z, hi);
            }
            if (z < zSouth)
                walls.Add(new WallSeg(new Vector3(x, WALL_H / 2, (z + zSouth) / 2), new Vector3(WALL_T, WALL_H, zSouth - z)));
        }

        public static MuseumLayout Build(ContentDatabase db)
        {
            var L = new MuseumLayout();
            var atriumGaps = new List<float[]>();
            float atriumMinX = 0f;
            float cursorX = PAD;

            foreach (var movement in db.PopulatedMovements)
            {
                var artists = db.ArtistsInMovement(movement.id);
                var sides = artists.Select(RoomSide).ToList();
                float maxD = sides.Count > 0 ? sides.Max() : MIN_SIDE;
                float colHalf = CORR_HALF + maxD;
                float junctionX = cursorX + colHalf;

                L.gateways.Add(new Gateway { movementId = movement.id, name = movement.name, period = movement.period, color = movement.color, x = junctionX });
                atriumGaps.Add(new[] { junctionX - CORR_HALF, junctionX + CORR_HALF });

                var westGaps = new List<float[]>();
                var eastGaps = new List<float[]>();

                Action<Artist, string, float, float, float> addRoom = (artist, side, D, len, cz) =>
                {
                    float doorEdge = side == "west" ? junctionX - CORR_HALF : junctionX + CORR_HALF;
                    float cx = side == "west" ? doorEdge - D / 2 : doorEdge + D / 2;
                    L.rooms.Add(new RoomLayout { artist = artist, movement = movement, cx = cx, cz = cz, width = D, length = len, corridorX = junctionX, doorSide = side });
                    L.walkables.Add(new WalkRect(cx - D / 2 + INSET, cx + D / 2 - INSET, cz - len / 2 + INSET, cz + len / 2 - INSET));
                    float backX = side == "west" ? cx - D / 2 : cx + D / 2;
                    L.walls.Add(new WallSeg(new Vector3(backX, WALL_H / 2, cz), new Vector3(WALL_T, WALL_H, len)));
                    L.walls.Add(new WallSeg(new Vector3(cx, WALL_H / 2, cz + len / 2), new Vector3(D, WALL_H, WALL_T)));
                    L.walls.Add(new WallSeg(new Vector3(cx, WALL_H / 2, cz - len / 2), new Vector3(D, WALL_H, WALL_T)));
                    (side == "west" ? westGaps : eastGaps).Add(new[] { cz - DOOR_W / 2, cz + DOOR_W / 2 });
                    L.walkables.Add(new WalkRect(doorEdge - 0.9f, doorEdge + 0.9f, cz - DOOR_W / 2 + PLAYER_R, cz + DOOR_W / 2 - PLAYER_R));
                };

                float bandTop = -ATRIUM_HALF - CORR_START;
                for (int k = 0; k < artists.Count; k += 2)
                {
                    float dW = sides[k];
                    float dE = (k + 1 < artists.Count) ? sides[k + 1] : 0f;
                    float len = Mathf.Max(dW, dE);
                    float cz = bandTop - len / 2;
                    addRoom(artists[k], "west", dW, len, cz);
                    if (k + 1 < artists.Count) addRoom(artists[k + 1], "east", dE, len, cz);
                    bandTop = cz - len / 2 - BAND_GAP;
                }

                float corridorEnd = bandTop;
                L.walkables.Add(new WalkRect(junctionX - CORR_HALF + PLAYER_R, junctionX + CORR_HALF - PLAYER_R, corridorEnd + INSET, -ATRIUM_HALF + 0.9f));
                SideWall(junctionX - CORR_HALF, westGaps, corridorEnd, -ATRIUM_HALF, L.walls);
                SideWall(junctionX + CORR_HALF, eastGaps, corridorEnd, -ATRIUM_HALF, L.walls);
                L.walls.Add(new WallSeg(new Vector3(junctionX, WALL_H / 2, corridorEnd), new Vector3(CORR_HALF * 2, WALL_H, WALL_T)));
                L.corridors.Add(new Corridor { x = junctionX, half = CORR_HALF, zSouth = -ATRIUM_HALF, zNorth = corridorEnd, color = movement.color });

                cursorX = junctionX + colHalf + GAP;
            }

            float atriumMaxX = cursorX - GAP + PAD;
            float pxW = POOL_X - POOL_R;
            float pxE = POOL_X + POOL_R;
            float ov = 0.6f;
            L.walkables.Add(new WalkRect(atriumMinX + INSET, pxW, -ATRIUM_HALF + INSET, ATRIUM_HALF - INSET)); // west
            L.walkables.Add(new WalkRect(pxE, atriumMaxX - INSET, -ATRIUM_HALF + INSET, ATRIUM_HALF - INSET)); // east
            L.walkables.Add(new WalkRect(pxW - ov, pxE + ov, POOL_R, ATRIUM_HALF - INSET));   // north walkway
            L.walkables.Add(new WalkRect(pxW - ov, pxE + ov, -ATRIUM_HALF + INSET, -POOL_R)); // south walkway

            L.walls.Add(new WallSeg(new Vector3((atriumMinX + atriumMaxX) / 2, WALL_H / 2, ATRIUM_HALF), new Vector3(atriumMaxX - atriumMinX, WALL_H, WALL_T)));
            L.walls.Add(new WallSeg(new Vector3(atriumMinX, WALL_H / 2, 0), new Vector3(WALL_T, WALL_H, ATRIUM_HALF * 2)));
            L.walls.Add(new WallSeg(new Vector3(atriumMaxX, WALL_H / 2, 0), new Vector3(WALL_T, WALL_H, ATRIUM_HALF * 2)));

            atriumGaps = atriumGaps.OrderBy(g => g[0]).ToList();
            float x2 = atriumMinX;
            foreach (var g in atriumGaps)
            {
                float lo = g[0], hi = g[1];
                if (lo > x2)
                    L.walls.Add(new WallSeg(new Vector3((x2 + lo) / 2, WALL_H / 2, -ATRIUM_HALF), new Vector3(lo - x2, WALL_H, WALL_T)));
                x2 = hi;
            }
            if (x2 < atriumMaxX)
                L.walls.Add(new WallSeg(new Vector3((x2 + atriumMaxX) / 2, WALL_H / 2, -ATRIUM_HALF), new Vector3(atriumMaxX - x2, WALL_H, WALL_T)));

            L.atriumMinX = atriumMinX;
            L.atriumMaxX = atriumMaxX;
            L.atriumHalf = ATRIUM_HALF;
            L.poolX = POOL_X; L.poolZ = 0f; L.poolR = POOL_R;
            L.spawn = new Vector2(atriumMinX + 2.5f, 0f);
            return L;
        }

        /// <summary>Clamp a desired move to the walkable union, sliding along walls.</summary>
        public static Vector2 ResolveMove(List<WalkRect> walkables, float fromX, float fromZ, float toX, float toZ)
        {
            bool Inside(float px, float pz)
            {
                for (int i = 0; i < walkables.Count; i++)
                    if (walkables[i].Contains(px, pz)) return true;
                return false;
            }
            if (Inside(toX, toZ)) return new Vector2(toX, toZ);
            float x = fromX, z = fromZ;
            if (Inside(toX, fromZ)) x = toX;
            if (Inside(x, toZ)) z = toZ;
            return new Vector2(x, z);
        }

        public static bool IsWalkable(List<WalkRect> walkables, float x, float z)
        {
            for (int i = 0; i < walkables.Count; i++)
                if (walkables[i].Contains(x, z)) return true;
            return false;
        }
    }
}
