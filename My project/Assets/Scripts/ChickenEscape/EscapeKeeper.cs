using UnityEngine;
using UnityEngine.AI;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EscapeKeeper : MonoBehaviour
    {
        public enum PatrolState
        {
            Patrolling, Investigating, Distracted, Pursuing, Windup, Lunging, ClosingGate, Recovering,
            ScoopingHen, CarryingHen, PuttingHenDown, CorneringDog, HoldingDog
        }

        public const float SightRange = 7.2f;
        public const float SightAngle = 49f;

        const float EyeHeight = .80f;
        const float BarkHearing = 12.8f;
        const float HenGrabDistance = 1.18f;
        const float HenLungeRange = 2.3f;
        const float DogReach = 1.5f;
        const float DogWarningTime = 1.24f;
        const float DogRescueTime = 6f;
        const float WindupTime = .60f;
        const float ScoopTime = .81f;
        const float SetDownTime = .62f;
        const float DecoyDuration = 4.45f;
        const float DecoyBreakRange = 2.65f;
        const float PursuitMemory = 2.2f;
        const float GateGrace = 3.2f;
        static readonly float ViewDot = Mathf.Cos(SightAngle * Mathf.Deg2Rad);
        static readonly Vector3[] Patrol = {
            new(6.05f, 0f, 1.35f), new(4.85f, 0f, -4.75f), new(1.6f, 0f, -5.44f),
            new(-5.65f, 0f, -3.8f), new(-6.0f, 0f, 1.14f), new(-.35f, 0f, 1.24f)
        };

        [SerializeField] Animator animator;
        [SerializeField] Transform returnApproach, holdingPen;

        NavMeshAgent agent;
        MarketEscapeRun yard;
        EscapeKeeperRig rig;
        EscapeHen quarry, inHands;
        EscapeDog heldDog, dogInReach;
        Vector3 barkPoint, decoyPoint, lastSeen, strikePoint;
        Vector3 henPositionOnGrab, dropPoint, firstPosition;
        float proceedsAt, nextPathAt, barkUntil, decoyUntil, tracksUntil, dogReachTime;
        int waypoint, pose, pennedHens;
        bool started;

        public PatrolState Current { get; private set; }
        public bool HasSpottedSomeone => Current is PatrolState.Pursuing or PatrolState.Windup or PatrolState.Lunging
            or PatrolState.CorneringDog or PatrolState.HoldingDog;
        public bool IsDistracted => Current == PatrolState.Distracted;
        public bool IsHandlingHen => inHands != null;
        public Vector3 DecoyPosition => decoyPoint;
        public Vector3 StrikePoint => strikePoint;
        public float DogPressure => Mathf.Clamp01(dogReachTime / DogWarningTime);
        public float DogRescueRemaining => heldDog ? Mathf.Max(0f, proceedsAt - Time.time) : 0f;
        public bool DogSecured => heldDog && Time.time >= proceedsAt;
        public int AgentType => agent.agentTypeID;
        NavMeshQueryFilter Navigation => new() { agentTypeID = AgentType, areaMask = agent.areaMask };

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rig = new EscapeKeeperRig(animator, transform);
            firstPosition = transform.position;
        }

        public void BindYard(MarketEscapeRun run) => yard = run;

        public bool HearBark(Vector3 point)
        {
            if (!Within(point, BarkHearing)) return false;
            barkPoint = point;
            barkUntil = Time.time + 4.05f;
            nextPathAt = 0f;
            return true;
        }

        public bool TryDistract(Vector3 point)
        {
            if (!started || IsHandlingHen ||
                Current is PatrolState.Windup or PatrolState.Lunging or PatrolState.HoldingDog ||
                !Within(point, 15f) || !NavMesh.SamplePosition(point, out var ground, 1.3f, Navigation))
                return false;

            decoyPoint = ground.position;
            decoyUntil = Time.time + DecoyDuration;
            tracksUntil = 0f;
            dogReachTime = 0f;
            return true;
        }

        public void ReleaseDog()
        {
            if (!heldDog) return;
            heldDog.BreakLoose();
            heldDog = null;
            dogInReach = null;
            dogReachTime = 0f;
            barkUntil = 0f;
            Recover(1.75f, "Startle");
        }

        public EscapeHen Step(EscapeDog dog, EscapeGate gate, EscapeHen[] flock, bool heist,
            bool hensInPen, bool passageBlocked, bool dogInYard)
        {
            if (!heist || !agent.isOnNavMesh) return null;
            rig?.Restore();
            if (!started)
            {
                started = true;
                agent.acceleration = 22f;
                agent.angularSpeed = 640f;
            }

            if (heldDog)
            {
                heldDog.PullToward(transform.position);
                Face(heldDog.transform.position);
                return null;
            }
            if (inHands)
            {
                ReturnHen();
                return null;
            }

            if (Current == PatrolState.Windup)
            {
                Face(strikePoint);
                if (Time.time < proceedsAt) return null;
                ChangeState(PatrolState.Lunging);
                proceedsAt = Time.time + .67f;
            }
            if (Current == PatrolState.Lunging) return Lunge();
            if (Current == PatrolState.Recovering && Time.time < proceedsAt) return null;

            Vector3 dogPosition = dog.transform.position;
            bool dogAvailable = dogInYard && dog.CanBeCaught;
            bool dogInGrabbingRange = dogAvailable && Within(dogPosition, DogReach) && ClearTo(dogPosition);
            EscapeHen hen = VisibleHen(flock);
            if (Time.time < decoyUntil)
            {
                bool henTooClose = hen && Within(hen.transform.position, DecoyBreakRange);
                if (!dogInGrabbingRange && !henTooClose)
                {
                    Go(PatrolState.Distracted, decoyPoint, 5.25f);
                    return null;
                }
                decoyUntil = 0f;
            }

            if (dogInGrabbingRange)
            {
                CornerDog(dog);
                return null;
            }

            dogInReach = null;
            dogReachTime = Mathf.MoveTowards(dogReachTime, 0f, 2.6f * Time.deltaTime);
            bool seesDog = dogAvailable && CanSee(dogPosition);
            if (hen || seesDog)
            {
                quarry = hen;
                lastSeen = hen ? hen.transform.position : dogPosition;
                tracksUntil = Time.time + PursuitMemory;
                if (hen && Within(lastSeen, HenLungeRange))
                {
                    dogReachTime = 0f;
                    ChangeState(PatrolState.Windup);
                    strikePoint = Lead(lastSeen, hen.Velocity, .31f);
                    proceedsAt = Time.time + WindupTime;
                    Stop("Watch");
                }
                else Go(PatrolState.Pursuing, Lead(lastSeen, hen ? hen.Velocity : dog.Velocity, .40f), 5.6f);
            }
            else if (Time.time < tracksUntil && (quarry ? !quarry.Saved && !quarry.Caught : dogInYard))
                Go(PatrolState.Pursuing, lastSeen, 5.05f);
            else if (Time.time < barkUntil) Go(PatrolState.Investigating, barkPoint, 4.75f);
            else if (gate.IsOpen && hensInPen && Time.time > gate.OpenedAt + GateGrace)
            {
                Go(PatrolState.ClosingGate, gate.Approach, 4.25f);
                if (!passageBlocked && Within(gate.Approach, .85f))
                {
                    gate.SetOpen(false);
                    Stop("Protest");
                }
            }
            else
            {
                if (Within(yard.FromYard(Patrol[waypoint]), .72f)) waypoint = (waypoint + 1) % Patrol.Length;
                Go(PatrolState.Patrolling, yard.FromYard(Patrol[waypoint]), 3.15f);
            }
            return null;
        }

        void CornerDog(EscapeDog dog)
        {
            quarry = null;
            dogInReach = dog;
            dogReachTime += Time.deltaTime;
            lastSeen = dog.transform.position;
            tracksUntil = Time.time + PursuitMemory;
            if (dogReachTime < DogWarningTime)
            {
                if (Within(lastSeen, 1.05f))
                {
                    ChangeState(PatrolState.CorneringDog);
                    Face(lastSeen);
                    Stop("Watch");
                }
                else Go(PatrolState.CorneringDog, lastSeen, 4.5f, .84f);
                return;
            }

            Face(lastSeen);
            Stop("Watch");
            dog.Grab(transform.position);
            heldDog = dog;
            ChangeState(PatrolState.HoldingDog);
            proceedsAt = Time.time + DogRescueTime;
        }

        EscapeHen VisibleHen(EscapeHen[] flock)
        {
            EscapeHen nearest = null;
            float best = float.PositiveInfinity;
            foreach (var hen in flock)
            {
                if (hen.Saved || hen.Caught || !hen.Following || yard.InPen(hen.transform.position)) continue;
                float distance = (hen.transform.position - transform.position).sqrMagnitude;
                if (distance >= best || !CanSee(hen.transform.position)) continue;
                nearest = hen;
                best = distance;
            }
            return nearest;
        }

        EscapeHen Lunge()
        {
            Go(PatrolState.Lunging, strikePoint, 7.25f, .10f);
            if (quarry && !quarry.Caught && !quarry.Saved && !yard.InPen(quarry.transform.position) &&
                Within(quarry.transform.position, HenGrabDistance) && ClearTo(quarry.transform.position))
            {
                henPositionOnGrab = quarry.transform.position;
                inHands = quarry;
                inHands.Lift();
                ChangeState(PatrolState.ScoopingHen);
                proceedsAt = Time.time + ScoopTime;
                tracksUntil = 0f;
                return inHands;
            }
            if (Time.time >= proceedsAt) Recover(.78f, "Watch");
            return null;
        }

        void ReturnHen()
        {
            if (Current == PatrolState.ScoopingHen)
            {
                if (Progress(ScoopTime) < .29f) Go(PatrolState.ScoopingHen, henPositionOnGrab, 3.2f, .68f);
                else
                {
                    Stop("Watch");
                    Face(henPositionOnGrab);
                }
                if (Time.time < proceedsAt) return;
            }

            if (Current != PatrolState.PuttingHenDown)
            {
                Vector3 approach = returnApproach ? returnApproach.position : firstPosition;
                Go(PatrolState.CarryingHen, approach, 4.75f, .28f);
                if (!Within(approach, .54f)) return;

                Vector3 pen = holdingPen ? holdingPen.position : firstPosition + yard.YardVector(Vector3.forward);
                int slot = pennedHens++;
                dropPoint = pen + yard.YardVector(new Vector3((slot % 3 - 1) * .41f, 0f, slot / 3 * .32f));
                Stop("Watch");
                ChangeState(PatrolState.PuttingHenDown);
                proceedsAt = Time.time + SetDownTime;
            }

            Face(dropPoint);
            if (Time.time < proceedsAt) return;
            inHands.ReleaseInPen(dropPoint);
            inHands = null;
            Recover(.42f, "Watch");
        }

        void LateUpdate()
        {
            if (Time.deltaTime <= 0f) return;
            rig ??= new EscapeKeeperRig(animator, transform);
            switch (Current)
            {
                case PatrolState.Windup:
                {
                    float ready = Ease(Progress(WindupTime));
                    rig.Cradle(strikePoint + Vector3.up * .46f, ready * .48f, ready * .85f);
                    break;
                }
                case PatrolState.Lunging:
                    rig.Cradle(strikePoint + Vector3.up * .38f, .55f, .92f);
                    break;
                case PatrolState.ScoopingHen:
                    PoseScoop();
                    break;
                case PatrolState.CarryingHen:
                {
                    Vector3 grip = rig.Cradle(ChestGrip, 0f, 1f);
                    inHands.CarryAt(grip - Vector3.up * .20f, transform.forward, .28f);
                    break;
                }
                case PatrolState.PuttingHenDown:
                {
                    float down = Ease(Progress(SetDownTime));
                    Vector3 grip = rig.Cradle(Vector3.Lerp(ChestGrip, dropPoint + Vector3.up * .24f, down), down, 1f);
                    inHands.CarryAt(grip - Vector3.up * .19f, transform.forward, .32f * (1f - down));
                    break;
                }
                case PatrolState.CorneringDog when dogInReach:
                    rig.HoldCollar(dogInReach.Collar, Ease(DogPressure) * .82f);
                    break;
                case PatrolState.HoldingDog when heldDog:
                    rig.HoldCollar(heldDog.Collar, Mathf.Lerp(.82f, 1f, Ease(Progress(DogRescueTime) * 14f)));
                    break;
            }
        }

        void PoseScoop()
        {
            float progress = Progress(ScoopTime);
            float lift = Ease(Mathf.InverseLerp(.24f, 1f, progress));
            float crouch = Mathf.Lerp(.6f, 1f, Ease(progress * 4.4f)) * (1f - lift);
            Vector3 contact = henPositionOnGrab + Vector3.up * .27f;
            Vector3 grip = rig.Cradle(Vector3.Lerp(contact, ChestGrip, lift), crouch, 1f);
            Vector3 raised = grip - Vector3.up * .20f;
            raised.y = Mathf.Max(henPositionOnGrab.y, raised.y);
            Vector3 henPosition = Vector3.Lerp(henPositionOnGrab, raised, Ease(Mathf.InverseLerp(.17f, .47f, progress)));
            inHands.CarryAt(henPosition, transform.forward, Mathf.Lerp(1f, .28f, lift));
        }

        Vector3 ChestGrip => transform.position + transform.forward * .44f + Vector3.up * 1.12f;

        float Progress(float duration) => Mathf.Clamp01(1f - (proceedsAt - Time.time) / duration);

        static float Ease(float value) => Mathf.SmoothStep(0f, 1f, value);

        void Recover(float delay, string nextPose)
        {
            ChangeState(PatrolState.Recovering);
            proceedsAt = Time.time + delay;
            tracksUntil = 0f;
            Stop(nextPose);
        }

        public bool CanSee(Vector3 position)
        {
            Vector3 direction = Flat(position - transform.position);
            float distance = direction.sqrMagnitude;
            if (distance > SightRange * SightRange) return false;
            if (distance > 1.16f * 1.16f && Vector3.Dot(transform.forward, direction.normalized) < ViewDot) return false;
            return ClearTo(position);
        }

        bool ClearTo(Vector3 position)
        {
            Vector3 offset = Flat(position - transform.position);
            float distance = offset.magnitude;
            return distance < .01f || SightDistance(offset / distance, distance) >= distance;
        }

        internal float SightDistance(Vector3 direction, float distance)
        {
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            if (Physics.Raycast(eye, direction, out var hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = hit.distance;

            // Sweep down to hen height so low cover and raised cart beds both cast a shadow.
            Vector3 lower = transform.position + Vector3.up * .22f;
            if (Physics.CapsuleCast(lower, eye, .055f, direction, out hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = hit.distance;
            return distance;
        }

        bool Within(Vector3 point, float range) => Flat(point - transform.position).sqrMagnitude < range * range;

        static Vector3 Flat(Vector3 vector) => Vector3.ProjectOnPlane(vector, Vector3.up);

        Vector3 Lead(Vector3 position, Vector3 velocity, float seconds)
        {
            Vector3 offset = Vector3.ClampMagnitude(Flat(velocity) * seconds, 2.65f);
            var filter = Navigation;
            if (offset.sqrMagnitude < .04f || !NavMesh.SamplePosition(position, out var start, .65f, filter))
                return position;
            Vector3 headStart = start.position + offset;
            return NavMesh.Raycast(start.position, headStart, out var edge, filter)
                ? edge.position : headStart;
        }

        void Face(Vector3 target)
        {
            Vector3 direction = Flat(target - transform.position);
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                    960f * Time.deltaTime);
        }

        void ChangeState(PatrolState state)
        {
            if (Current == state) return;
            Current = state;
            nextPathAt = 0f;
        }

        void Go(PatrolState state, Vector3 target, float speed, float stopDistance = .33f)
        {
            ChangeState(state);
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = speed;
            agent.stoppingDistance = stopDistance;
            if (Time.time >= nextPathAt)
            {
                if (NavMesh.SamplePosition(target, out var ground, 1.25f, Navigation))
                    agent.SetDestination(ground.position);
                nextPathAt = Time.time + .16f;
            }
            if (agent.velocity.sqrMagnitude < .12f && state is PatrolState.Distracted or PatrolState.Investigating)
                Face(target);
            Animate(agent.velocity.sqrMagnitude > .12f ? "Walk" : "Watch");
        }

        void Stop(string nextPose)
        {
            agent.isStopped = true;
            agent.updateRotation = false;
            Animate(nextPose);
        }

        void Animate(string nextPose)
        {
            int stateHash = Animator.StringToHash("Base Layer." + nextPose);
            if (stateHash != pose)
            {
                animator.CrossFadeInFixedTime(stateHash, .11f);
                pose = stateHash;
            }
            animator.speed = nextPose == "Walk" ? Mathf.Clamp(agent.velocity.magnitude / 2.6f, .4f, 1.85f) : 1f;
        }
    }
}
