using UnityEngine;

namespace Amusnet.Mess
{
    // Poses a villager on a boat with code (the character rigs only ship a Walk clip): sitting calmly while moored,
    // standing and flailing when the boat breaks loose, then treading water and swimming for the bank when it snaps.
    // Limbs are aimed with character-space direction targets so it does not depend on the rig's bone axes.
    public sealed class BoatPassenger : MonoBehaviour
    {
        private enum State { Seated, Panic, Swim, Safe, Swept, Lost }

        [SerializeField] private float seatDrop = 0.42f;
        [SerializeField] private float standTime = 0.5f;
        [SerializeField] private float swimSpeed = 1.1f;

        private sealed class Limb
        {
            public Transform bone;
            public Vector3 bindDir;      // direction to the child bone, in character space
            public Quaternion bindRot;   // rotation in character space
        }

        private Transform hips, head;
        private Limb thighL, thighR, shinL, shinR, armL, armR, foreL, foreR;
        private Vector3 hipsBindPos;
        private Quaternion hipsBindLocal, headBindLocal;
        private float stand, panicTime = -1f, seed, waterY, dropT;
        private State state;
        private Vector3 swimDir;
        private Terrain terrain;

        public bool IsSwimming => state == State.Swim;
        public bool IsSafe => state == State.Safe;
        public bool IsLost => state == State.Lost;
        // In the water and still in trouble: swimming for the bank or swept helplessly.
        public bool NeedsHelp => (state == State.Swept || state == State.Swim) && !rescued;
        public bool IsSwept => state == State.Swept;
        private float swimMult = 1f, sweptT, lostT;
        private bool rescued;
        [SerializeField] private float drownTime = 40f;

        // Thrown into the current with nowhere to swim: drifts, flails, and drowns unless someone pulls them out.
        public void Swept(float surfaceY, Vector3 dirToBank)
        {
            Swim(surfaceY, dirToBank);
            state = State.Swept;
            sweptT = 0f;
        }

        // A rescuer has hold of them: haul to the bank quickly.
        public void Rescue(Vector3 dirToBank)
        {
            swimDir = new Vector3(dirToBank.x, 0f, dirToBank.z).normalized;
            swimMult = 2.2f;
            rescued = true;
            dropT = 1f;
            state = State.Swim;
        }

        public void Panic() { state = State.Panic; panicTime = 0f; }
        public void Calm() { state = State.Seated; }

        // Fall into the water and swim along dirToBank (horizontal) until the ground rises out of the water.
        public void Swim(float surfaceY, Vector3 dirToBank)
        {
            state = State.Swim;
            waterY = surfaceY;
            swimDir = new Vector3(dirToBank.x, 0f, dirToBank.z).normalized;
            swimDir = (swimDir + transform.right * Random.Range(-0.25f, 0.25f)).normalized;
            terrain = Terrain.activeTerrain;
            dropT = 0f;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        private void Awake()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator) animator.enabled = false;
            seed = Random.value * 10f;
            hips = Find("Hips"); head = Find("Head");
            hipsBindPos = transform.InverseTransformPoint(hips.position);
            hipsBindLocal = hips.localRotation; headBindLocal = head.localRotation;
            thighL = Make("Thigh.L", "Shin.L"); thighR = Make("Thigh.R", "Shin.R");
            shinL = Make("Shin.L", "Foot.L"); shinR = Make("Shin.R", "Foot.R");
            armL = Make("UpperArm.L", "Forearm.L"); armR = Make("UpperArm.R", "Forearm.R");
            foreL = Make("Forearm.L", "Hand.L"); foreR = Make("Forearm.R", "Hand.R");
        }

        private Transform Find(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        private Limb Make(string bone, string child)
        {
            var b = Find(bone); var c = Find(child);
            return new Limb
            {
                bone = b,
                bindDir = transform.InverseTransformDirection((c.position - b.position).normalized),
                bindRot = Quaternion.Inverse(transform.rotation) * b.rotation
            };
        }

        // dir is in character space.
        private void Aim(Limb l, Vector3 dir)
        {
            var from = transform.TransformDirection(l.bindDir);
            var to = transform.TransformDirection(dir.normalized);
            l.bone.rotation = Quaternion.FromToRotation(from, to) * (transform.rotation * l.bindRot);
        }

        private float GroundY(Vector3 p) => terrain ? terrain.SampleHeight(p) + terrain.transform.position.y : -1000f;

        private void Update()
        {
            if (state == State.Swept || state == State.Lost) { UpdateSwept(); return; }
            if (state != State.Swim) return;
            // Splash down from the boat, then paddle toward the bank until the ground comes up out of the water.
            dropT += Time.deltaTime;
            Vector3 p = transform.position;
            float floatY = waterY - hipsBindPos.y + 0.12f + Mathf.Sin(Time.time * 2.2f + seed) * 0.03f;
            p.y = Mathf.MoveTowards(p.y, floatY, Time.deltaTime * 3f);
            if (dropT > 0.5f)
            {
                p += swimDir * (swimSpeed * swimMult * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(swimDir), Time.deltaTime * 4f);
            }
            transform.position = p;
            float ground = GroundY(p + swimDir * 0.6f);
            if (dropT > 1f && GroundY(p) > waterY - 0.55f)
            {
                state = State.Safe;
                p.y = GroundY(p);
                transform.position = p;
            }
        }

        private void UpdateSwept()
        {
            Vector3 p = transform.position;
            if (state == State.Lost)
            {
                lostT += Time.deltaTime;
                p.y -= 0.6f * Time.deltaTime;
                transform.position = p;
                if (lostT > 2.5f) gameObject.SetActive(false);
                return;
            }
            sweptT += Time.deltaTime;
            dropT += Time.deltaTime;
            float floatY = waterY - hipsBindPos.y + 0.12f + Mathf.Sin(Time.time * 2.2f + seed) * 0.05f;
            p.y = Mathf.MoveTowards(p.y, floatY, Time.deltaTime * 3f);
            // The current carries them along while they drift slowly toward the nearer bank.
            if (p.x < 74f) p.x += 0.3f * Time.deltaTime;
            p += swimDir * (0.2f * Time.deltaTime);
            transform.position = p;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(swimDir), Time.deltaTime * 2f);
            if (sweptT > 14f && GroundY(p) > waterY + 0.1f) { state = State.Safe; p.y = GroundY(p); transform.position = p; return; }
            if (sweptT > drownTime) { state = State.Lost; lostT = 0f; Amusnet.Mess.HudNotice.Show("The villager was swept away..."); }
        }

