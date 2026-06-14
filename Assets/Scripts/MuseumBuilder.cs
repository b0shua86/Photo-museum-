// Builds the entire museum procedurally from the layout: walls (with colliders),
// floors, vaulted ceilings, corridor runners, movement gateways, chandeliers,
// the atrium showpiece (glass roof, fountain, statuary, plants, butterflies) and
// a distance-gated controller per artist room. Mirrors the web prototype's
// Museum.tsx assembly. Static geometry is combined for cheap rendering on M4.
using System.Collections.Generic;
using UnityEngine;

namespace CameraObscura
{
    /// <summary>Shared references set up at boot, read by room controllers etc.</summary>
    public static class MuseumRefs
    {
        public static Transform Player;
        public static MuseumLayout Layout;
        public static ContentDatabase DB;
        public static readonly List<RoomController> Rooms = new List<RoomController>();
    }

    public static class MuseumBuilder
    {
        static readonly Dictionary<string, Material> _accentCache = new Dictionary<string, Material>();

        public static Material Accent(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#8a7355";
            if (_accentCache.TryGetValue(hex, out var m)) return m;
            m = MaterialLibrary.Lit(ColorUtil.Hex(hex), smoothness: 0.1f, metallic: 0f);
            _accentCache[hex] = m;
            return m;
        }

        public static GameObject Build(MuseumLayout L, ContentDatabase db)
        {
            var root = new GameObject("Museum");
            MuseumRefs.Layout = L;
            MuseumRefs.DB = db;

            BuildWalls(root.transform, L);
            BuildAtriumShell(root.transform, L);
            BuildCorridors(root.transform, L);
            BuildGateways(root.transform, L);
            BuildRooms(root.transform, L, db);
            BuildAtriumDecor(root.transform, L);

            // Combine static meshes for cheap draw calls (runtime static batching).
            foreach (Transform child in root.transform) child.gameObject.isStatic = true;
            StaticBatchingUtility.Combine(root);

            Debug.Log($"[MuseumBuilder] built {L.rooms.Count} rooms, {L.walls.Count} wall segments, " +
                      $"{L.corridors.Count} corridors, {L.gateways.Count} gateways.");
            return root;
        }

        static void BuildWalls(Transform parent, MuseumLayout L)
        {
            var wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(parent, false);
            foreach (var w in L.walls)
                GO.Box(wallsRoot, w.position, w.size, MaterialLibrary.Wall, "wall", collider: true);
        }

        static void BuildAtriumShell(Transform parent, MuseumLayout L)
        {
            var g = new GameObject("Atrium").transform;
            g.SetParent(parent, false);
            float cx = (L.atriumMinX + L.atriumMaxX) / 2f;
            float len = L.atriumMaxX - L.atriumMinX;

            // Floor.
            GO.Box(g, new Vector3(cx, -0.025f, 0), new Vector3(len, 0.05f, L.atriumHalf * 2), MaterialLibrary.AtriumFloor, "atrium-floor");

            // Glass arched roof + ribs + daylight.
            DecorBuilder.GlassRoof(g, L.atriumMinX, L.atriumMaxX, L.atriumHalf, Layout.WALL_H);

            // Reflection probes along the spine (rendered after lighting, in Bootstrap).
            float span = L.atriumMaxX - L.atriumMinX;
            int n = Mathf.Max(2, Mathf.RoundToInt(span / 28f));
            for (int i = 0; i <= n; i++)
            {
                float x = L.atriumMinX + span * i / n;
                ReflectionProbes.Create(g, new Vector3(x, 3.2f, 0),
                    new Vector3(span / n + 10f, Layout.WALL_H + L.atriumHalf, L.atriumHalf * 2 + 2f), 128, false);
            }
        }

        static void BuildCorridors(Transform parent, MuseumLayout L)
        {
            var root = new GameObject("Corridors").transform;
            root.SetParent(parent, false);
            foreach (var c in L.corridors)
            {
                float len = c.zSouth - c.zNorth;
                float midZ = (c.zSouth + c.zNorth) / 2f;
                var g = new GameObject($"corridor-{c.color}").transform;
                g.SetParent(root, false);

                GO.Box(g, new Vector3(c.x, -0.025f, midZ), new Vector3(c.half * 2, 0.05f, len), MaterialLibrary.Floor, "corr-floor");
                // Accent runner.
                GO.Box(g, new Vector3(c.x, 0.02f, midZ), new Vector3(c.half * 1.1f, 0.02f, len - 1), Accent(c.color), "runner");
                // Vaulted ceiling.
                var vault = MeshFactory.BarrelVault(c.half, len, 18);
                GO.MeshObj(g, new Vector3(c.x, Layout.WALL_H, midZ), vault, MaterialLibrary.Ceiling, "corr-vault");
                // Two chandeliers.
                DecorBuilder.Chandelier(g, new Vector3(c.x, Layout.WALL_H + c.half - 0.4f, midZ - len * 0.28f), ColorUtil.Hex(c.color));
                DecorBuilder.Chandelier(g, new Vector3(c.x, Layout.WALL_H + c.half - 0.4f, midZ + len * 0.28f), ColorUtil.Hex(c.color));
                ReflectionProbes.Create(g, new Vector3(c.x, 3f, midZ),
                    new Vector3(c.half * 2 + 1f, Layout.WALL_H + c.half + 2f, len + 1f), 96, false);
            }
        }

