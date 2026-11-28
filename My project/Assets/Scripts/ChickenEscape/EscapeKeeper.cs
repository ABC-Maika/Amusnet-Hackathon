using UnityEngine;
using UnityEngine.AI;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EscapeKeeper : MonoBehaviour
    {
        public enum PatrolState { Patrolling, Investigating, Distracted, Pursuing, Windup, Lunging, ClosingGate, Recovering }

        public const float SightRange = 6.7f;
        public const float SightAngle = 47f;
        public const float EyeHeight = .80f;

        const float BarkHearing = 11f;
        const float GrabDistance = 1.12f;
        const float WindupTime = .52f;
        static readonly float ViewDot = Mathf.Cos(SightAngle * Mathf.Deg2Rad);
        static readonly Vector3[] Patrol = {
            new(6.05f, 0f, 1.35f), new(4.85f, 0f, -4.75f), new(1.6f, 0f, -5.44f),
            new(-5.65f, 0f, -3.8f), new(-6.0f, 0f, 1.14f), new(-.35f, 0f, 1.24f)
        };

        [SerializeField] Animator animator;

        NavMeshAgent agent;
        EscapeHen quarry;
        Vector3 barkPoint, decoyPoint, lastSeen, strikePoint;
        float proceedsAt, nextPathAt, barkUntil, decoyUntil, tracksUntil;
        int waypoint, pose;
        bool started, huntingDog;

        public PatrolState Current { get; private set; }
        public bool HasSpottedSomeone => Current is PatrolState.Pursuing or PatrolState.Windup or PatrolState.Lunging;
        public bool IsDistracted => Current == PatrolState.Distracted;
        public Vector3 Eye => transform.position + Vector3.up * EyeHeight;
        public Vector3 HoldingPoint { get; private set; }
        public Vector3 StrikePoint => strikePoint;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            HoldingPoint = transform.position + new Vector3(.25f, 0f, 1.05f);
        }

        public bool HearBark(Vector3 point)
        {
            if (!Within(point, BarkHearing)) return false;
            barkPoint = point;
            barkUntil = Time.time + 3.7f;
            nextPathAt = 0f;
            return true;
        }

        public bool TryDistract(Vector3 point)
        {
            if (!started || Current is PatrolState.Windup or PatrolState.Lunging ||
                !Within(point, 15f) || !NavMesh.SamplePosition(point, out var ground, 1.3f, agent.areaMask))
                return false;

            decoyPoint = ground.position;
            decoyUntil = Time.time + 5.3f;
            tracksUntil = 0f;
            return true;
        }

        public EscapeHen Step(EscapeDog dog, EscapeGate gate, EscapeHen[] flock, bool heist,
            bool hensInPen, bool passageBlocked)
        {
            if (!heist || !agent.isOnNavMesh) return null;
            if (!started)
            {
                started = true;
                agent.acceleration = 17f;
                agent.angularSpeed = 640f;
            }

            if (Current == PatrolState.Windup)
            {
                Face(strikePoint);
                if (Time.time >= proceedsAt)
                {
                    ChangeState(PatrolState.Lunging);
                    proceedsAt = Time.time + .63f;
                }
                else return null;
            }

            if (Current == PatrolState.Lunging) return Lunge(dog);
            if (Current == PatrolState.Recovering && Time.time < proceedsAt) return null;

            if (Time.time < decoyUntil)
            {
                Go(PatrolState.Distracted, decoyPoint, 4.95f);
                return null;
            }

            EscapeHen hen = VisibleHen(flock);
            bool seesDog = !dog.IsStopped && CanSee(dog.transform.position);
            if (hen || seesDog)
            {
                quarry = hen;
                huntingDog = !hen;
                lastSeen = hen ? hen.transform.position : dog.transform.position;
                tracksUntil = Time.time + 1.8f;
                if (Within(lastSeen, 2.25f))
                {
                    ChangeState(PatrolState.Windup);
                    strikePoint = lastSeen;
                    proceedsAt = Time.time + WindupTime;
                    Stop("Protest");
                }
                else Go(PatrolState.Pursuing, lastSeen, 5.18f);
            }
            else if (Time.time < tracksUntil) Go(PatrolState.Pursuing, lastSeen, 4.8f);
            else if (Time.time < barkUntil) Go(PatrolState.Investigating, barkPoint, 4.15f);
            else if (gate.IsOpen && hensInPen && Time.time > gate.OpenedAt + 5.4f)
            {
                Go(PatrolState.ClosingGate, gate.Approach, 3.5f);
                if (!passageBlocked && Within(gate.Approach, .85f))
                {
                    gate.SetOpen(false);
                    Stop("Protest");
                }
            }
            else
            {
                if (Within(Patrol[waypoint], .72f)) waypoint = (waypoint + 1) % Patrol.Length;
                Go(PatrolState.Patrolling, Patrol[waypoint], 2.55f);
            }
            return null;
        }

        EscapeHen VisibleHen(EscapeHen[] flock)
        {
            EscapeHen nearest = null;
            float best = float.PositiveInfinity;
            foreach (var hen in flock)
            {
                if (hen.Saved || hen.Caught || !hen.Following || MarketEscapeRun.InPen(hen.transform.position)) continue;
                float distance = (hen.transform.position - transform.position).sqrMagnitude;
                if (distance >= best || !CanSee(hen.transform.position)) continue;
                nearest = hen;
                best = distance;
            }
            return nearest;
        }

        EscapeHen Lunge(EscapeDog dog)
        {
            Go(PatrolState.Lunging, strikePoint, 7.15f, .06f);
            EscapeHen captured = null;
            bool hit = false;
            if (huntingDog)
            {
                if (!dog.IsStopped && Within(dog.transform.position, GrabDistance) && ClearTo(dog.transform.position))
                {
                    dog.Hesitate();
                    hit = true;
                }
            }
            else if (quarry && !quarry.Caught && !quarry.Saved && !MarketEscapeRun.InPen(quarry.transform.position) &&
                Within(quarry.transform.position, GrabDistance) && ClearTo(quarry.transform.position))
            {
                captured = quarry;
                hit = true;
            }

            if (hit || Time.time >= proceedsAt)
            {
                ChangeState(PatrolState.Recovering);
                proceedsAt = Time.time + (captured ? 1.85f : hit ? 1.24f : .92f);
                tracksUntil = 0f;
                Stop(hit ? "Protest" : "Watch");
            }
            return captured;
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
            position.y = Eye.y;
            return !Physics.Linecast(Eye, position, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        bool Within(Vector3 point, float range) => Flat(point - transform.position).sqrMagnitude < range * range;

        static Vector3 Flat(Vector3 vector) => Vector3.ProjectOnPlane(vector, Vector3.up);

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
                if (NavMesh.SamplePosition(target, out var ground, 1.25f, agent.areaMask))
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
