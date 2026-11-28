using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Butterfly.Death
{
    /// <summary>
    /// "Martyr's Last Flutter". Call Die(cause, butterfly) from any killer or from the predator system.
    /// Sequence: slow-mo (~15%) -> camera follows fall -> ~3s steering -> landing -> chain reaction -> legacy bonus.
    /// </summary>
    public class DeathManager : MonoBehaviour
    {
        public static DeathManager Instance { get; private set; }

        [Header("Slow-mo")]
        [Range(0.05f, 1f)] public float slowMoScale = 0.15f;
        public float slowMoEaseIn = 0.15f;      // real seconds
        public float steerDuration = 3f;        // real seconds
        public float returnToNormalTime = 0.6f; // real seconds, after steering ends
        public float maxFallTime = 8f;          // real seconds safety net

        [Header("Fall")]
        public float fallGravityScale = 0.6f;
        public LayerMask groundMask = ~0;

        [Header("Final chain reaction")]
        public float chainRadius = 8f;
        public float chainPower = 1f;
        public float baseLegacy = 10f;
        public float legacyPerMessValue = 1f;
        public float blastImpulse = 6f;
        public float endDelay = 1.5f;

        [Header("Butterfly")]
        [Tooltip("Flight controller etc. that must stop while falling. Leave empty to auto-disable all other MonoBehaviours on the butterfly.")]
        public Behaviour[] disableOnDeath;

        [Header("Events")]
        public UnityEvent<DeathCause> onDeathStarted;
        public UnityEvent<float> onLegacyAwarded;   // legacy gained
        public UnityEvent onDeathFinished;          // hook GameFlow.Lose() here

        public static event Action<DeathCause> DeathStarted;
        public static event Action<float> LegacyAwarded;
        public static event Action DeathFinished;

        public bool IsDying { get; private set; }

        const float BaseFixedDelta = 0.02f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; SetTime(1f); }
        }

        /// <summary>Entry point for every killer.</summary>
        public void Die(DeathCause cause, GameObject butterfly)
        {
            if (IsDying || cause == null || butterfly == null) return;
            StartCoroutine(Sequence(cause, butterfly));
        }

        IEnumerator Sequence(DeathCause cause, GameObject butterfly)
        {
            IsDying = true;
            DeathStarted?.Invoke(cause);
            onDeathStarted?.Invoke(cause);
            cause.OnDeathBegin(butterfly);

            // Stop normal control.
            if (disableOnDeath != null && disableOnDeath.Length > 0)
            {
                foreach (var b in disableOnDeath) if (b) b.enabled = false;
            }
            else
            {
                foreach (var mb in butterfly.GetComponents<MonoBehaviour>())
                    if (mb != null && !(mb is MartyrSteering) && !(mb is LandingDetector)) mb.enabled = false;
            }

            // The real flight controller moves with a CharacterController, which a Rigidbody can't collide
            // with. Swap it for a small sphere so the fall can land.
            var cc = butterfly.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                cc.enabled = false;
                var sphere = butterfly.GetComponent<SphereCollider>();
                if (sphere == null) sphere = butterfly.AddComponent<SphereCollider>();
                sphere.center = cc.center;
                sphere.radius = Mathf.Max(0.05f, cc.radius * 0.6f);
            }

            // Physics body.
            var rb = butterfly.GetComponent<Rigidbody>();
            if (rb == null) rb = butterfly.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = cause.launchVelocity;

            var landing = butterfly.GetComponent<LandingDetector>();
            if (landing == null) landing = butterfly.AddComponent<LandingDetector>();
            landing.Init(groundMask);

            var steering = butterfly.GetComponent<MartyrSteering>();
            if (steering == null) steering = butterfly.AddComponent<MartyrSteering>();
            steering.enabled = true;

            // Camera.
            MartyrCamera martyrCam = null;
            if (Camera.main != null)
            {
                foreach (var mb in Camera.main.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name.Contains("CinemachineBrain")) mb.enabled = false;
                martyrCam = Camera.main.gameObject.AddComponent<MartyrCamera>();
                martyrCam.target = butterfly.transform;
            }

            // Slow-mo ease in.
            float startScale = Time.timeScale;
            for (float t = 0f; t < slowMoEaseIn; t += Time.unscaledDeltaTime)
            {
                while (Butterfly.UI.PauseMenu.IsPaused) yield return null;
                SetTime(Mathf.Lerp(startScale, slowMoScale, t / slowMoEaseIn));
                yield return null;
            }
            SetTime(slowMoScale);

            // Steering window: she drifts down slowly.
            float elapsed = 0f;
            while (elapsed < steerDuration && !landing.Landed)
            {
                while (Butterfly.UI.PauseMenu.IsPaused) yield return null;
                elapsed += Time.unscaledDeltaTime;
                rb.AddForce(Physics.gravity * fallGravityScale, ForceMode.Acceleration);
                yield return null;
            }

            // Steering over: back to normal time, fall for real.
            steering.enabled = false;
            rb.useGravity = true;
            for (float t = 0f; t < returnToNormalTime && !landing.Landed; t += Time.unscaledDeltaTime)
            {
                while (Butterfly.UI.PauseMenu.IsPaused) yield return null;
                SetTime(Mathf.Lerp(slowMoScale, 1f, t / returnToNormalTime));
                yield return null;
            }
            SetTime(1f);

            float safety = 0f;
            while (!landing.Landed && safety < maxFallTime)
            {
                safety += Time.deltaTime;
                yield return null;
            }
            SetTime(1f);

            // Landing: final chain reaction.
            Vector3 point = landing.Landed ? landing.LandPoint : butterfly.transform.position;
            float messValue = ChainReaction(point, cause);
            float legacy = (baseLegacy + messValue * legacyPerMessValue) * cause.legacyMultiplier
                           * LegacyBank.AllyMultiplier(Butterfly.Animals.RebelAnimal.TotalAllyBonus());
            LegacyBank.Add(legacy);
            LegacyBank.RecordDeathPoint(point);
            cause.OnDeathLanded(butterfly, point);
            LegacyAwarded?.Invoke(legacy);
            onLegacyAwarded?.Invoke(legacy);

            yield return new WaitForSeconds(endDelay); // let the chaos play out

            cause.OnDeathEnd(butterfly, legacy);
            if (martyrCam) Destroy(martyrCam);
            DeathFinished?.Invoke();
            onDeathFinished?.Invoke();
            IsDying = false;
        }

        float ChainReaction(Vector3 origin, DeathCause cause)
        {
            float power = chainPower * cause.chainPowerMultiplier;
            float total = 0f;
            var done = new HashSet<IChainReactionTarget>();
            var hits = Physics.OverlapSphere(origin, chainRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits)
            {
                foreach (var target in h.GetComponentsInParent<IChainReactionTarget>())
                    if (done.Add(target)) total += target.TriggerChain(origin, power);

                var body = h.attachedRigidbody;
                if (body != null && !body.isKinematic)
                    body.AddExplosionForce(blastImpulse * power, origin, chainRadius, 0.5f, ForceMode.Impulse);
            }
            return total;
        }

        static void SetTime(float s)
        {
            Time.timeScale = s;
            Time.fixedDeltaTime = BaseFixedDelta * s; // keep physics smooth in slow-mo
        }
    }
}
