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

        [SerializeField] private Vector3[] stops;
        [SerializeField] private Transform horse;
        [SerializeField] private Transform[] hideOnBoom;
        [SerializeField] private Vector3 fuseLocal = new Vector3(0f, 2.3f, 0.6f);
        [SerializeField] private float speed = 3.4f;
        [SerializeField] private float dwell = 14f;
        [SerializeField] private float igniteRange = 7f;
        [SerializeField] private float chargeTime = 1.1f;
        [SerializeField] private float fuseTime = 4.5f;
        [SerializeField] private float blastRadius = 13f;
        [SerializeField] private float blastPower = 14f;
        [SerializeField] private float respawnDelay = 20f;

        private State state;
        private Transform player;
        private Animator horseAnim;
        private Vector3 horseLocalPos;
        private Quaternion horseLocalRot;
        private int idx = 1, dir = 1;
        private float yaw, dwellT, charge, fuse, goneT, slowT, horseRun;
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
            transform.position = stops[0];
            yaw = transform.eulerAngles.y;
            dwellT = 3f;
        }

        // Highest surface under (x, z): terrain, or a bridge deck.
        private float SurfaceY(Vector3 p)
        {
            float best = float.MinValue;
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (player && h.transform.IsChildOf(player)) continue;
                if (h.rigidbody) continue;
                best = Mathf.Max(best, h.point.y);
            }
            if (best == float.MinValue) { var t = Terrain.activeTerrain; best = t ? t.SampleHeight(p) + t.transform.position.y : p.y; }
            return best;
        }

        private void Update()
        {
            if (slowT > 0f) { slowT -= Time.unscaledDeltaTime; TimeSlow.Hold(0.3f); }
            switch (state)
            {
                case State.Roam: UpdateRoam(); break;
                case State.Lit: UpdateLit(); break;
                case State.Gone:
                    goneT += Time.deltaTime;
                    if (goneT > respawnDelay) Respawn();
                    break;
            }
            if (horseRun > 0f) RunHorse();
        }

        private void UpdateRoam()
        {
            bool moving = false;
            Vector3 to = stops[idx] - transform.position; to.y = 0f;
            if (dwellT > 0f) dwellT -= Time.deltaTime;
            else if (to.magnitude < 0.9f)
            {
                dwellT = dwell;
                if (idx + dir < 0 || idx + dir >= stops.Length) dir = -dir;
                idx += dir;
            }
            else
            {
                float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                yaw = Mathf.MoveTowardsAngle(yaw, want, 70f * Time.deltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, want)) < 30f)
                {
                    transform.position += Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (speed * Time.deltaTime);
                    moving = true;
                }
            }
            Place();
            if (horseAnim)
            {
                horseAnim.SetFloat("Vert", Mathf.MoveTowards(horseAnim.GetFloat("Vert"), moving ? 1f : 0f, Time.deltaTime * 4f));
                horseAnim.SetFloat("State", 0f);
            }

            // Pollen on the fuse: hold Space close to the cart.
            bool near = player && (player.position - transform.position).sqrMagnitude < igniteRange * igniteRange;
            bool held = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            charge = Mathf.Clamp01(charge + (near && held ? Time.deltaTime / chargeTime : -Time.deltaTime * 1.5f));
            if (charge >= 1f) Light();
        }

        // Stand on the ground (or bridge) and pitch with the slope.
        private void Place()
        {
            Vector3 f = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var p = transform.position;
            float yFront = SurfaceY(p + f * 2.2f), yBack = SurfaceY(p - f * 2.2f);
            p.y = Mathf.Lerp(p.y, (yFront + yBack) * 0.5f, 1f - Mathf.Exp(-10f * Time.deltaTime));
            transform.position = p;
            float pitch = -Mathf.Atan2(yFront - yBack, 4.4f) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void Light()
        {
            state = State.Lit;
            fuse = fuseTime;
            charge = 0f;
            sparks = BlastFx.FuseSparks(transform, fuseLocal);
            sparks.transform.SetParent(transform, true);
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
            Place();
            if (fuse <= 0f) Boom();
        }

        private void Boom()
        {
            Vector3 c = transform.position + Vector3.up * 1.4f;
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
            idx = Mathf.Min(1, stops.Length - 1);
            dir = 1;
            transform.position = stops[0];
            dwellT = 3f;
            charge = 0f;
            state = State.Roam;
            HudNotice.Show("A fresh TNT cart rolls into the village");
        }

        private void OnGUI()
        {
            float k = Screen.height / 1080f;
            if (state == State.Roam && charge > 0.01f || state == State.Roam && player &&
                (player.position - transform.position).sqrMagnitude < igniteRange * igniteRange)
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