        static void BuildGateways(Transform parent, MuseumLayout L)
        {
            var root = new GameObject("Gateways").transform;
            root.SetParent(parent, false);
            foreach (var gw in L.gateways)
            {
                var g = new GameObject($"gateway-{gw.movementId}").transform;
                g.SetParent(root, false);
                // Engraved gold cartouche hung in the hall mouth, facing the atrium.
                g.localPosition = new Vector3(gw.x, 8.5f, -L.atriumHalf + 0.15f);
                float fz;
                var cart = ModelLibrary.PlaceFlat(g, "plaque_cartouche", Vector3.zero, 0f, 8.4f, 2.6f, out fz);
                Transform ct;
                if (cart != null) { ct = cart.transform; }
                else
                {
                    GO.Quad(g, Vector3.zero, new Vector2(8.4f, 2.6f), Accent(gw.color), "sign");
                    ct = g; fz = 0.08f;
                }
                UIText.Engrave(ct, new Vector3(0, 0.55f, fz), gw.name, 0.6f, new Color(0.15f, 0.11f, 0.04f), TMPro.TextAlignmentOptions.Center, 7.4f);
                UIText.Engrave(ct, new Vector3(0, -0.55f, fz), gw.period, 0.36f, new Color(0.24f, 0.18f, 0.07f), TMPro.TextAlignmentOptions.Center, 7.4f);
                // Accent inlay line below the title.
                GO.Box(ct, new Vector3(0, 0.05f, fz), new Vector3(4.5f, 0.07f, 0.04f), Accent(gw.color), "accent");
            }
        }

        static void BuildRooms(Transform parent, MuseumLayout L, ContentDatabase db)
        {
            var root = new GameObject("Rooms").transform;
            root.SetParent(parent, false);
            MuseumRefs.Rooms.Clear();
            foreach (var room in L.rooms)
            {
                var g = new GameObject($"room-{room.artist.id}");
                g.transform.SetParent(root, false);
                g.transform.localPosition = new Vector3(room.cx, 0, room.cz);

                string accentHex = room.movement != null ? room.movement.color : "#8a7355";

                // Floor.
                GO.Box(g.transform, new Vector3(0, -0.025f, 0), new Vector3(room.width, 0.05f, room.length), MaterialLibrary.Floor, "floor");
                // Vaulted ceiling.
                var vault = MeshFactory.BarrelVault(room.width / 2f, room.length, 20);
                GO.MeshObj(g.transform, new Vector3(0, Layout.WALL_H, 0), vault, MaterialLibrary.Ceiling, "vault");
                // Doorway threshold tint.
                float doorEdgeLocal = room.doorSide == "west" ? room.width / 2f : -room.width / 2f;
                float thrX = doorEdgeLocal + (room.doorSide == "west" ? 0.5f : -0.5f);
                GO.Box(g.transform, new Vector3(thrX, 0.02f, 0), new Vector3(1f, 0.02f, 2.4f), Accent(accentHex), "threshold");

                // Distance-gated artwork + bio controller.
                var rc = g.AddComponent<RoomController>();
                rc.Init(room);
                MuseumRefs.Rooms.Add(rc);
            }
        }

        static void BuildAtriumDecor(Transform parent, MuseumLayout L)
        {
            var root = new GameObject("AtriumDecor").transform;
            root.SetParent(parent, false);

            // Fountain + reflecting pool at the atrium centre, under a grand chandelier.
            DecorBuilder.Fountain(root, new Vector3(L.poolX, 0, L.poolZ), L.poolR);
            DecorBuilder.GrandChandelier(root, new Vector3(L.poolX, Layout.WALL_H + L.atriumHalf - 5f, 0), 4f);

            // Keep clear of every hall entrance (gateway openings on the north wall).
            System.Func<float, bool> nearGate = (x) =>
            {
                foreach (var gw in L.gateways) if (Mathf.Abs(x - gw.x) < 9f) return true;
                return false;
            };
            float zSide = L.atriumHalf - 1.6f;

            // Peristyle colonnade down both sides of the nave (north side skips hall mouths).
            for (float x = L.atriumMinX + 12; x < L.atriumMaxX - 10; x += 22)
            {
                DecorBuilder.Column(root, new Vector3(x, 0, zSide), 14.5f);
                if (!nearGate(x)) DecorBuilder.Column(root, new Vector3(x, 0, -zSide), 14.5f);
            }

            // Statuary accents, alternating sides (never in front of a hall mouth).
            int flip = 1, kind = 0;
            for (float x = L.atriumMinX + 26; x < L.atriumMaxX - 12; x += 44)
            {
                float z = flip * (L.atriumHalf - 4.5f);
                if (!(flip < 0 && nearGate(x)))
                    DecorBuilder.Sculpture(root, new Vector3(x, 0, z), flip > 0 ? 180f : 0f, kind++);
                flip *= -1;
            }

            // Gallery benches down the south side of the nave.
            for (float x = L.atriumMinX + 38; x < L.atriumMaxX - 14; x += 50)
                DecorBuilder.Bench(root, new Vector3(x, 0, L.atriumHalf - 3.5f), 180f);

            // Potted plants flanking each gateway, OUTSIDE the opening.
            foreach (var gw in L.gateways)
            {
                DecorBuilder.Plant(root, new Vector3(gw.x - 6.5f, 0, -L.atriumHalf + 1.6f), 3f);
                DecorBuilder.Plant(root, new Vector3(gw.x + 6.5f, 0, -L.atriumHalf + 1.6f), 3f);
            }

            // Butterflies (atrium only).
            float atriumCx = (L.atriumMinX + L.atriumMaxX) / 2f;
            var flock = new GameObject("Butterflies");
            flock.transform.SetParent(root, false);
            flock.transform.localPosition = new Vector3(atriumCx, 0, 0);
            for (int i = 0; i < 10; i++)
            {
                var b = new GameObject($"butterfly-{i}");
                b.transform.SetParent(flock.transform, false);
                b.AddComponent<Butterfly>().Seed = i + 1;
            }
        }
    }
}
