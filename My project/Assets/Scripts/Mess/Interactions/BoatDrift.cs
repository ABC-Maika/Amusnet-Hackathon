using Amusnet.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Amusnet.Mess
{
    // Sits on a trigger zone around a moored boat. Pressing E near it (or a wing gust) cuts the rope and the creek
    // (plus wind) carries the boat downstream. Both boats can be cut. An intact bridge snaps the hull; a blown-up bridge lets the
    // boat reach the weir, where it capsizes and the villagers are swept away until rescuers pull them out.
    public sealed class BoatDrift : Interactable
    {
        [SerializeField] private Transform boat;
        [SerializeField] private Transform mooring;
        [SerializeField] private BoatDrift rival;
        [SerializeField] private WindSystem wind;
        [SerializeField] private Vector3 flow = Vector3.right;
        [SerializeField] private float currentSpeed = 0.85f;
        [SerializeField] private float windInfluence = 0.6f;
        [SerializeField] private float laneZ = -19f;
        [SerializeField] private float stopX = 70f;
        [SerializeField] private float cutReach = 7f;
        [SerializeField] private string cutLabel = "Cut the rope";
        [SerializeField] private BoatPassenger[] passengers;
        [SerializeField] private LineRenderer rope;
        [SerializeField] private Vector3 bowLocal = Vector3.forward;   // boat-local direction the pointed end faces
        [SerializeField] private float halfLength = 2.4f;
        [SerializeField] private float bridgeX = 1e9f;                 // the boat snaps when its bow reaches this x
        [SerializeField] private Transform bowHalf, sternHalf;         // pre-split hull pieces, hidden until the snap
        [SerializeField] private Transform[] cargo;
        [SerializeField] private Vector3 swimTo = Vector3.forward;     // direction of the nearest bank
        [SerializeField] private Destructible bridge;                  // the bridge the hull would hit
        [SerializeField] private float slowReach = 10f;                // time slows this close to the rope

        private static BoatDrift prompting;
        private Transform player;
        private Vector3 ropeEnd, ropeStart;
        private float seed, speed, surfaceY, kick, cutAt = -10f, locked;
        private bool drifting, snapped, capsized;
        private float capsizeAt;
        private float yaw, snapAt;
        private Vector3 halfBowPos, halfSternPos;
        public bool IsDrifting => drifting;
        public bool IsLocked => locked > 0f;
        public bool HasSnapped => snapped;
        public bool HasCapsized => capsized;
        public Transform Boat => boat;
        public BoatPassenger[] Passengers => passengers;
        public Vector3 CutPoint => mooring ? Vector3.Lerp(ropeStart, RopeBoatPoint(), 0.5f) : boat.position;
        public bool CanBeCut => !drifting && locked == 0f && isActiveAndEnabled;
        public bool HasArrived { get; private set; }

        private void Awake()
        {
            flow = new Vector3(flow.x, 0f, flow.z).normalized;
            surfaceY = boat.position.y;
            yaw = boat.eulerAngles.y;
            bowLocal = new Vector3(bowLocal.x, 0f, bowLocal.z).normalized;
            if (bowHalf) halfBowPos = bowHalf.localPosition;
            if (sternHalf) halfSternPos = sternHalf.localPosition;
            seed = Random.value * 10f;
            if (!wind) wind = FindAnyObjectByType<WindSystem>();
            var c = FindAnyObjectByType<ButterflyController>();
            if (c) player = c.transform;
            if (mooring) ropeStart = mooring.position + Vector3.up * 0.5f;
        }

        protected override void OnInteracted(MessManager manager)
        {
            drifting = true;
            cutAt = Time.time;
            kick = 1f;
            ropeEnd = RopeBoatPoint();
            foreach (var p in passengers) if (p) p.Panic();
        }

        // The other boat was chosen: this one stays tied up for good.
        public void Lock()
        {
            locked = 1f;
            if (TryGetComponent<Collider>(out var zone)) zone.enabled = false;
        }

        // Cut by pressing E close to the boat; works with or without the mess/score systems loaded.
        private void Cut()
        {
            var manager = FindAnyObjectByType<MessManager>();
            if (manager) { if (TryInteract(manager)) return; }
            if (!HasInteracted && locked == 0f && !drifting) OnInteracted(null);
        }

        private Vector3 RopeBoatPoint() => boat.position + Vector3.up * 0.5f;
        private bool CanCut => !drifting && locked == 0f && isActiveAndEnabled && player &&
            (player.position - boat.position).sqrMagnitude < cutReach * cutReach;
        private bool NearRope => !drifting && locked == 0f && isActiveAndEnabled && player &&
            (player.position - CutPoint).sqrMagnitude < slowReach * slowReach;

        private void Update()
        {
            if (NearRope) TimeSlow.Hold(0.4f);
            if (CanCut)
            {
                // Closest boat in range owns the prompt and the key.
                if (!prompting || prompting == this || !prompting.CanCut ||
                    (player.position - boat.position).sqrMagnitude < (player.position - prompting.boat.position).sqrMagnitude)
                    prompting = this;
                if (prompting == this && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Cut();
            }
            else if (prompting == this) prompting = null;

            float t = Time.time + seed;
            Vector3 p = boat.position;
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 0.8f);
            if (drifting)
            {
                Vector3 push = wind ? wind.Sample(p, Time.time) : Vector3.zero;
                float target = currentSpeed + Mathf.Max(0f, Vector3.Dot(push, flow)) * windInfluence;
                if (p.x >= stopX || snapped) target = 0f; // ends against the weir / wrecked at the bridge
                // The cut gives the boat a lurch before the current takes over.
                speed = Mathf.MoveTowards(speed, target + (snapped ? 0f : kick * 0.9f), Time.deltaTime * (snapped ? 2.5f : 1.2f));
                p += flow * (speed * Time.deltaTime);
                if (!snapped) p.z = Mathf.Lerp(p.z, RiverLane.ZAt(p.x, laneZ), 1f - Exp(snapped ? 0f : 2f));
                if (speed < 0.02f && p.x >= stopX) HasArrived = true;
                // Nose first: turn until the bow points downstream.
                float want = Mathf.Atan2(flow.x, flow.z) * Mathf.Rad2Deg - Mathf.Atan2(bowLocal.x, bowLocal.z) * Mathf.Rad2Deg;
                if (!snapped) yaw = Mathf.LerpAngle(yaw, want, 1f - Exp(0.9f));
                if (!snapped && !capsized && p.x + halfLength * flow.x >= bridgeX && !(bridge && bridge.IsDestroyed)) Snap();
                if (!snapped && !capsized && p.x >= stopX - 0.5f && bridge && bridge.IsDestroyed) Capsize();
            }
            if (snapped) AnimateWreck();
            float capRoll = 0f;
            if (capsized)
            {
                float ct = Time.time - capsizeAt;
                capRoll = 100f * (1f - Mathf.Exp(-1.4f * ct));
                surfaceY = Mathf.MoveTowards(surfaceY, -2.2f, Time.deltaTime * Mathf.Clamp01((ct - 2f) / 3f) * 0.5f);
            }
            // Gentle bob and rock; stronger while moving, and a hard rock right after the cut.
            float amount = (drifting ? 1f : 0.45f) + kick * 2.5f;
            p.y = surfaceY + Mathf.Sin(t * 1.6f) * 0.04f * amount;
            boat.position = p;
            boat.rotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 1.4f * amount, yaw, Mathf.Sin(t * 1.1f + 1f) * 2f * amount + Mathf.Sin(t * 6f) * kick * 4f + capRoll);

            UpdateRope();
        }

        private static float Exp(float k) => Mathf.Exp(-k * Time.deltaTime);

        // The bridge breaks the hull in two: the pieces tilt apart, the villagers fall in and swim for the bank.
        private void Snap()
        {
            snapped = true;
            snapAt = Time.time;
            HasArrived = true;
            if (boat.TryGetComponent<Renderer>(out var hull)) hull.enabled = false;
            if (boat.TryGetComponent<Collider>(out var hullCol)) hullCol.enabled = false;
            if (bowHalf) bowHalf.gameObject.SetActive(true);
            if (sternHalf) sternHalf.gameObject.SetActive(true);
            if (cargo != null)
                foreach (var c in cargo)
                    if (c) c.SetParent(Vector3.Dot(boat.InverseTransformPoint(c.position), bowLocal) > 0f ? bowHalf : sternHalf, true);
            foreach (var p in passengers)
                if (p) { p.transform.SetParent(null, true); p.Swim(surfaceY, swimTo); }
        }

        // No bridge to break the hull: it rolls over against the weir and everyone aboard is swept into the current.
        private void Capsize()
        {
            capsized = true;
            capsizeAt = Time.time;
            HasArrived = true;
            HudNotice.Show("The boat capsizes at the weir!");
            foreach (var p in passengers)
                if (p) { p.transform.SetParent(null, true); p.Swept(surfaceY, swimTo); }
        }

        private void AnimateWreck()
        {
            float t = Time.time - snapAt;
            float k = 1f - Mathf.Exp(-1.6f * t);
            Vector3 up = Vector3.up, tilt = Vector3.Cross(bowLocal, up);
            float sink = Mathf.Clamp01((t - 2.5f) / 4f);
            if (bowHalf)
            {
                bowHalf.localRotation = Quaternion.AngleAxis(24f * k, tilt) * Quaternion.AngleAxis(-18f * k, up);
                bowHalf.localPosition = halfBowPos + bowLocal * (0.5f * k) + Vector3.down * (1.2f * sink * sink) + Vector3.up * (0.12f * k);
            }
            if (sternHalf)
            {
                sternHalf.localRotation = Quaternion.AngleAxis(-30f * k, tilt) * Quaternion.AngleAxis(16f * k, up);
                sternHalf.localPosition = halfSternPos - bowLocal * (0.5f * k) + Vector3.down * (0.35f * k + 1.6f * sink * sink);
            }
            if (sink >= 1f)
            {
                if (bowHalf) bowHalf.gameObject.SetActive(false);
                if (sternHalf) sternHalf.gameObject.SetActive(false);
            }
        }

        // Taut to the boat while moored; once cut, the free end falls into the water by the post.
        private void UpdateRope()
        {
            if (!rope || !mooring) return;
            Vector3 start = mooring.position + Vector3.up * 0.5f;
            if (!drifting)
            {
                rope.SetPosition(0, start);
                rope.SetPosition(1, RopeBoatPoint());
                return;
            }
            float k = Mathf.Clamp01((Time.time - cutAt) / 0.7f);
            Vector3 end = Vector3.Lerp(ropeEnd, new Vector3(start.x, surfaceY + 0.05f, start.z + 0.4f), k * k);
            rope.SetPosition(0, start);
            rope.SetPosition(1, end);
        }

        private void OnGUI()
        {
            if (prompting != this || !CanCut) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width * 0.5f - 130f, Screen.height - 120f, 260f, 38f), "E   " + cutLabel, style);
        }
    }
}
