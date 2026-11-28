using Butterfly.Death;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Amusnet.Mess
{
    // A fish that patrols the creek. When the butterfly flies low and close to the water it stalks the player, then
    // leaps. The jump slows time and asks the player to mash A / D to dodge; fail and the fish catches the butterfly.
    public sealed class RiverFish : DeathCause
    {
        private enum State { Swim, Stalk, Leap, Fall }

        [SerializeField] private Transform model;           // the fish mesh, child of this object
        [SerializeField] private Vector3 modelEuler = new Vector3(90f, 180f, 0f); // fixes the mesh's own forward axis
        [SerializeField] private float waterY = -0.71f;
        [SerializeField] private Vector3 riverMin = new Vector3(-40f, 0f, -23f);
        [SerializeField] private Vector3 riverMax = new Vector3(66f, 0f, -15.5f);
        [SerializeField] private float swimSpeed = 1.4f;
        [SerializeField] private float stalkSpeed = 4f;
        [SerializeField] private float attackRange = 6.5f;  // starts stalking when the butterfly is this close
        [SerializeField] private float nearHeight = 4f;     // ...and this low over the water
        [SerializeField] private float leapRange = 3.6f;     // horizontal distance at which it jumps
        [SerializeField] private float leapGravity = 7f;    // floaty arc
        [SerializeField] private float leapTime = 1.15f;      // game seconds from jump to the bite
        [SerializeField] private int mashesNeeded = 10;
        [SerializeField] private float cooldown = 7f;
        [SerializeField] private string label = "Fish";

        private static RiverFish active;
        private State state;
        private Vector3 target, vel;
        private Transform player;
        private float nextAttack, leapElapsed, seed, mashes, caughtMsg;
        private bool expectLeft = true, dodged, splashedOut;

        private void Start()
        {
            var c = FindAnyObjectByType<ButterflyController>();
            if (c) player = c.transform;
            seed = Random.value * 10f;
            displayName = label;
            nextAttack = Time.time + Random.Range(1f, 4f);
            NewTarget();
            transform.position = target;
            NewTarget();
        }

        private void OnDisable() { if (active == this) active = null; }

        private void NewTarget()
        {
            float x = Random.Range(riverMin.x, riverMax.x);
            target = new Vector3(x, waterY - 0.35f, RiverLane.ZAt(x, -19f) + Random.Range(-1.6f, 1.6f));
        }

        private Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private void Update()
        {
            float dt = Time.deltaTime;
            float wag = Mathf.Sin((Time.time + seed) * (state == State.Swim ? 6f : 14f));
            if (model) model.localRotation = Quaternion.Euler(0f, wag * (state == State.Swim ? 8f : 16f), 0f) * Quaternion.Euler(modelEuler);
            if (caughtMsg > 0f) caughtMsg -= Time.unscaledDeltaTime;

            switch (state)
            {
                case State.Swim: UpdateSwim(dt); break;
                case State.Stalk: UpdateStalk(dt); break;
                case State.Leap: UpdateLeap(dt); break;
                case State.Fall: UpdateFall(dt); break;
            }
        }

        private bool PlayerNear(float range, out float flat)
        {
            flat = float.MaxValue;
            if (!player) return false;
            flat = Flat(player.position - transform.position).magnitude;
            return flat < range && player.position.y < waterY + nearHeight &&
                   Mathf.Abs(player.position.z - RiverLane.ZAt(player.position.x, -19f)) < 5f;
        }

        private void Steer(Vector3 to, float speed, float dt)
        {
            var d = to - transform.position;
            if (d.sqrMagnitude < 0.01f) return;
            var dir = d.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 4f);
            transform.position += transform.forward * (speed * dt);
        }

        private void UpdateSwim(float dt)
        {
            if (Flat(target - transform.position).sqrMagnitude < 1f) NewTarget();
            Steer(target, swimSpeed, dt);
            if (Time.time > nextAttack && !active && !(DeathManager.Instance && DeathManager.Instance.IsDying) &&
                PlayerNear(attackRange, out _))
            {
                active = this;
                state = State.Stalk;
            }
        }

        private void UpdateStalk(float dt)
        {
            if (!PlayerNear(attackRange * 1.3f, out float flat)) { Abort(); return; }
            float tx = Mathf.Clamp(player.position.x, riverMin.x, riverMax.x), lz = RiverLane.ZAt(tx, -19f);
            var to = new Vector3(tx, waterY - 0.5f, Mathf.Clamp(player.position.z, lz - 2f, lz + 2f));
            Steer(to, stalkSpeed, dt);
            if (Flat(player.position - transform.position).magnitude < leapRange) StartLeap();
        }

        private void Abort()
        {
            state = State.Swim;
            nextAttack = Time.time + 3f;
            if (active == this) active = null;
            NewTarget();
        }

        private void StartLeap()
        {
            // Ballistic jump that reaches the butterfly's current position after leapTime.
            float g = leapGravity;
            var d = player.position - transform.position;
            vel = new Vector3(d.x / leapTime, d.y / leapTime + 0.5f * g * leapTime, d.z / leapTime);
            leapElapsed = 0f;
            mashes = 0f;
            expectLeft = Random.value < 0.5f;
            dodged = false;
            splashedOut = false;
            state = State.Leap;
        }

        private void UpdateLeap(float dt)
        {
            TimeSlow.Hold(0.25f);
            leapElapsed += dt;
            vel += Vector3.down * leapGravity * dt;
            transform.position += vel * dt;
            Face(vel, dt);
            if (!splashedOut && transform.position.y > waterY)
            {
                splashedOut = true;
                BlastFx.Splash(new Vector3(transform.position.x, waterY, transform.position.z), 1.2f);
            }

            var kb = Keyboard.current;
            if (kb != null && !dodged)
            {
                bool left = kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame;
                bool right = kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
                if ((expectLeft && left) || (!expectLeft && right))
                {
                    mashes++;
                    NudgePlayer(expectLeft ? -1f : 1f);
                    expectLeft = !expectLeft;
                }
            }
            if (!dodged && mashes >= mashesNeeded)
            {
                dodged = true;
                // The fish sails past the dodging butterfly.
                vel += Vector3.Cross(vel.normalized, Vector3.up).normalized * 3.5f;
                state = State.Fall;
                return;
            }
            if (leapElapsed >= leapTime) Catch();
        }

        // Nose follows the arc, belly always down: pitch comes from the climb angle, never rolls.
        private void Face(Vector3 v, float dt)
        {
            var flat = Flat(v);
            if (flat.sqrMagnitude < 0.0001f) return;
            float pitch = Mathf.Clamp(Mathf.Atan2(v.y, flat.magnitude) * Mathf.Rad2Deg, -60f, 60f);
            var want = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(-pitch, 0f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-12f * dt));
        }

        private void NudgePlayer(float side)
        {
            if (!player || !player.TryGetComponent<CharacterController>(out var cc) || !cc.enabled) return;
            var cam = Camera.main;
            var right = cam ? Flat(cam.transform.right).normalized : player.right;
            cc.Move(right * (side * 0.35f));
        }

        private void Catch()
        {
            state = State.Fall;
            caughtMsg = 2f;
            var death = DeathManager.Instance;
            if (death && player) { death.Die(this, player.gameObject); return; }
            // No death system in this scene: just drop the butterfly back at its spawn.
            var spawn = GameObject.Find("PlayerSpawn");
            if (player && spawn && player.TryGetComponent<CharacterController>(out var cc))
            {
                cc.enabled = false;
                player.position = spawn.transform.position + Vector3.up * 3f;
                cc.enabled = true;
            }
        }

        private void UpdateFall(float dt)
        {
            vel += Vector3.down * leapGravity * dt;
            float before = transform.position.y;
            transform.position += vel * dt;
            Face(vel, dt);
            if (before > waterY && transform.position.y <= waterY && vel.y < 0f)
                BlastFx.Splash(new Vector3(transform.position.x, waterY, transform.position.z), 1.5f);
            if (transform.position.y < waterY - 0.3f && vel.y < 0f)
            {
                var p = transform.position; p.y = waterY - 0.35f; transform.position = p;
                state = State.Swim;
                nextAttack = Time.time + cooldown;
                if (active == this) active = null;
                NewTarget();
            }
        }

        private void OnGUI()
        {
            float k = Screen.height / 1080f;
            if (state == State.Leap && !dodged)
            {
                var title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(44 * k), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                var key = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(48 * k), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                float cx = Screen.width * 0.5f, y = Screen.height * 0.62f;
                GUI.Label(new Rect(cx - 400 * k, y - 90 * k, 800 * k, 70 * k), label.ToUpper() + " ATTACK!  MASH  A / D", title);
                var prev = GUI.color;
                float bw = 110 * k;
                GUI.color = expectLeft ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.45f);
                GUI.Box(new Rect(cx - 200 * k, y, bw, bw), "<  A", key);
                GUI.color = !expectLeft ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.45f);
                GUI.Box(new Rect(cx + 200 * k - bw, y, bw, bw), "D  >", key);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(cx - 200 * k, y + bw + 18 * k, 400 * k, 22 * k), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(1f, 0.4f, 0.2f), new Color(0.4f, 1f, 0.4f), mashes / mashesNeeded);
                GUI.DrawTexture(new Rect(cx - 196 * k, y + bw + 22 * k, 392 * k * Mathf.Clamp01(mashes / mashesNeeded), 14 * k), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            else if (caughtMsg > 0f)
            {
                var title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(40 * k), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 80 * k), "Snapped up by a " + label.ToLower() + "!", title);
            }
        }
    }
}
