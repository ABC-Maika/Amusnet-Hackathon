using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// The predator (Flycatcher). Always circles in the sky around the butterfly; the circle tightens as the
    /// predator meter fills and, in the hide stage, closes in so it arrives exactly when the countdown ends.
    /// Strike(): dives at the butterfly, bites (Attack animation), eats it, flies off, then the Martyr's Last
    /// Flutter plays. Animator needs an "Attack" trigger (Flycatcher.controller has it).
    /// </summary>
    public class PredatorBird : MonoBehaviour
    {
        public static PredatorBird Instance { get; private set; }

        [Header("Model")]
        public Transform model;
        public Animator animator;
        [Tooltip("Longest side of the model is scaled to this many metres.")]
        public float targetSize = 2.2f;
        [Tooltip("Rotate the model if it flies sideways or backwards (try 90 / -90 / 180).")]
        public float modelYawOffset = 0f;
        public string attackTrigger = "Attack";

        [Header("Circling")]
        public float farRadius = 45f;
        public float nearRadius = 16f;
        public float farHeight = 20f;
        public float nearHeight = 7f;
        public float flySpeed = 9f;
        public float turnSharpness = 3f;

        [Header("Strike")]
        public float diveSpeed = 24f;
        public float attackSeconds = 1.4f;
        public float eatMoment = 0.45f;       // seconds into the attack animation
        public float flyAwaySeconds = 2f;

        public bool IsStriking { get; private set; }

        Transform butterfly;
        float angle;
        Vector3 velocity;
        Vector3 smoothPos;
        float mouthDistance;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (model == null && animator != null) model = animator.transform;
            NormaliseScale();
            ResolveButterfly();
            angle = Random.value * Mathf.PI * 2f;
            transform.position = CirclePoint(farRadius, farHeight);
            smoothPos = transform.position;
        }

        void NormaliseScale()
        {
            if (model == null) return;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (size > 0.0001f) model.localScale *= targetSize / size;
            mouthDistance = targetSize * 0.5f;
        }

        void ResolveButterfly()
        {
            butterfly = ButterflyLocator.Find();
        }

        Vector3 CirclePoint(float radius, float height)
        {
            if (butterfly == null) return transform.position;
            return butterfly.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius + Vector3.up * height;
        }

        void Update()
        {
            if (IsStriking) return;
            if (butterfly == null) { ResolveButterfly(); return; }

            // how close the bird wants to be (0 = far, 1 = on top of the butterfly)
            var meter = PredatorMeter.Instance;
            float danger = 0f;
            float closing = 0f;
            if (meter != null)
            {
                danger = meter.Percent / 100f;
                if (meter.CurrentStage == PredatorMeter.Stage.Hide && meter.arrivalSeconds > 0.01f)
                    closing = 1f - Mathf.Clamp01(meter.CountdownLeft / meter.arrivalSeconds);
            }

            float radius = Mathf.Lerp(farRadius, nearRadius, danger);
            float height = Mathf.Lerp(farHeight, nearHeight, danger);
            radius = Mathf.Lerp(radius, 4f, closing);
            height = Mathf.Lerp(height, 2.5f, closing);

            // angular speed so linear speed stays roughly flySpeed
            angle += (flySpeed / Mathf.Max(radius, 3f)) * Time.deltaTime;
            Vector3 target = CirclePoint(radius, height);

            Vector3 prev = transform.position;
            smoothPos = Vector3.Lerp(smoothPos, target, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
            transform.position = smoothPos;
            FaceMovement(transform.position - prev);
        }

        void FaceMovement(Vector3 delta)
        {
            if (delta.sqrMagnitude < 0.00001f) return;
            var look = Quaternion.LookRotation(delta.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        /// <summary>Dive, bite, eat, fly away, then call onEaten (starts the death sequence).</summary>
        public void Strike(GameObject victim, System.Action onEaten)
        {
            if (IsStriking) return;
            StartCoroutine(StrikeRoutine(victim, onEaten));
        }

        IEnumerator StrikeRoutine(GameObject victim, System.Action onEaten)
        {
            IsStriking = true;

            // freeze the player's control while the bird has them
            var frozen = new List<Behaviour>();
            foreach (var mb in victim.GetComponents<MonoBehaviour>())
                if (mb != null && mb.enabled) { mb.enabled = false; frozen.Add(mb); }
            var rb = victim.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // 1. dive
            while (true)
            {
                Vector3 mouthDir = (victim.transform.position - transform.position);
                float dist = mouthDir.magnitude;
                if (dist <= mouthDistance + 1.2f) break;
                Vector3 step = mouthDir.normalized * diveSpeed * Time.deltaTime;
                transform.position += step;
                FaceMovement(step);
                yield return null;
            }

            // 2. bite: attack animation, lunge the last bit, swallow at eatMoment
            if (animator != null && !string.IsNullOrEmpty(attackTrigger)) animator.SetTrigger(attackTrigger);
            float t = 0f;
            bool eaten = false;
            Vector3 startScale = victim.transform.localScale;
            while (t < attackSeconds)
            {
                t += Time.deltaTime;
                Vector3 mouth = transform.position + transform.forward * mouthDistance;
                Vector3 toVictim = victim.transform.position - mouth;
                transform.position += toVictim * Mathf.Min(1f, 8f * Time.deltaTime);
                FaceMovement(toVictim);

                if (t >= eatMoment)
                {
                    float k = Mathf.Clamp01((t - eatMoment) / 0.25f);
                    victim.transform.position = Vector3.Lerp(victim.transform.position, mouth, k);
                    victim.transform.localScale = startScale * (1f - k);
                    if (k >= 1f) eaten = true;
                }
                yield return null;
            }
            if (!eaten) victim.transform.localScale = Vector3.zero;

            // 3. gulp and fly away with the butterfly "inside"
            float away = 0f;
            Vector3 dir = (transform.forward + Vector3.up * 0.8f).normalized;
            while (away < flyAwaySeconds)
            {
                away += Time.deltaTime;
                Vector3 step = dir * flySpeed * 1.3f * Time.deltaTime;
                transform.position += step;
                FaceMovement(step);
                yield return null;
            }

            // 4. the spirit of the butterfly falls: Martyr's Last Flutter
            victim.transform.position = transform.position + Vector3.down * 0.5f;
            victim.transform.localScale = startScale;
            bool alreadyDying = DeathManager.Instance != null && DeathManager.Instance.IsDying;
            if (!alreadyDying) foreach (var b in frozen) if (b != null) b.enabled = true;
            if (rb != null) rb.isKinematic = false;
            onEaten?.Invoke();

            // bird keeps circling afterwards
            IsStriking = false;
            ResolveButterfly();
        }
    }
}
