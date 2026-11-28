using Amusnet.Mess;
using Amusnet.World;
using UnityEngine;
using UnityEngine.AI;

namespace Amusnet.NPC
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VillagerAI : MonoBehaviour
    {
        public enum Activity { Walking, Working, Idle, Investigating, Chasing, Complaining }

        [SerializeField] private MessManager mess;
        [SerializeField] private Animator animator;
        [SerializeField] private TextMesh bubble;
        [SerializeField] private Vector3 workplace;
        [SerializeField] private Vector3 restSpot;
        [SerializeField] private float hearingRadius = 12f;
        [SerializeField] private float chaseRadius = 9f;

        public Activity CurrentActivity { get; private set; }
        private NavMeshAgent agent;
        private ShelterState shelter;
        private Camera view;
        private Vector3 target;
        private Vector3 modelPosition;
        private Quaternion modelRotation;
        private float remaining;
        private float repathIn;
        private bool nextWork = true;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            shelter = mess.GetComponent<ShelterState>();
            view = Camera.main;
            modelPosition = animator.transform.localPosition;
            modelRotation = animator.transform.localRotation;
        }

        private void OnEnable() => mess.MessCreated += Notice;
        private void OnDisable() => mess.MessCreated -= Notice;

        private void Start()
        {
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var closest, 2f, agent.areaMask))
                agent.Warp(closest.position);
            SetActivity(Activity.Idle, 1.1f);
        }

        private void Notice(Interactable source, int amount)
        {
            if (!agent.isOnNavMesh || !source
                || (source.transform.position - transform.position).sqrMagnitude > hearingRadius * hearingRadius) return;
            SetActivity(Activity.Investigating, 6f);
            if (!WalkTo(source.transform.position))
                SetActivity(Activity.Complaining, 2.4f);
        }

        private void Update()
        {
            if (!agent.isOnNavMesh) return;
            remaining -= Time.deltaTime;
            switch (CurrentActivity)
            {
                case Activity.Idle:
                case Activity.Working:
                case Activity.Complaining:
                    if (CurrentActivity == Activity.Complaining && mess)
                        TurnTowards(mess.transform.position);
                    if (remaining <= 0f) BeginRoutine();
                    break;
                case Activity.Walking:
                    if (Arrived() || remaining <= 0f)
                    {
                        SetActivity(nextWork ? Activity.Working : Activity.Idle, nextWork ? 3.5f : 2.4f);
                        nextWork = !nextWork;
                    }
                    break;
                case Activity.Investigating:
                    if (Arrived() || remaining <= 0f)
                    {
                        if (CanSeeButterfly())
                        {
                            SetActivity(Activity.Chasing, 3.4f);
                            repathIn = 0f;
                        }
                        else SetActivity(Activity.Complaining, 2.4f);
                    }
                    break;
                case Activity.Chasing:
                    if (remaining <= 0f || !CanSeeButterfly())
                    {
                        SetActivity(Activity.Complaining, 2.4f);
                        break;
                    }
                    repathIn -= Time.deltaTime;
                    if (repathIn <= 0f)
                    {
                        WalkTo(mess.transform.position);
                        repathIn = .24f;
                    }
                    break;
            }
            Animate();
        }

        private void SetActivity(Activity activity, float seconds)
        {
            CurrentActivity = activity;
            remaining = seconds;
            bool travelling = activity is Activity.Walking or Activity.Investigating or Activity.Chasing;
            if (agent.isOnNavMesh)
            {
                if (!travelling) agent.ResetPath();
                agent.isStopped = !travelling;
                agent.speed = activity == Activity.Chasing ? 3.8f : activity == Activity.Investigating ? 2.6f : 1.65f;
            }
            bubble.text = activity switch
            {
                Activity.Investigating => "What was that?",
                Activity.Chasing => "Hey!",
                Activity.Complaining => "What a mess!",
                _ => ""
            };
        }

        private void BeginRoutine()
        {
            SetActivity(Activity.Walking, 12f);
            if (!WalkTo(nextWork ? workplace : restSpot))
                SetActivity(Activity.Idle, 1f);
        }

        private bool WalkTo(Vector3 position)
        {
            position.y = transform.position.y;
            if (!NavMesh.SamplePosition(position, out var hit, 2f, agent.areaMask)) return false;
            target = hit.position;
            return agent.SetDestination(target);
        }

        private bool Arrived() => !agent.pathPending && (transform.position - target).sqrMagnitude < .7f;

        private bool CanSeeButterfly()
        {
            if (!mess || (shelter && shelter.IsHidden)) return false;
            Vector3 offset = mess.transform.position - transform.position;
            if (offset.sqrMagnitude > chaseRadius * chaseRadius || offset.y > 4f) return false;
            if (!Physics.Linecast(transform.position + Vector3.up * 1.35f, mess.transform.position,
                    out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform.IsChildOf(mess.transform);
        }

        private void TurnTowards(Vector3 position)
        {
            Vector3 direction = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .1f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240f * Time.deltaTime);
        }

        private void Animate()
        {
            float speed = agent.velocity.magnitude;
            float stride = Mathf.Sin(Time.time * 5.5f);
            float playback = speed > .12f ? Mathf.Clamp(speed / 1.65f, .45f, 2.1f) : 0f;
            animator.speed = Mathf.MoveTowards(animator.speed, playback, 5f * Time.deltaTime);
            float tilt = CurrentActivity == Activity.Working ? 7f + stride * 6f
                : CurrentActivity == Activity.Complaining ? -6f + stride * 5f : 0f;
            animator.transform.localRotation = Quaternion.Slerp(animator.transform.localRotation,
                modelRotation * Quaternion.Euler(tilt, 0f, 0f), 7f * Time.deltaTime);
            animator.transform.localPosition = modelPosition + Vector3.up * (.015f * Mathf.Sin(Time.time * 2.4f));
            if (view) bubble.transform.rotation = view.transform.rotation;
        }
    }
}
