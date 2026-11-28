using UnityEngine;

namespace Amusnet.World
{
    public sealed class GoatLaundryPreview : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Garment
        {
            public BoxCollider pollenTarget;
            public GameObject pollenMark;
            public Transform cloth;
            [System.NonSerialized] public int pollen;
            [System.NonSerialized] public bool pulled;
        }

        private enum Phase { Waiting, Approaching, ApproachingVillager, Tugging, Dropping, Returning, BackingUp, Dragging, RunningThroughGate, Finished, PatrolBump }

        [SerializeField] private ParticleSystem pollen;
        [SerializeField] private WindSystem wind;
        [SerializeField] private Garment shirt;
        [SerializeField] private Garment sheet;
        [SerializeField] private Transform line;
        [SerializeField] private Transform goat;
        [SerializeField] private Transform head;
        [SerializeField] private Transform[] legs;
        [SerializeField] private LaundryGatePreview gate;
        [SerializeField] private RollingTimberPreview timber;
        [SerializeField] private Transform goatRear;
        [SerializeField, Min(1)] private int pollenNeeded = 4;

        private ParticleSystem.Particle[] particles;
        private Phase phase;
        private Garment target;
        private Vector3 home;
        private Vector3 approach;
        private Vector3 dropStart;
        private Quaternion homeRotation;
        private Quaternion dropRotation;
        private float elapsed;
        private float gait;
        private bool walking;
        private float headPitch;
        private Vector3 sheetStartPosition;
        private Quaternion sheetStartRotation;
        private Vector3 sheetStartScale;
        private Vector3 sheetEndPosition;
        private Quaternion sheetEndRotation;
        private Vector3 sheetEndScale;
        private float sheetBlend = 1f;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private LayerMask villagerLayers;
        [SerializeField] private float detectionRadius = 4f;
        [SerializeField] private float bumpRadius = 0.8f;
        [SerializeField] private UnityEngine.Events.UnityEvent<Transform> onPatrolBump = new();

        private int patrolIndex;
        private Transform bumpTarget;
        private float nextBumpTime;

        public bool ShirtPulled => shirt.pulled;
        public bool LineDown { get; private set; }
        public bool HasGrabbedSheet { get; private set; }
        public bool GoatEscaped { get; private set; }
        public Vector3 GoatPosition => goat.position;
        public bool IsInSheetChase => target == sheet && phase is Phase.Approaching or Phase.Tugging
            or Phase.Dropping or Phase.BackingUp or Phase.Dragging or Phase.RunningThroughGate;

        public bool TryCollectSheet(Transform basket)
        {
            if (sheet.pulled)
                return false;

            sheet.pulled = true;
            MoveSheetTo(basket, Vector3.zero, Quaternion.Euler(88f, 0f, 0f), Vector3.one * 0.28f);
            if (target == sheet && (phase == Phase.Approaching || phase == Phase.Tugging))
            {
                SetPhase(Phase.Returning);
            }
            return true;
        }

        private void Awake()
        {
            if (!pollen) return; // resolved later from the player (UsePlayerPollen)
            home = goat.position;
            homeRotation = goat.rotation;
            particles = new ParticleSystem.Particle[pollen.main.maxParticles];
            var drift = pollen.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
        }

        // In the game the map's preview butterfly (and its pollen) is switched off: use the player's pollen.
        private bool UsePlayerPollen()
        {
            if (pollen && pollen.gameObject.activeInHierarchy) return true;
            if (!ButterflyPollen.Active) return false;
            pollen = ButterflyPollen.Active.GetComponent<ParticleSystem>();
            particles = new ParticleSystem.Particle[pollen.main.maxParticles];
            var drift = pollen.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            return true;
        }

