using System;
using Amusnet.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Butterfly.Death
{
    /// <summary>
    /// Predator percentage. Rises while the butterfly flies, drops while it is hidden (flower, barn, hedge).
    /// Stage 1 (landOnFlowerAt): "land on a flower or the predator will kill you".
    /// Stage 2 (hideAt): "hide! the predator arrives in N seconds" - a countdown starts. Hiding cancels it.
    /// Countdown reaching 0, or the meter hitting 100, sends the bird: DeathManager.Die(BirdDeath).
    /// When this is in the scene it owns the threat: Yavkata's PredatorSystem stops ticking and only
    /// mirrors Percent to GameHUD. Without it, PredatorSystem runs alone and still kills via DeathManager.
    /// </summary>
    public class PredatorMeter : MonoBehaviour
    {
        public enum Stage { Safe, LandOnFlower, Hide }

        public static PredatorMeter Instance { get; private set; }

        [Header("Rates (percent per second)")]
        public float riseWhileFlying = 1.5f;
        public float fallWhileHidden = 30f;
        public float fallWhileResting = 6f;
        [Tooltip("Speed above which the butterfly counts as flying.")]
        public float flyingSpeed = 0.6f;

        [Header("Stages")]
        [Range(0, 100)] public float landOnFlowerAt = 55f;
        [Range(0, 100)] public float hideAt = 80f;
        [Tooltip("Seconds between the hide warning and the bird arriving.")]
        public float arrivalSeconds = 10f;

        [Header("Test helpers")]
        public bool holdHToHide = true;

        public float Percent { get; private set; }
        public Stage CurrentStage { get; private set; } = Stage.Safe;
        public float CountdownLeft { get; private set; }
        public bool IsHidden { get; private set; }
        public bool IsFlying { get; private set; }

        public static event Action<Stage> StageChanged;

        Transform butterfly;
        ShelterState shelter;
        BirdDeath bird;
        Vector3 lastPos;
        bool hasLast;
        bool sent;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            bird = GetComponent<BirdDeath>();
            if (bird == null) bird = gameObject.AddComponent<BirdDeath>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (sent) return;
            if (DeathManager.Instance != null && DeathManager.Instance.IsDying) return;
            // Freeze on the menu, while paused and after win/lose.
            if (GameFlow.Instance != null && !GameFlow.Instance.IsRunning) { hasLast = false; return; }
            if (!Resolve()) return;

            float dt = Time.deltaTime;
            float speed = hasLast ? (butterfly.position - lastPos).magnitude / Mathf.Max(dt, 0.0001f) : 0f;
            lastPos = butterfly.position;
            hasLast = true;

            IsHidden = (shelter != null && shelter.IsHidden)
                       || ProximityKiller.ButterflyHidden
                       || HidingZone.IsHidden(butterfly.position)
                       || (holdHToHide && Keyboard.current != null && Keyboard.current.hKey.isPressed);
            IsFlying = speed > flyingSpeed;

            if (IsHidden) Percent -= fallWhileHidden * dt;
            else if (IsFlying) Percent += riseWhileFlying * dt;
            else Percent -= fallWhileResting * dt;
            Percent = Mathf.Clamp(Percent, 0f, 100f);

            UpdateStage();

            if (CurrentStage == Stage.Hide)
            {
                CountdownLeft -= dt;
                if (CountdownLeft <= 0f) SendBird();
            }
            if (Percent >= 100f) SendBird();
        }

        void UpdateStage()
        {
            Stage next = Percent >= hideAt ? Stage.Hide
                       : Percent >= landOnFlowerAt ? Stage.LandOnFlower
                       : Stage.Safe;
            if (next == CurrentStage) return;

            if (next == Stage.Hide) CountdownLeft = arrivalSeconds;
            CurrentStage = next;
            StageChanged?.Invoke(next);
        }

        void SendBird()
        {
            if (sent) return;
            sent = true;
            Percent = 100f;
            if (DeathManager.Instance == null || butterfly == null)
            {
                Debug.LogWarning("[Predator] Bird arrived but there is no DeathManager or butterfly.");
                return;
            }

            var victim = butterfly.gameObject;
            if (PredatorBird.Instance != null)
                PredatorBird.Instance.Strike(victim, () => DeathManager.Instance.Die(bird, victim));
            else
                DeathManager.Instance.Die(bird, victim);
        }

        /// <summary>For a new run.</summary>
        public void ResetMeter()
        {
            Percent = 0f; CountdownLeft = 0f; sent = false; CurrentStage = Stage.Safe; hasLast = false;
        }

        bool Resolve()
        {
            if (butterfly != null) return true;
            butterfly = ButterflyLocator.Find();
            if (butterfly == null) return false;
            shelter = butterfly.GetComponentInChildren<ShelterState>();
            return true;
        }
    }
}
