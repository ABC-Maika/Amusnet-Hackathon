using UnityEngine;
using UnityEngine.InputSystem;

namespace Amusnet.Mess
{
    // A horse pulls a cart loaded with TNT around the village, pausing at stops. The butterfly dusts the fuse with pollen
    // (hold Space close to the cart); the horse bolts, the fuse burns down and the blast wrecks whatever stands nearby:
    // houses, trees, even bridges. Wherever the cart is stopped decides what goes up.
    public sealed class TntCart : MonoBehaviour
    {
        private enum State { Roam, Lit, Gone }

        [SerializeField] private Vector3[] route;      // the road the cart follows (ping-pong), baked from the village roads
        [SerializeField] private Vector3[] stopPoints; // places it pauses at: the bridge centres
        [SerializeField] private Transform horse;
        [SerializeField] private Transform[] hideOnBoom;
        [SerializeField] private Vector3 fuseLocal = new Vector3(0f, 2.3f, 0.6f);
        [SerializeField] private float speed = 4f;
        [SerializeField] private float acceleration = 2.5f;
        [SerializeField] private float dwell = 6f;
        [SerializeField] private float igniteRange = 7f;
        [SerializeField] private float chargeTime = 1.1f;
        [SerializeField] private float fuseTime = 4.5f;
        [SerializeField] private float blastRadius = 13f;
        [SerializeField] private float blastPower = 14f;
        [SerializeField] private float respawnDelay = 20f;
        [SerializeField] private float wheelRadius = 0.34f;

        private State state;
        private Transform player;
        private Animator horseAnim;
        private Rigidbody rb;
        private Collider[] colliders;
        private Vector3 horseLocalPos;
        private Quaternion horseLocalRot;
        private float[] cum, stopS;
        private int dir = 1, lastStop = -1;
        private float s, cur, yaw, dwellT, charge, fuse, goneT, slowT, horseRun, sway;
        private Vector3 pos;
        private Transform[] wheels, crates;
        private Quaternion[] crateRot;
        private GameObject sparks;
        private Material cartMat;

        private void Start()
        {
            var c = FindAnyObjectByType<ButterflyController>();
            if (c) player = c.transform;
            if (horse)
            {
                horseAnim = horse.GetComponentInChildren<Animator>();
                horseLocalPos = horse.localPosition;
                horseLocalRot = horse.localRotation;
            }
            foreach (var h in hideOnBoom)
                if (h && !cartMat) { var r = h.GetComponentInChildren<Renderer>(); if (r) cartMat = r.sharedMaterial; }

            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            var wl = new System.Collections.Generic.List<Transform>();
            var cl = new System.Collections.Generic.List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name.Contains("Wheel")) wl.Add(t);
                else if (t.name.EndsWith("Crate") && t.parent == transform) cl.Add(t);
            }
            wheels = wl.ToArray();
            crates = cl.ToArray();
            crateRot = new Quaternion[crates.Length];
            for (int i = 0; i < crates.Length; i++) crateRot[i] = crates[i].localRotation;

