using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Butterfly.NPC
{
    public enum AngerTier
    {
        Calm = 0,      // villagers just complain
        Annoyed = 1,   // swatters
        Angry = 2,     // + nets, windows close
        Furious = 3    // + traps, gates close
    }

    /// <summary>Anything that changes behaviour with anger: villagers, windows, gates, trap spawners.</summary>
    public interface IAngerReactive
    {
        void OnAngerTierChanged(AngerTier tier, AngerTier previous);
    }

    /// <summary>What villagers are allowed to use at each tier.</summary>
    [Flags]
    public enum AngerTools
    {
        None = 0,
        Swatter = 1,
        Net = 2,
        Trap = 4,
        CloseWindows = 8,
        CloseGates = 16
    }

    /// <summary>
    /// Villager anger escalation. Feed it the mess score (normalised 0..1) via SetMess().
    /// At each threshold the tier rises and every IAngerReactive is told. Villagers query
    /// Instance.Tools / Instance.SpeedMultiplier; windows and gates just implement IAngerReactive
    /// (see CloseOnAnger).
    /// Hook-up: MessManager calls AngerLevels.Instance.SetMess(score / goal) whenever score changes.
    /// </summary>
    public class AngerLevels : MonoBehaviour
    {
        public static AngerLevels Instance { get; private set; }

        [Serializable]
        public class TierSettings
        {
            public AngerTier tier;
            [Range(0f, 1f), Tooltip("Normalised mess score (0..1) at which this tier starts.")]
            public float threshold;
            public AngerTools tools;
            [Tooltip("Villager move/chase speed multiplier at this tier.")]
            public float speedMultiplier = 1f;
            [Tooltip("How far villagers notice the butterfly at this tier.")]
            public float noticeRadius = 8f;
            [Tooltip("Multiplies the points of each new mess while the village is this angry.")]
            public float scoreMultiplier = 1f;
        }

        [Header("Tiers (ordered by threshold)")]
        public List<TierSettings> tiers = new List<TierSettings>
        {
            new TierSettings { tier = AngerTier.Calm,    threshold = 0f,    tools = AngerTools.None,                                                        speedMultiplier = 1f,   noticeRadius = 8f  },
            new TierSettings { tier = AngerTier.Annoyed, threshold = 0.25f, tools = AngerTools.Swatter,                                                     speedMultiplier = 1.1f, noticeRadius = 10f, scoreMultiplier = 1.25f },
            new TierSettings { tier = AngerTier.Angry,   threshold = 0.55f, tools = AngerTools.Swatter | AngerTools.Net | AngerTools.CloseWindows,         speedMultiplier = 1.25f, noticeRadius = 13f, scoreMultiplier = 1.5f },
            new TierSettings { tier = AngerTier.Furious, threshold = 0.8f,  tools = AngerTools.Swatter | AngerTools.Net | AngerTools.Trap | AngerTools.CloseWindows | AngerTools.CloseGates, speedMultiplier = 1.4f, noticeRadius = 16f, scoreMultiplier = 2f },
        };

        [Tooltip("If true the tier can drop again when the mess score drops. Default: anger never cools down.")]
        public bool canCoolDown = false;

        [Header("Events")]
        public UnityEvent<AngerTier> onTierChanged;
        public static event Action<AngerTier, AngerTier> TierChanged; // (new, previous)

        public float Mess { get; private set; }
        public AngerTier Tier { get; private set; } = AngerTier.Calm;
        TierSettings current;
        /// <summary>Settings of the current tier (cached: villagers read it several times a frame).</summary>
        public TierSettings Current => current ??= FindTier(Tier);

        TierSettings FindTier(AngerTier tier)
        {
            foreach (var t in tiers) if (t.tier == tier) return t;
            return tiers[0];
        }

        // Enter Play Mode without a domain reload keeps statics.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; TierChanged = null; }
        public AngerTools Tools => Current.tools;
        public float SpeedMultiplier => Current.speedMultiplier;
        public float NoticeRadius => Current.noticeRadius;
        public float ScoreMultiplier => Current.scoreMultiplier;

        readonly List<IAngerReactive> reactives = new List<IAngerReactive>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            tiers.Sort((a, b) => a.threshold.CompareTo(b.threshold));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool Can(AngerTools tool) => (Tools & tool) != 0;

        public void Register(IAngerReactive r)
        {
            if (r == null || reactives.Contains(r)) return;
            reactives.Add(r);
            r.OnAngerTierChanged(Tier, Tier); // let late joiners sync to the current state
        }

        public void Unregister(IAngerReactive r) => reactives.Remove(r);

        float rawMess, agitation;
        bool frozen;

        /// <summary>While frozen the tier never changes (e.g. during the villager lull); unfreezing applies what built up.</summary>
        public bool Frozen { get => frozen; set { frozen = value; if (!value) Apply(); } }

        /// <summary>Call with mess score normalised to 0..1 (score / goal).</summary>
        public void SetMess(float normalised)
        {
            rawMess = Mathf.Clamp01(normalised);
            Apply();
        }

        /// <summary>Raises anger on top of the mess score (0..1 of the bar), e.g. when the butterflies multiply.</summary>
        public void AddAgitation(float amount)
        {
            agitation = Mathf.Max(0f, agitation + amount);
            Apply();
        }

        void Apply()
        {
            Mess = Mathf.Clamp01(rawMess + agitation);
            if (frozen) return;

            AngerTier target = Tier;
            foreach (var t in tiers) if (Mess >= t.threshold) target = t.tier;

            if (!canCoolDown && target < Tier) return;
            if (target == Tier) return;

            var previous = Tier;
            Tier = target;
            current = null;
            Debug.Log($"[Anger] {previous} -> {Tier} (mess {Mess:0.00})");

            foreach (var r in reactives.ToArray()) r?.OnAngerTierChanged(Tier, previous);
            TierChanged?.Invoke(Tier, previous);
            onTierChanged?.Invoke(Tier);
        }

        /// <summary>Reset for a new run.</summary>
        public void ResetAnger()
        {
            Mess = rawMess = agitation = 0f;
            var previous = Tier;
            Tier = AngerTier.Calm;
            current = null;
            if (previous == Tier) return;
            foreach (var r in reactives.ToArray()) r?.OnAngerTierChanged(Tier, previous);
            TierChanged?.Invoke(Tier, previous);
            onTierChanged?.Invoke(Tier);
        }
    }

    /// <summary>
    /// Put on a window or gate. Closes (enables the blocker, hides the open visual) once the
    /// matching tool flag is active. Works with any art: just assign the two objects.
    /// </summary>
    public class CloseOnAnger : MonoBehaviour, IAngerReactive
    {
        public enum Kind { Window, Gate }

        public Kind kind = Kind.Window;
        [Tooltip("Shown while open (e.g. open shutters). Optional.")]
        public GameObject openVisual;
        [Tooltip("Shown/enabled while closed (closed shutters, collider that blocks the butterfly).")]
        public GameObject closedVisual;

        void OnEnable()
        {
            Apply(false);
            if (AngerLevels.Instance != null) AngerLevels.Instance.Register(this);
        }

        void OnDisable() { if (AngerLevels.Instance != null) AngerLevels.Instance.Unregister(this); }

        public void OnAngerTierChanged(AngerTier tier, AngerTier previous)
        {
            var flag = kind == Kind.Window ? AngerTools.CloseWindows : AngerTools.CloseGates;
            Apply(AngerLevels.Instance != null && AngerLevels.Instance.Can(flag));
        }

        void Apply(bool closed)
        {
            if (openVisual) openVisual.SetActive(!closed);
            if (closedVisual) closedVisual.SetActive(closed);
        }
    }
}
