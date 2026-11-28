using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Reusable killer behaviour used by the frog, spider, swatter, net and zapper lamp.
    /// Idle -> Warning (butterfly enters warnRadius) -> Telegraph (wind-up) -> Strike (kills if still in reach).
    /// Leaving the strike reach during the telegraph, or hiding, makes it miss and cool down.
    /// Timings come from DeathCause.warningTime / telegraphTime (see docs/DeathDesign.md).
    /// Art plugs in through the Animator triggers and the UnityEvents; no code change needed per killer.
    /// </summary>
    public class ProximityKiller : DeathCause
    {
        public enum State { Idle, Warning, Telegraph, Cooldown }

        [Header("Detection")]
        public float warnRadius = 6f;
        public float strikeRadius = 2.5f;
        [Tooltip("Only hunts when the butterfly is below this height above this object (frog: low over water). 0 = no limit.")]
        public float maxHeightAbove = 0f;
        [Tooltip("If set, only active at night (zapper lamp). Reads a function: return true when it is night.")]
        public bool nightOnly = false;
        public float cooldown = 3f;
        [Tooltip("Seconds between the attack animation starting and the kill landing.")]
        public float killDelay = 0.2f;

        [Header("Animation hooks (optional)")]
        public Animator animator;
        public string warnTrigger = "Warn";
        public string telegraphTrigger = "Telegraph";
        public string strikeTrigger = "Strike";
        public string resetTrigger = "Reset";

        [Header("Butterfly")]
        [Tooltip("Found by name (starts with 'Butterfly') if empty.")]
        public Transform butterfly;
        [Tooltip("Set true by hiding-spot code while the butterfly is hidden.")]
        public static bool ButterflyHidden;
        /// <summary>Assign from the day-night system: () => isNight. Only used when nightOnly is true.</summary>
        public static System.Func<bool> IsNight = () => true;

        [Header("Warning bar")]
        [Tooltip("Small loading bar above the head that shows the attack charging.")]
        public bool showWarningBar = true;
        [Tooltip("Bar position in the killer's local space (above its head).")]
        public Vector3 warningBarOffset = new Vector3(0f, 1.1f, 0f);
        public float warningBarWidth = 1.3f;

        public State Current { get; private set; } = State.Idle;
        float timer;

        /// <summary>0..1 while the attack is charging (warning + telegraph), 0 otherwise.</summary>
        public float ChargeProgress
        {
            get
            {
                float total = warningTime + telegraphTime;
                if (total <= 0.0001f) return 0f;
                if (Current == State.Warning) return Mathf.Clamp01((warningTime - timer) / total);
                if (Current == State.Telegraph) return Mathf.Clamp01((warningTime + telegraphTime - timer) / total);
                return 0f;
            }
        }

        /// <summary>Seconds the player still has to get away.</summary>
        public float ChargeSecondsLeft => (warningTime + telegraphTime) * (1f - ChargeProgress);

        void Start()
        {
            if (showWarningBar) KillerWarningBar.Create(this, warningBarOffset, warningBarWidth);
        }
        Amusnet.World.ShelterState shelter;

        void Reset() { displayName = "Killer"; }

        void Update()
        {
            if (DeathManager.Instance != null && DeathManager.Instance.IsDying) return;
            if (!ResolveButterfly()) return;

            if (nightOnly && !IsNight()) { SetState(State.Idle); return; }

            float dist = Vector3.Distance(butterfly.position, transform.position);
            bool hidden = ButterflyHidden || (shelter != null && shelter.IsHidden);
            bool inWarn = dist <= warnRadius && !hidden && HeightOk();
            bool inStrike = dist <= strikeRadius && !hidden && HeightOk();

            switch (Current)
            {
                case State.Idle:
                    if (inWarn) Enter(State.Warning, warnTrigger);
                    break;

                case State.Warning:
                    if (!inWarn) { Enter(State.Idle, resetTrigger); break; }
                    timer -= Time.deltaTime;
                    if (timer <= 0f) Enter(State.Telegraph, telegraphTrigger);
                    break;

                case State.Telegraph:
                    timer -= Time.deltaTime;
                    if (timer > 0f) break;
                    Attack(inStrike); // bar is full: always attack; it only kills if the butterfly is still in reach
                    break;

                case State.Cooldown:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) Enter(State.Idle, null);
                    break;
            }
        }

        void Attack(bool hit)
        {
            if (animator && !string.IsNullOrEmpty(strikeTrigger)) animator.SetTrigger(strikeTrigger);
            Current = State.Cooldown;
            timer = cooldown;
            if (hit) StartCoroutine(KillAfterDelay());
        }

        System.Collections.IEnumerator KillAfterDelay()
        {
            // let the tongue / swing animation reach the butterfly first
            yield return new WaitForSeconds(killDelay);
            if (butterfly == null) yield break;
            if (DeathManager.Instance != null) DeathManager.Instance.Die(this, butterfly.gameObject);
            else Debug.LogWarning("[Killer] No DeathManager in the scene.");
        }

        void Enter(State s, string trigger)
        {
            Current = s;
            timer = s switch
            {
                State.Warning => warningTime,
                State.Telegraph => telegraphTime,
                State.Cooldown => cooldown,
                _ => 0f
            };
            if (animator && !string.IsNullOrEmpty(trigger)) animator.SetTrigger(trigger);
        }

        void SetState(State s) { if (Current != s) Enter(s, null); }

        bool HeightOk() => maxHeightAbove <= 0f || butterfly.position.y - transform.position.y <= maxHeightAbove;

        bool ResolveButterfly()
        {
            if (butterfly != null) return true;
            butterfly = ButterflyLocator.Find();
            if (butterfly == null) return false;
            shelter = butterfly.GetComponentInChildren<Amusnet.World.ShelterState>();
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, warnRadius);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, strikeRadius);
        }
    }
}
