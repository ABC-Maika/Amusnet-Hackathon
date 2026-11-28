using UnityEngine;
using UnityEngine.InputSystem;

namespace Amusnet.World
{
    public sealed class LaundryGatePreview : MonoBehaviour
    {
        private enum Phase { Waiting, ToGate, ToLaundry, HoldingGate, Stumbling, Returning, Finished }

        [SerializeField] private GoatLaundryPreview laundry;
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private ParticleSystem rain;
        [SerializeField] private Transform gate;
        [SerializeField] private Transform villager;
        [SerializeField] private Animator walk;
        [SerializeField] private Transform basket;
        [SerializeField] private Transform gateApproach;
        [SerializeField] private Transform laundryApproach;
        [SerializeField] private Transform escapePoint;
        [SerializeField, Tooltip("Metres from the gate along the escape route. Negative values drop the sheet before the gate.")]
        private float sheetDropOffset = -1.7f;

        private Phase phase;
        private Quaternion closed;
        private Vector3 home;
        private bool wantsGateOpen;
        private float gateTravel;
        private float walkPlayback;
        private float forcedRainUntil;
        private Vector3 stumbleStart;
        private Vector3 stumbleEnd;
        private Quaternion stumbleFacing;
        private float stumbleProgress;

        public bool IsGateOpen => GateAngle >= 78f;
        public bool SheetCollected { get; private set; }
        public bool VillagerBumped { get; private set; }
        public Transform EscapePoint => escapePoint;
        private float GateAngle => Mathf.SmoothStep(0f, 95f, gateTravel);
        private bool IsVillagerAtGate => (villager.position - gateApproach.position).sqrMagnitude < 0.9f * 0.9f;

        public bool IsNearSheetDrop(Vector3 goatPosition)
        {
            Vector3 exitDirection = (escapePoint.position - gateApproach.position).normalized;
            return Vector3.Dot(goatPosition - gateApproach.position, exitDirection) >= sheetDropOffset;
        }

        private void CheckForGoatContact()
        {
            if (phase is not (Phase.ToGate or Phase.ToLaundry or Phase.HoldingGate) || !IsVillagerAtGate ||
                (villager.position - laundry.GoatPosition).sqrMagnitude > 0.8f * 0.8f)
                return;

            VillagerBumped = true;
            stumbleStart = villager.position;
            Vector3 lane = escapePoint.position - gateApproach.position;
            lane.y = 0f;
            Vector3 side = Vector3.Cross(lane.normalized, Vector3.up);
            stumbleEnd = stumbleStart + side * 1.35f;
            stumbleFacing = villager.rotation;
            stumbleProgress = 0f;
            phase = Phase.Stumbling;
        }

        private void Awake()
        {
            home = villager.position;
            closed = gate.localRotation;
            walk.speed = 0f;
        }

        private void Update()
        {
            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
                forcedRainUntil = Time.time + 12f;

            float rainLevel = Time.time < forcedRainUntil ? 1f : weather.Rain;
            var emission = rain.emission;
            emission.rateOverTime = 420f * rainLevel;
            gateTravel = Mathf.MoveTowards(gateTravel, wantsGateOpen ? 1f : 0f, Time.deltaTime * 1.45f);
            gate.localRotation = closed * Quaternion.Euler(0f, -GateAngle, 0f);

            walkPlayback = 0f;
            CheckForGoatContact();
            switch (phase)
            {
                case Phase.Waiting:
                    if (rainLevel > 0.35f)
                        phase = Phase.ToGate;
                    break;
                case Phase.ToGate:
                    if (WalkTo(gateApproach.position))
                    {
                        wantsGateOpen = true;
                        phase = Phase.ToLaundry;
                    }
                    break;
                case Phase.ToLaundry:
                    if (!IsGateOpen)
                        break;
                    if (laundry.IsInSheetChase && (laundry.HasGrabbedSheet || IsVillagerAtGate))
                    {
                        phase = Phase.HoldingGate;
                        break;
                    }
                    if (WalkTo(laundryApproach.position))
                    {
                        SheetCollected = laundry.TryCollectSheet(basket);
                        // Leave the gate open while the goat makes its escape.
                        phase = SheetCollected ? Phase.Returning : Phase.Finished;
                    }
                    break;
                case Phase.HoldingGate:
                    if (!laundry.IsInSheetChase)
                        phase = Phase.ToLaundry;
                    else
                        WalkTo(gateApproach.position, 2.6f);
                    break;
                case Phase.Stumbling:
                    Stumble();
                    break;
                case Phase.Returning:
                    if (WalkTo(home))
                    {
                        wantsGateOpen = false;
                        phase = Phase.Finished;
                    }
                    break;
            }
            walk.speed = Mathf.MoveTowards(walk.speed, walkPlayback, 9f * Time.deltaTime);
        }

        private void Stumble()
        {
            stumbleProgress = Mathf.Clamp01(stumbleProgress + Time.deltaTime / 0.65f);
            float slide = Mathf.SmoothStep(0f, 1f, stumbleProgress);
            walkPlayback = 1.55f * Mathf.Max(0f, Mathf.Sin(Mathf.PI * stumbleProgress));
            villager.position = Vector3.Lerp(stumbleStart, stumbleEnd, slide);
            villager.rotation = stumbleFacing * Quaternion.Euler(0f, 0f, -23f * Mathf.Sin(Mathf.PI * stumbleProgress));
            if (stumbleProgress < 1f)
                return;

            phase = Phase.Finished;
        }

        private bool WalkTo(Vector3 destination, float speed = 1.65f)
        {
            Vector3 direction = destination - villager.position;
            if (direction.sqrMagnitude <= 0.0025f)
                return true;

            walkPlayback = speed / 1.65f;
            villager.rotation = Quaternion.RotateTowards(villager.rotation,
                Quaternion.LookRotation(direction), 540f * Time.deltaTime);
            villager.position = Vector3.MoveTowards(villager.position, destination, speed * Time.deltaTime);
            return false;
        }

        private void OnGUI()
        {
            string hint = phase switch
            {
                Phase.Waiting => "Press R to start rain for this preview.",
                Phase.ToGate => "Rain! A villager is coming for the washing.",
                Phase.ToLaundry when IsGateOpen => "The gate is open. Can the goat reach the sheet first?",
                Phase.ToLaundry => "The villager is opening the gate.",
                Phase.HoldingGate when IsVillagerAtGate => "The villager is holding the gate open.",
                Phase.HoldingGate => "The villager is rushing back to the open gate.",
                Phase.Stumbling => "The goat bumped the villager aside!",
                Phase.Returning => "The villager is taking the sheet indoors.",
                Phase.Finished when VillagerBumped => "The goat bumped the villager. She’s back on her feet.",
                _ => null
            };
            if (hint != null)
                GUI.Box(new Rect(16, 62, Mathf.Min(460, Screen.width - 32), 28), hint);
        }
    }
}