        private void LateUpdate()
        {
            if (!UsePlayerPollen()) return;
            Vector3 air = wind ? wind.Sample(pollen.transform.position, Time.time) : Vector3.zero;
            var drift = pollen.velocityOverLifetime;
            drift.x = air.x;
            drift.y = air.y;
            drift.z = air.z;

            if (phase != Phase.Finished && phase != Phase.Dropping)
                CatchPollen();

            elapsed += Time.deltaTime;
            walking = false;
            headPitch = 0f;
            switch (phase)
            {
                case Phase.Waiting:
                    if (gate && gate.CanGoatApproach)
                    {
                        SetPhase(Phase.ApproachingVillager);
                        break;
                    }
                    if (!sheet.pulled && sheet.pollen >= pollenNeeded)
                        Approach(sheet);
                    else if (!shirt.pulled && shirt.pollen >= pollenNeeded)
                        Approach(shirt);
                    else
                        Patrol();
                    break;
                case Phase.Approaching:
                    if (WalkTo(approach))
                        SetPhase(Phase.Tugging);
                    break;
                case Phase.Tugging:
                    Tug();
                    break;
                case Phase.Dropping:
                    Drop();
                    break;
                case Phase.Returning:
                    if (WalkTo(home))
                        SetPhase(Phase.Waiting);
                    break;
                case Phase.BackingUp:
                    BackIntoTimber();
                    break;
                case Phase.ApproachingVillager:
                    if (!gate || !gate.CanGoatApproach)
                    {
                        SetPhase(Phase.Returning);
                        break;
                    }
                    WalkTo(gate.VillagerPosition);
                    break;
                case Phase.PatrolBump:
                    if (!bumpTarget || !bumpTarget.gameObject.activeInHierarchy ||
                        (bumpTarget.position - goat.position).sqrMagnitude >
                            detectionRadius * detectionRadius)
                    {
                        SetPhase(Phase.Waiting);
                        break;
                    }

                    if ((bumpTarget.position - goat.position).sqrMagnitude <= bumpRadius * bumpRadius)
                    {
                        nextBumpTime = Time.time + 3f;
                        SetPhase(Phase.Waiting);
                        onPatrolBump.Invoke(bumpTarget);
                        break;
                    }

                    WalkTo(bumpTarget.position);
                    break;
                case Phase.Finished:
                    if (gate && gate.CanGoatApproach)
                        SetPhase(Phase.ApproachingVillager);
                    break;
                case Phase.Dragging:
                case Phase.RunningThroughGate:
                    RunToExit();
                    break;
            }

            if (!walking)
                foreach (Transform leg in legs)
                    leg.localRotation = Quaternion.RotateTowards(leg.localRotation, Quaternion.identity, 220f * Time.deltaTime);
            head.localRotation = Quaternion.RotateTowards(head.localRotation,
                Quaternion.Euler(headPitch, 0f, 0f), 280f * Time.deltaTime);
            BlendSheetPose();
        }
        private void Patrol()
        {
            if (Time.time >= nextBumpTime)
            {
                Transform nearest = null;
                float nearestDistance = detectionRadius * detectionRadius;

                foreach (Collider hit in Physics.OverlapSphere(
     goat.position, detectionRadius, villagerLayers, QueryTriggerInteraction.Ignore))
                {
                    Transform person = hit.transform;

                    if (!person || !person.gameObject.activeInHierarchy)
                        continue;

                    float distance = (person.position - goat.position).sqrMagnitude;
                    if (distance <= nearestDistance)
                    {
                        nearest = person;
                        nearestDistance = distance;
                    }
                }

                if (nearest)
                {
                    bumpTarget = nearest;
                    SetPhase(Phase.PatrolBump);
                    return;
                }
            }

            if (patrolPoints.Length > 0 && WalkTo(patrolPoints[patrolIndex].position))
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
        private void CatchPollen()
        {
            int count = pollen.GetParticles(particles);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = particles[i].position;
                if (Deposit(sheet, position) || Deposit(shirt, position))
                {
                    particles[i].remainingLifetime = -1f;
                    changed = true;
                }
            }
            if (changed)
                pollen.SetParticles(particles, count);
        }

