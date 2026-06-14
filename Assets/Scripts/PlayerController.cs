// First-person controller: CharacterController body + mouse-look camera, with
// the web prototype's walkable-rectangle clamp layered on top of real wall
// colliders (belt-and-suspenders so the player can never tunnel a wall). Cursor
// is captured for look; Esc frees it. Game input is suppressed while a UI field
// or fullscreen overlay is active, and while a guided tour drives the camera.
using UnityEngine;

namespace CameraObscura
{
    /// <summary>Shared UI/interaction state read by the player controller.</summary>
    public static class UIState
    {
        /// <summary>True when a fullscreen overlay/menu owns the input (no walking/looking).</summary>
        public static bool InputBlocked;
    }

    public struct ArtAnchor
    {
        public Artwork artwork;
        public string artistId, artistName, movementId;
        public Color accent;
        public Track artistMusic;
        public Vector3 pos;
        public Vector3 facing;
    }

    public class PlayerController : MonoBehaviour
    {
        public const float EYE_HEIGHT = 1.7f;
        const float SPEED = 5.2f;
        const float LOOK_SENS = 2.2f;
        // How far the crosshair can reach to "read" a work. The salon hang stacks
        // works in rows up the 18 m walls, so a top-row piece sits ~13–14 m from the
        // eye once you step back to see it — a 6 m reach left those unreadable.
        const float REACH = 16f;

        public Camera Cam;
        public System.Collections.Generic.List<WalkRect> Walkables;
        public System.Collections.Generic.List<ArtAnchor> Anchors;

        CharacterController _cc;
        float _yaw, _pitch;
        float _focusTimer;

        /// <summary>The artwork the player is currently facing (within reach), or null.</summary>
        public ArtAnchor? CurrentFocus { get; private set; }
        /// <summary>Set by TourSystem to take over the camera.</summary>
        public bool TourActive { get; set; }

        public void Setup(Vector2 spawn, System.Collections.Generic.List<WalkRect> walkables, System.Collections.Generic.List<ArtAnchor> anchors)
        {
            Walkables = walkables;
            Anchors = anchors;
            _cc = GetComponent<CharacterController>();
            transform.position = new Vector3(spawn.x, 0f, spawn.y);
            _yaw = 90f; // face +X (into the museum / down the timeline)
            _pitch = 0f;
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            if (Cam != null) Cam.transform.localPosition = new Vector3(0, EYE_HEIGHT, 0);
            LockCursor(true);
        }

        void Update()
        {
            if (TourActive) return;

            // Cursor capture handling.
            if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
            if (Input.GetMouseButtonDown(0) && !UIState.InputBlocked && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);

            bool blocked = UIState.InputBlocked || Cursor.lockState != CursorLockMode.Locked;

            if (!blocked)
            {
                Look();
                Move();
            }

            UpdateFocus();
        }

        void Look()
        {
            _yaw += Input.GetAxis("Mouse X") * LOOK_SENS;
            _pitch -= Input.GetAxis("Mouse Y") * LOOK_SENS;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            if (Cam != null) Cam.transform.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }

        void Move()
        {
            float f = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) +
                      (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? -1 : 0);
            float r = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) +
                      (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (f == 0 && r == 0) { EnforceFloor(); return; }

            Vector3 fwd = transform.forward; fwd.y = 0; fwd.Normalize();
            Vector3 right = transform.right; right.y = 0; right.Normalize();
            Vector3 move = (fwd * f + right * r);
            if (move.sqrMagnitude > 1f) move.Normalize();
            move *= SPEED * dt;

            float x = transform.position.x, z = transform.position.z;
            Vector2 target = Walkables != null
                ? Layout.ResolveMove(Walkables, x, z, x + move.x, z + move.z)
                : new Vector2(x + move.x, z + move.z);
            Vector3 delta = new Vector3(target.x - x, 0, target.y - z);
            if (_cc != null && _cc.enabled) _cc.Move(delta);
            else transform.position += delta;
            EnforceFloor();
        }

        void EnforceFloor()
        {
            var p = transform.position;
            if (Mathf.Abs(p.y) > 0.001f) transform.position = new Vector3(p.x, 0f, p.z);
        }

        void UpdateFocus()
        {
            _focusTimer -= Time.deltaTime;
            if (_focusTimer > 0f) return;
            _focusTimer = 0.1f;
            if (Cam == null || Anchors == null) { CurrentFocus = null; return; }

            Vector3 eye = Cam.transform.position;
            Vector3 look = Cam.transform.forward;
            float best = 0.55f;       // min alignment
            ArtAnchor? pick = null;
            foreach (var an in Anchors)
            {
                Vector3 to = an.pos - eye;
                float d = to.magnitude;
                if (d > REACH || d < 0.05f) continue;
                Vector3 dir = to / d;
                float align = Vector3.Dot(look, dir);
                if (align < best) continue;
                // Must be on the front side of the piece (allows steep up-angles to
                // high works; only rejects looking at the back through the wall).
                if (Vector3.Dot(an.facing, -dir) < 0f) continue;
                best = align;
                pick = an;
            }
            CurrentFocus = pick;
        }

        public void TeleportTo(float x, float z, Vector2 face)
        {
            bool was = _cc != null && _cc.enabled;
            if (_cc != null) _cc.enabled = false;
            transform.position = new Vector3(x, 0f, z);
            _yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            _pitch = 0f;
            if (Cam != null) Cam.transform.localRotation = Quaternion.identity;
            if (_cc != null) _cc.enabled = was;
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
