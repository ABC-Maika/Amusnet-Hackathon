using System;
using System.Collections.Generic;
using Butterfly.Death;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Butterfly.Animals
{
    /// <summary>
    /// An animal that can join the rebellion. Fly close and press E (hold to fill the ring) to recruit it.
    /// Allies then walk to nearby messable objects (IChainReactionTarget) and cause extra messes on their own.
    /// MessManager should listen to RebelAnimal.AllyMess to add the value to the score.
    /// </summary>
    public class RebelAnimal : MonoBehaviour
    {
        public static event Action<RebelAnimal, float> AllyMess;   // (animal, mess value)
        public static event Action<RebelAnimal> Recruited;

        public static readonly List<RebelAnimal> Allies = new List<RebelAnimal>();

        [Header("Recruiting")]
        public string animalName = "Animal";
        public float recruitRadius = 4f;
        [Tooltip("Seconds the player has to hold E near the animal.")]
        public float recruitTime = 1.2f;

        [Header("Ally behaviour")]
        public float messRadius = 18f;
        public Vector2 messInterval = new Vector2(6f, 10f);
        public float walkSpeed = 3.5f;
        public float messPower = 1f;
        public Color allyColor = new Color(0.4f, 1f, 0.4f);

        [Header("Rebel Legacy")]
        [Tooltip("How much this ally adds to the legacy multiplier (e.g. 0.5 = +×0.50). " +
                 "Negative = automatic value based on Animal Name (Chicken 0.10, Duck 0.15, Frog 0.20, Dog 0.25, Cow 0.35, Horse 0.50).")]
        public float multiplierBonus = -1f;

        /// <summary>Multiplier bonus this ally gives (Inspector value, or the per-species default).</summary>
        public float MultiplierBonus => multiplierBonus >= 0f ? multiplierBonus : DefaultBonus(animalName);

        public static float DefaultBonus(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "chicken": return 0.10f;
                case "duck": return 0.15f;
                case "frog": return 0.20f;
                case "dog": return 0.25f;
                case "cow": return 0.35f;
                case "horse": return 0.50f;
                default: return 0.20f;
            }
        }

        /// <summary>Sum of the bonuses of every recruited ally.</summary>
        public static float TotalAllyBonus()
        {
            float sum = 0f;
            foreach (var a in Allies) if (a != null) sum += a.MultiplierBonus;
            return sum;
        }

        public bool IsAlly { get; private set; }

        Transform player;
        float progress;
        float nextMessTime;
        IChainReactionTarget goalTarget;
        Vector3 goalPos;
        readonly HashSet<IChainReactionTarget> used = new HashSet<IChainReactionTarget>();

        void OnDestroy() => Allies.Remove(this);

        void Update()
        {
            if (GameFlow.Instance != null && !GameFlow.Instance.IsRunning) return;
            if (!IsAlly) UpdateRecruit(); else UpdateAlly();
        }

        // ---------- recruiting ----------

        Transform Player()
        {
            if (player != null) return player;
            player = Butterfly.Death.ButterflyLocator.Find();
            return player;
        }

        bool PlayerNear()
        {
            var p = Player();
            return p != null && (p.position - transform.position).sqrMagnitude <= recruitRadius * recruitRadius;
        }

        void UpdateRecruit()
        {
            bool holding = PlayerNear() && Keyboard.current != null && Keyboard.current.eKey.isPressed;
            progress = holding ? progress + Time.deltaTime : Mathf.Max(0f, progress - Time.deltaTime * 2f);
            if (progress >= recruitTime) Recruit();
        }

        public void Recruit()
        {
            if (IsAlly) return;
            IsAlly = true;
            Allies.Add(this);
            nextMessTime = Time.time + UnityEngine.Random.Range(messInterval.x, messInterval.y);
            var r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = allyColor;
            Debug.Log($"[Rebel] {animalName} joined the rebellion ({Allies.Count} allies)");
            Recruited?.Invoke(this);
        }

        // ---------- ally mess making ----------

        void UpdateAlly()
        {
            if (goalTarget == null)
            {
                if (Time.time < nextMessTime) return;
                PickTarget();
                nextMessTime = Time.time + UnityEngine.Random.Range(messInterval.x, messInterval.y);
                return;
            }

            Vector3 flat = new Vector3(goalPos.x - transform.position.x, 0f, goalPos.z - transform.position.z);
            if (flat.magnitude < 1.2f)
            {
                float value = goalTarget.TriggerChain(transform.position, messPower);
                AllyMess?.Invoke(this, value);
                goalTarget = null;
                return;
            }

            Vector3 step = flat.normalized * walkSpeed * Time.deltaTime;
            Vector3 pos = transform.position + step;
            if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out var hit, 60f))
                pos.y = hit.point.y + 0.5f;
            transform.position = pos;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
        }

        void PickTarget()
        {
            IChainReactionTarget best = null;
            Vector3 bestPos = default;
            float bestDist = float.MaxValue;
            foreach (var c in Physics.OverlapSphere(transform.position, messRadius, ~0, QueryTriggerInteraction.Collide))
            {
                var t = c.GetComponentInParent<IChainReactionTarget>();
                if (t == null || used.Contains(t)) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = t; bestPos = c.transform.position; }
            }
            if (best == null) return;
            used.Add(best);
            goalTarget = best;
            goalPos = bestPos;
        }

        // ---------- feedback (replace with proper world-space UI later) ----------

        void OnGUI()
        {
            if (IsAlly || !PlayerNear()) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            if (sp.z < 0f) return;
            float pct = Mathf.Clamp01(progress / recruitTime);
            GUI.Label(new Rect(sp.x - 70, Screen.height - sp.y - 10, 160, 22), $"Hold E: recruit {animalName} (+×{MultiplierBonus:0.00}) {(int)(pct * 100)}%");
        }
    }
}