        private bool Deposit(Garment garment, Vector3 position)
        {
            if (garment.pulled || !garment.pollenTarget.bounds.Contains(position))
                return false;

            garment.pollen = Mathf.Min(pollenNeeded, garment.pollen + 1);
            garment.pollenMark.SetActive(true);
            float coverage = (float)garment.pollen / pollenNeeded;
            garment.pollenMark.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1f, coverage);
            return true;
        }

        private void Approach(Garment garment)
        {
            target = garment;
            approach = garment.pollenTarget.transform.position;
            approach.y = home.y;
            approach += line.forward * 0.8f;
            SetPhase(Phase.Approaching);
        }

        private bool WalkTo(Vector3 destination, bool backwards = false)
        {
            Vector3 offset = destination - goat.position;
            if (offset.sqrMagnitude < 0.005f)
                return true;

            walking = true;
            goat.rotation = Quaternion.RotateTowards(goat.rotation,
                Quaternion.LookRotation(backwards ? -offset : offset), 420f * Time.deltaTime);
            goat.position = Vector3.MoveTowards(goat.position, destination, 2f * Time.deltaTime);
            gait += (backwards ? -12f : 12f) * Time.deltaTime;
            for (int i = 0; i < legs.Length; i++)
            {
                Quaternion step = Quaternion.Euler(Mathf.Sin(gait + i * Mathf.PI) * 21f, 0f, 0f);
                legs[i].localRotation = Quaternion.RotateTowards(legs[i].localRotation, step, 300f * Time.deltaTime);
            }
            return false;
        }

        private void Tug()
        {
            Vector3 towardCloth = target.pollenTarget.transform.position - goat.position;
            towardCloth.y = 0f;
            goat.rotation = Quaternion.RotateTowards(
                goat.rotation, Quaternion.LookRotation(towardCloth), 420f * Time.deltaTime);
            headPitch = -12f + Mathf.Sin(elapsed * 19f) * 12f;
            if (elapsed < 0.85f)
                return;

            target.pulled = true;
            bool grabbedSheet = target == sheet;
            HasGrabbedSheet |= grabbedSheet;
            if (grabbedSheet)
                MoveSheetTo(goat, new Vector3(0f, 0.56f, 1.5f), Quaternion.Euler(-49f, 0f, 0f), Vector3.one);
            Transform falling = grabbedSheet ? line : target.cloth;
            dropStart = falling.localPosition;
            dropRotation = falling.localRotation;
            SetPhase(Phase.Dropping);
        }

        private void Drop()
        {
            float amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.65f));
            if (target == sheet)
            {
                // Fall away from the goat, pivoting the posts at ground level.
                line.localRotation = dropRotation * Quaternion.Euler(-87f * amount, 0f, 0f);
            }
            else
            {
                target.cloth.localPosition = Vector3.Lerp(dropStart,
                    new Vector3(dropStart.x, 0.12f, dropStart.z + 0.35f), amount);
                target.cloth.localRotation = dropRotation * Quaternion.Euler(85f * amount, 0f, 12f * amount);
            }

            if (elapsed < 0.65f)
                return;

            if (target != sheet)
            {
                target.cloth.SetParent(transform, true);
                SetPhase(Phase.Returning);
                return;
            }

            LineDown = true;
            SetPhase(Phase.BackingUp);
        }

        private void BackIntoTimber()
        {
            if (timber)
            {
                bool stopped = WalkTo(timber.BackUpPoint.position, true);
                if (!timber.TryRelease(goatRear.position) && !stopped)
                    return;
            }

            if (gate && gate.IsGateOpen)
            {
                MoveSheetTo(goat, new Vector3(0.12f, 0.43f, 1.5f), Quaternion.Euler(-57f, 0f, 0f), Vector3.one);
                SetPhase(Phase.Dragging);
                return;
            }

            DropCarriedSheet();
            SetPhase(Phase.Finished);
        }

        private void DropCarriedSheet()
        {
            Vector3 position = transform.InverseTransformPoint(sheet.cloth.position);
            position.y = 0.055f;
            Quaternion flatOnGround = Quaternion.Euler(-90f, goat.eulerAngles.y, 0f);
            MoveSheetTo(transform, position, Quaternion.Inverse(transform.rotation) * flatOnGround, Vector3.one);
        }

        private void MoveSheetTo(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Transform cloth = sheet.cloth;
            cloth.SetParent(parent, true);
            sheetStartPosition = cloth.localPosition;
            sheetStartRotation = cloth.localRotation;
            sheetStartScale = cloth.localScale;
            sheetEndPosition = position;
            sheetEndRotation = rotation;
            sheetEndScale = scale;
            sheetBlend = 0f;
        }

        private void BlendSheetPose()
        {
            if (sheetBlend >= 1f)
                return;

            sheetBlend = Mathf.Clamp01(sheetBlend + Time.deltaTime / 0.28f);
            float amount = Mathf.SmoothStep(0f, 1f, sheetBlend);
            sheet.cloth.localPosition = Vector3.Lerp(sheetStartPosition, sheetEndPosition, amount);
            sheet.cloth.localRotation = Quaternion.Slerp(sheetStartRotation, sheetEndRotation, amount);
            sheet.cloth.localScale = Vector3.Lerp(sheetStartScale, sheetEndScale, amount);
        }

        private void RunToExit()
        {
            bool reachedExit = WalkTo(gate.EscapePoint.position);
            if (phase == Phase.Dragging && (reachedExit || gate.IsNearSheetDrop(goat.position)))
            {
                DropCarriedSheet();
                SetPhase(Phase.RunningThroughGate);
            }

            if (reachedExit)
            {
                GoatEscaped = true;
                SetPhase(Phase.Finished);
            }
        }

        private void SetPhase(Phase next)
        {
            phase = next;
            elapsed = 0f;
        }

        private void OnGUI()
        {
            string status = phase switch
            {
                Phase.Approaching => "The goat spotted the pollen.",
                Phase.Tugging => "The goat is tugging on the laundry.",
                Phase.Dropping => "The laundry is coming down!",
                Phase.BackingUp => "The goat got the sheet and is backing up!",
                Phase.Dragging => "The goat is dragging the sheet toward the gate!",
                Phase.RunningThroughGate => "The goat dropped the sheet and is running for the gate!",
                Phase.Finished when GoatEscaped => "The goat escaped through the gate!",
                Phase.Finished => "The goat pulled down the washing line.",
                _ when gate && gate.SheetCollected => "The villager brought the sheet in.",
                _ when shirt.pulled => "Shirt pulled down. Dust the bedsheet to topple the line.",
                _ => "Hold Space near the laundry to dust it with pollen."
            };
            GUI.Box(new Rect(16, 16, Mathf.Min(460, Screen.width - 32), 42), status);
        }
    }
}