            BuildRoute();
            ResetToStart();
        }

        // Cumulative length along the route and the distance of each pause point.
        private void BuildRoute()
        {
            cum = new float[route.Length];
            for (int i = 1; i < route.Length; i++) cum[i] = cum[i - 1] + Flat(route[i] - route[i - 1]).magnitude;
            stopS = new float[stopPoints.Length];
            for (int k = 0; k < stopPoints.Length; k++)
            {
                float best = float.MaxValue;
                for (int i = 0; i < route.Length; i++)
                {
                    float d = Flat(route[i] - stopPoints[k]).sqrMagnitude;
                    if (d < best) { best = d; stopS[k] = cum[i]; }
                }
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // Point and heading at distance d along the route.
        private Vector3 Sample(float d, out Vector3 tangent)
        {
            d = Mathf.Clamp(d, 0f, cum[cum.Length - 1]);
            int i = System.Array.BinarySearch(cum, d);
            if (i < 0) i = ~i;
            i = Mathf.Clamp(i, 1, cum.Length - 1);
            float seg = Mathf.Max(0.0001f, cum[i] - cum[i - 1]);
            tangent = Flat(route[i] - route[i - 1]).normalized;
            return Vector3.Lerp(route[i - 1], route[i], (d - cum[i - 1]) / seg);
        }

        private void ResetToStart()
        {
            s = 0f; dir = 1; cur = 0f; dwellT = 3f; lastStop = -1;
            var p = Sample(0f, out var tg);
            yaw = Mathf.Atan2(tg.x, tg.z) * Mathf.Rad2Deg;
            pos = p; pos.y = SurfaceY(p);
            p.y = pos.y;
            ApplyPose(p, Quaternion.Euler(0f, yaw, 0f), true);
        }

        // Highest surface under (x, z): terrain, or a bridge deck. Moving bodies (incl. this cart) are ignored.
        private float SurfaceY(Vector3 p)
        {
            float best = float.MinValue;
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (player && h.transform.IsChildOf(player)) continue;
                if (h.rigidbody || h.transform.IsChildOf(transform)) continue;
                best = Mathf.Max(best, h.point.y);
            }
            if (best == float.MinValue) { var t = Terrain.activeTerrain; best = t ? t.SampleHeight(p) + t.transform.position.y : p.y; }
            return best;
        }

        private void ApplyPose(Vector3 p, Quaternion r, bool teleport)
        {
            if (rb && !teleport) { rb.MovePosition(p); rb.MoveRotation(r); }
            else { transform.SetPositionAndRotation(p, r); if (rb) { rb.position = p; rb.rotation = r; } }
        }

        private void Update()
        {
            if (slowT > 0f) { slowT -= Time.unscaledDeltaTime; TimeSlow.Hold(0.3f); }
            switch (state)
            {
                case State.Roam: UpdateIgnition(); break;
                case State.Lit: UpdateLit(); break;
                case State.Gone:
                    goneT += Time.deltaTime;
                    if (goneT > respawnDelay) Respawn();
                    break;
            }
            if (horseRun > 0f) RunHorse();
            if (state != State.Gone) Animate(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (cum == null || state == State.Gone) return;
            if (state == State.Roam) Drive(Time.fixedDeltaTime);
            else Place(Time.fixedDeltaTime, 0f);
        }

        // Follows the road: speeds up, slows into each bridge, pauses, turns round at the ends. Nothing random.
        private void Drive(float dt)
        {
            float total = cum[cum.Length - 1];
            float target = speed;
            if (dwellT > 0f) { dwellT -= dt; target = 0f; }
            else
            {
                for (int k = 0; k < stopS.Length; k++)
                {
                    float ahead = (stopS[k] - s) * dir;
                    if (k == lastStop) { if (ahead < -3f) lastStop = -1; continue; }
                    if (ahead >= 0f && ahead < cur * cur / (2f * acceleration) + 0.5f) target = 0f;
                    if (ahead >= -0.3f && ahead < 0.5f && cur < 0.3f) { dwellT = dwell; lastStop = k; target = 0f; }
                }
                float end = dir > 0 ? total - s : s;
                if (end < cur * cur / (2f * acceleration) + 0.3f) target = 0f;
                if (end < 0.4f && cur < 0.3f) { dir = -dir; dwellT = dwell; lastStop = -1; }
            }

            Sample(s, out var tg);
            float want = Mathf.Atan2(tg.x * dir, tg.z * dir) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, want)) > 30f) target = 0f; // swing round first, then drive off

            cur = Mathf.MoveTowards(cur, target, acceleration * dt);
            s = Mathf.Clamp(s + dir * cur * dt, 0f, total);
            Place(dt, 100f);
        }

        // Stand on the ground (or bridge) at the route position and pitch with the slope.
        private void Place(float dt, float turnRate)
        {
            Vector3 p = Sample(s, out var tg);
            if (turnRate > 0f)
            {
                float want = Mathf.Atan2(tg.x * dir, tg.z * dir) * Mathf.Rad2Deg;
                yaw = Mathf.MoveTowardsAngle(yaw, want, turnRate * dt);
            }
            Vector3 f = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float yFront = SurfaceY(p + f * 2.2f), yBack = SurfaceY(p - f * 2.2f);
            p.y = Mathf.Lerp(pos.y, (yFront + yBack) * 0.5f, 1f - Mathf.Exp(-10f * dt));
            pos = p;
            float pitch = -Mathf.Atan2(yFront - yBack, 4.4f) * Mathf.Rad2Deg;
            ApplyPose(p, Quaternion.Euler(pitch, yaw, 0f), false);
        }

        // Wheels roll with the ground speed, the load jostles, the horse walks in step.
        private void Animate(float dt)
        {
            float sp = speed > 0f ? Mathf.Clamp01(cur / speed) : 0f;
            sway += dt * (6f + 8f * sp);
            float deg = state == State.Roam ? dir * cur * dt / wheelRadius * Mathf.Rad2Deg : 0f;
            foreach (var w in wheels) if (w) w.Rotate(transform.right, deg, Space.World);
            for (int i = 0; i < crates.Length; i++)
            {
                if (!crates[i] || !crates[i].gameObject.activeSelf) continue;
                float ph = i * 1.7f;
                crates[i].localRotation = crateRot[i] * Quaternion.Euler(Mathf.Sin(sway + ph) * 1.4f * sp, 0f, Mathf.Cos(sway * 0.8f + ph) * 1.8f * sp);
            }
            if (horseAnim && horseRun <= 0f && state == State.Roam)
            {
                horseAnim.SetFloat("Vert", Mathf.MoveTowards(horseAnim.GetFloat("Vert"), sp, dt * 4f));
                horseAnim.SetFloat("State", 0f);
            }
        }

        // Pollen on the fuse: hold Space close to the cart.
        private bool PlayerNearFuse => player &&
            (player.position - transform.TransformPoint(fuseLocal)).sqrMagnitude < igniteRange * igniteRange;

        private void UpdateIgnition()
        {
            bool held = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            charge = Mathf.Clamp01(charge + (PlayerNearFuse && held ? Time.deltaTime / chargeTime : -Time.deltaTime * 1.5f));
            if (charge >= 1f) Light();
        }

        private void Light()
        {
            state = State.Lit;
            fuse = fuseTime;
            charge = 0f;
            sparks = BlastFx.FuseSparks(transform, fuseLocal);
            HudNotice.Show("Fuse lit! Get clear!");
            if (horse) { horse.SetParent(null, true); horseRun = 4f; }
        }

        // The horse bolts off ahead of the cart.
        private void RunHorse()
        {
            horseRun -= Time.deltaTime;
            if (!horse) return;
            horse.position += horse.forward * (8f * Time.deltaTime);
            var p = horse.position; p.y = SurfaceY(p); horse.position = p;
            if (horseAnim) { horseAnim.SetFloat("Vert", 1f); horseAnim.SetFloat("State", 1f); }
            if (horseRun <= 0f) horse.gameObject.SetActive(false);
        }

        private void UpdateLit()
        {
            fuse -= Time.deltaTime;
            if (fuse <= 0f) Boom();
        }

        private void Boom()
        {
            Vector3 c = transform.TransformPoint(fuseLocal);
            int houses = 0, trees = 0, bridges = 0;
            System.Action<Destructible> count = d =>
            {
                if (d.Type == Destructible.Kind.House) houses++;
                else if (d.Type == Destructible.Kind.Tree) trees++;
                else bridges++;
            };
            Destructible.Collapsed += count;
            Destructible.Blast(c, blastRadius, blastPower);
            Destructible.Collapsed -= count;
            BlastFx.Boom(c, blastRadius);
            BlastFx.Debris(new Bounds(c, new Vector3(2f, 1.5f, 4f)), cartMat, 12, c, blastPower, 20f);
            foreach (var h in hideOnBoom) if (h) h.gameObject.SetActive(false);
            foreach (var col in colliders) if (col) col.enabled = false;
            cur = 0f;
            if (sparks) Destroy(sparks);
            state = State.Gone;
            goneT = 0f;
            slowT = 1.0f;
            var parts = new System.Collections.Generic.List<string>();
            if (houses > 0) parts.Add(houses + (houses == 1 ? " house" : " houses"));
            if (trees > 0) parts.Add(trees + (trees == 1 ? " tree" : " trees"));
            if (bridges > 0) parts.Add(bridges == 1 ? "a bridge" : bridges + " bridges");
            HudNotice.Show(parts.Count > 0 ? "BOOM!  Destroyed " + string.Join(", ", parts) : "BOOM!  Nothing but a crater");
        }

        private void Respawn()
        {
            foreach (var h in hideOnBoom) if (h) h.gameObject.SetActive(true);
            if (horse)
            {
                horse.SetParent(transform, false);
                horse.localPosition = horseLocalPos;
                horse.localRotation = horseLocalRot;
                horse.gameObject.SetActive(true);
            }
            foreach (var c in colliders) if (c) c.enabled = true;
            ResetToStart();
            charge = 0f;
            state = State.Roam;
            HudNotice.Show("A fresh TNT cart rolls into the village");
        }

        private void OnGUI()
        {
            float k = Screen.height / 1080f;
            if (state == State.Roam && (charge > 0.01f || PlayerNearFuse))
            {
                var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(20 * k), alignment = TextAnchor.MiddleCenter };
                float cx = Screen.width * 0.5f, y = Screen.height - 130f * k;
                GUI.Box(new Rect(cx - 220 * k, y, 440 * k, 40 * k), "Hold SPACE  -  dust the fuse with pollen", style);
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(cx - 220 * k, y + 46 * k, 440 * k, 12 * k), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.15f), charge);
                GUI.DrawTexture(new Rect(cx - 218 * k, y + 48 * k, 436 * k * charge, 8 * k), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            else if (state == State.Lit && Camera.main)
            {
                Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 4.5f);
                if (sp.z > 0f)
                {
                    var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(54 * k), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                    style.normal.textColor = Mathf.Repeat(Time.time * 6f, 1f) > 0.5f ? new Color(1f, 0.25f, 0.15f) : new Color(1f, 0.85f, 0.3f);
                    GUI.Label(new Rect(sp.x - 150 * k, Screen.height - sp.y - 40 * k, 300 * k, 80 * k), "TNT  " + Mathf.Max(0f, fuse).ToString("0.0"), style);
                }
            }
        }
    }
}