        private void LateUpdate()
        {
            float t = Time.time + seed;
            float standTarget = state == State.Seated ? 0f : 1f;
            stand = Mathf.MoveTowards(stand, standTarget, Time.deltaTime / standTime);
            float s = Mathf.SmoothStep(0f, 1f, stand);
            if (state == State.Panic) panicTime += Time.deltaTime;

            bool swim = state == State.Swim || state == State.Swept;
            float flail = state == State.Panic ? Mathf.Clamp01(panicTime * 3f) : state == State.Swept ? 1f : 0f;
            // Hips drop when seated and sway with the boat.
            hips.position = transform.TransformPoint(hipsBindPos + Vector3.down * (seatDrop * (1f - s)));
            hips.localRotation = hipsBindLocal;
            float sway = state == State.Safe ? 0f : 2f + 10f * flail;
            hips.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 1.3f) * sway, transform.forward) * hips.rotation;

            // Seated: thighs forward, shins down. Standing/swimming: legs down (kicking while swimming).
            Vector3 thighSit = new Vector3(0f, -0.08f, 1f), thighStand = Vector3.down;
            Vector3 shinSit = new Vector3(0f, -1f, -0.1f), shinStand = Vector3.down;
            float kick = swim ? Mathf.Sin(t * 8f) : 0f;
            Aim(thighL, Vector3.Slerp(thighSit, thighStand + new Vector3(0f, 0f, 0.45f * kick), s));
            Aim(thighR, Vector3.Slerp(thighSit, thighStand - new Vector3(0f, 0f, 0.45f * kick), s));
            Aim(shinL, Vector3.Slerp(shinSit, shinStand + new Vector3(0f, 0f, -0.3f * kick), s));
            Aim(shinR, Vector3.Slerp(shinSit, shinStand + new Vector3(0f, 0f, 0.3f * kick), s));

            // Arms: resting on the lap when seated, windmilling when panicking, paddling when swimming.
            float w = Mathf.Sin(t * 9f), w2 = Mathf.Sin(t * 9f + 2.1f);
            Vector3 armRest = state == State.Safe ? new Vector3(0f, -1f, 0.05f) : new Vector3(0f, -1f, 0.35f);
            Vector3 foreRest = state == State.Safe ? new Vector3(0f, -1f, 0.15f) : new Vector3(0f, -0.25f, 1f);
            Vector3 armUpL = new Vector3(-0.6f, 0.8f + 0.25f * w, 0.2f), armUpR = new Vector3(0.6f, 0.8f + 0.25f * w2, 0.2f);
            Vector3 foreUpL = new Vector3(-0.3f + 0.7f * w2, 1f, 0.1f), foreUpR = new Vector3(0.3f + 0.7f * w, 1f, 0.1f);
            float sw = Mathf.Sin(t * 5f), sw2 = Mathf.Sin(t * 5f + Mathf.PI);
            Vector3 armSwL = new Vector3(-0.5f, -0.35f + 0.45f * sw, 0.8f), armSwR = new Vector3(0.5f, -0.35f + 0.45f * sw2, 0.8f);
            Vector3 foreSwL = new Vector3(-0.2f, -0.2f + 0.5f * sw, 1f), foreSwR = new Vector3(0.2f, -0.2f + 0.5f * sw2, 1f);
            Vector3 aL = Vector3.Slerp(armRest, armUpL, flail), aR = Vector3.Slerp(armRest, armUpR, flail);
            Vector3 fL = Vector3.Slerp(foreRest, foreUpL, flail), fR = Vector3.Slerp(foreRest, foreUpR, flail);
            if (swim && state != State.Swept) { aL = armSwL; aR = armSwR; fL = foreSwL; fR = foreSwR; }
            Aim(armL, aL); Aim(armR, aR);
            Aim(foreL, fL); Aim(foreR, fR);

            // Head glances around, and looks back at the shore while panicking.
            head.localRotation = headBindLocal;
            float glance = swim ? 8f : 20f + 50f * flail;
            head.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 0.7f) * glance, transform.up) * head.rotation;
        }
    }
}
