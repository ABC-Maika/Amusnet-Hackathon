using Amusnet.Mess;
using Amusnet.World;
using Butterfly.Death;
using Butterfly.NPC;
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
        private WingGust gust;
        private Camera view;
        private Vector3 target;
        private Vector3 modelPosition;
        private Quaternion modelRotation;
        [SerializeField] private bool expressiveRig;
        private float startledUntil, takeNoticeAfter;
        private int motion;
        private float remaining;
        private float repathIn;
        private bool nextWork = true;

        [Header("Anger tools")]
        [SerializeField, Tooltip("Seconds between 'Got you!' and the swing.")] private float swingWindUp = .45f;
        [SerializeField, Tooltip("How far the swatter or net reaches when it lands.")] private float swingReach = 2.2f;
        [SerializeField] private float trapCooldown = 15f;
        [SerializeField] private float trapLifetime = 20f;
        [SerializeField] private int maxTraps = 2;
        private SwatterDeath swatter;
        private NetDeath net;
        private float swingAt, nextTrapAt;
        private int liveTraps;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            shelter = mess.GetComponent<ShelterState>();
            gust = mess.GetComponent<WingGust>();
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
                || (source.transform.position - transform.position).sqrMagnitude > HearingRadius * HearingRadius) return;
            if (expressiveRig && Time.time >= takeNoticeAfter)
            {
                startledUntil = Time.time + .86f;
                takeNoticeAfter = Time.time + 4f;
            }
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
                    if (swingAt > 0f)
                    {
                        TurnTowards(mess.transform.position);
                        if (Time.time >= swingAt) Swing();
                        break;
                    }
                    if (CanSeeButterfly())
                    {
                        Vector3 offset = mess.transform.position - transform.position;
                        TryPlaceTrap();
                        if (offset.y < 1.95f && Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude < 1.3f)
                        {
                            if (HeldTool() != null)
                            {
                                // Short wind-up so the player can still dodge.
                                swingAt = Time.time + swingWindUp;
                                agent.isStopped = true;
                                bubble.text = "Got you!";
                                break;
                            }
                            if (gust)
                            {
                                gust.Shoo();
                                SetActivity(Activity.Complaining, 2.4f);
                                break;
                            }
                        }
                    }
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
            swingAt = 0f;
            bool travelling = activity is Activity.Walking or Activity.Investigating or Activity.Chasing;
            if (agent.isOnNavMesh)
            {
                if (!travelling) agent.ResetPath();
                agent.isStopped = !travelling;
                float baseSpeed = activity == Activity.Chasing ? 3.8f : activity == Activity.Investigating ? 2.6f : 1.65f;
                // Angry villagers only hurry when they are after the butterfly.
                agent.speed = travelling && activity != Activity.Walking ? baseSpeed * AngerSpeed : baseSpeed;
            }
            var tier = Anger ? Anger.Tier : AngerTier.Calm;
            bubble.text = activity switch
            {
                Activity.Investigating => tier >= AngerTier.Angry ? "Where are you?!" : "What was that?",
                Activity.Chasing => tier switch
                {
                    AngerTier.Furious => "I'll trap you!",
                    AngerTier.Angry => "Get the net!",
                    AngerTier.Annoyed => "Swat it!",
                    _ => "Hey!"
                },
                Activity.Complaining => tier >= AngerTier.Angry ? "That butterfly again!" : "What a mess!",
                _ => ""
            };
            UpdateToolVisibility();
        }

        // ---------- Anger tools (swatter / net / trap) ----------

        private static AngerLevels Anger => AngerLevels.Instance;
        private static float AngerSpeed => Anger ? Mathf.Max(.1f, Anger.SpeedMultiplier) : 1f;
        private static bool CanUse(AngerTools tool) => Anger && Anger.Can(tool);
        private float HearingRadius => Anger ? Mathf.Max(hearingRadius, Anger.NoticeRadius) : hearingRadius;

        private ProximityKiller HeldTool()
        {
            if (CanUse(AngerTools.Net)) return net ? net : net = MakeTool<NetDeath>("Villager Net");
            if (CanUse(AngerTools.Swatter)) return swatter ? swatter : swatter = MakeTool<SwatterDeath>("Villager Swatter");
            return null;
        }

        private T MakeTool<T>(string toolName) where T : ProximityKiller
        {
            var go = new GameObject(toolName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(.35f, 0f, .1f);
            var tool = go.AddComponent<T>(); // Awake applies the killer's defaults
            tool.autoHunt = false;           // the villager decides when to swing
            return tool;
        }

        // Only the tool the villager may use right now is shown. Never toggle mid-death: it would stop the kill animation.
        private void UpdateToolVisibility()
        {
            if (DeathManager.Instance && DeathManager.Instance.IsDying) return;
            bool useNet = CanUse(AngerTools.Net), useSwatter = !useNet && CanUse(AngerTools.Swatter);
            if (useNet && !net) HeldTool();
            if (useSwatter && !swatter) HeldTool();
            if (net) net.gameObject.SetActive(useNet);
            if (swatter) swatter.gameObject.SetActive(useSwatter);
        }

        private void Swing()
        {
            swingAt = 0f;
            var tool = HeldTool();
            Vector3 offset = mess.transform.position - transform.position;
            bool inReach = offset.y < 2.4f && Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude < swingReach * swingReach;
            if (tool && inReach && CanSeeButterfly() && DeathManager.Instance && !DeathManager.Instance.IsDying)
            {
                DeathManager.Instance.Die(tool, mess.gameObject);
                SetActivity(Activity.Complaining, 3f);
                bubble.text = "Got it!";
                return;
            }
            SetActivity(Activity.Complaining, 2.4f);
            bubble.text = "Missed!";
        }

        private void TryPlaceTrap()
        {
            if (!CanUse(AngerTools.Trap) || Time.time < nextTrapAt || liveTraps >= maxTraps) return;
            Vector3 spot = mess.transform.position;
            spot.y = transform.position.y;
            if (!NavMesh.SamplePosition(spot, out var hit, 3f, NavMesh.AllAreas)) return;
            nextTrapAt = Time.time + trapCooldown;

            var go = new GameObject("Villager Zapper Trap");
            go.transform.position = hit.position;
            var trap = go.AddComponent<ZapperLampDeath>();
            trap.nightOnly = false;      // villager traps work in daylight too
            trap.lifetime = trapLifetime;
            liveTraps++;
            go.AddComponent<TrapCounter>().owner = this;
            bubble.text = "Trap set!";
        }

        private sealed class TrapCounter : MonoBehaviour
        {
            public VillagerAI owner;
            private void OnDestroy() { if (owner) owner.liveTraps--; }
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
            if (expressiveRig)
            {
                bool startled = Time.time < startledUntil;
                string pose = startled ? "Startle" : CurrentActivity switch
                {
                    Activity.Working => "Sort goods",
                    Activity.Complaining => "Protest",
                    _ => speed > .15f ? "Walk" : "Watch"
                };
                int state = Animator.StringToHash("Base Layer." + pose);
                if (state != motion)
                {
                    animator.CrossFadeInFixedTime(state, startled ? .07f : .16f);
                    motion = state;
                }
                animator.speed = pose == "Walk" ? Mathf.Clamp(speed / 1.65f, .5f, 2f) : 1f;
                if (view) bubble.transform.rotation = view.transform.rotation;
                return;
            }
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
