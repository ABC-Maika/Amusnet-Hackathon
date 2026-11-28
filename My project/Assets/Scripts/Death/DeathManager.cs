using System;
using System.Collections;
using System.Collections.Generic;
using Amusnet.Mess;
using UnityEngine;
using UnityEngine.Events;

namespace Butterfly.Death
{
    /// <summary>
    /// Runs every death. Call Die(cause, butterfly) from any killer or from the predator system.
    ///  - Eaten (bird, frog, fish: cause.EatsVictim): the butterfly is swallowed, the camera follows the predator
    ///    while it digests (3-5 s), then it poops; the camera follows the poop down, and its splat knocks the
    ///    props around it over for score.
    ///  - Caught (spider web, swatter, net, zapper lamp): the butterfly goes limp and falls; the camera lingers on it.
    /// Then DeathFinished fires (GameIntegration turns it into GameFlow.Lose).
    /// </summary>
    public class DeathManager : MonoBehaviour
    {
        public static DeathManager Instance { get; private set; }

        [Header("Eaten")]
        [Tooltip("Seconds the camera follows the predator before it poops (random in this range).")]
        public Vector2 digestSeconds = new Vector2(3f, 5f);
        public float swallowSeconds = 0.35f;

        [Header("Caught (limp fall)")]
        public float maxFallTime = 6f;
        public LayerMask groundMask = ~0;

        [Header("Poop splat")]
        public float chainRadius = 8f;
        public float chainPower = 1f;
        public float blastImpulse = 6f;
        [Tooltip("Seconds the camera lingers on the poop or the fallen butterfly before the game-over screen.")]
        public float endDelay = 2f;

        [Header("Butterfly")]
        [Tooltip("Flight controller etc. that must stop. Leave empty to auto-disable all other MonoBehaviours on the butterfly.")]
        public Behaviour[] disableOnDeath;

        [Header("Events")]
        public UnityEvent<DeathCause> onDeathStarted;
        public UnityEvent onDeathFinished;          // hook GameFlow.Lose() here

        public static event Action<DeathCause> DeathStarted;
        /// <summary>The predator has digested the butterfly and pooped.</summary>
        public static event Action<DeathCause> Pooped;
        /// <summary>Score the poop splat earned (props knocked over).</summary>
        public static event Action<int> ScoreAwarded;
        public static event Action DeathFinished;

        public bool IsDying { get; private set; }

        // Enter Play Mode without a domain reload keeps statics.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; DeathStarted = null; Pooped = null; ScoreAwarded = null; DeathFinished = null; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Entry point for every killer.</summary>
        public void Die(DeathCause cause, GameObject butterfly)
        {
            if (IsDying || cause == null || butterfly == null) return;
            // No deaths on the menu, while paused, during the win cinematic or after the run has ended.
            if (GameFlow.Instance != null && !GameFlow.Instance.IsRunning) return;
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
                    if (mb != null && !(mb is LandingDetector)) mb.enabled = false;
            }

            // The death camera takes over from Cinemachine.
            DeathCamera cam = null;
            if (Camera.main != null)
            {
                foreach (var mb in Camera.main.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name.Contains("CinemachineBrain")) mb.enabled = false;
                cam = Camera.main.gameObject.AddComponent<DeathCamera>();
            }

            float score = 0f;
            var eater = cause.EatsVictim ? cause.Eater : null;
            if (eater != null) yield return Eaten(cause, eater, butterfly, cam, s => score = s);
            else yield return Caught(cause, butterfly, cam);

            cause.OnDeathEnd(butterfly, score);
            if (cam) Destroy(cam);
            DeathFinished?.Invoke();
            onDeathFinished?.Invoke();
            IsDying = false;
        }

        // ---------- bird, frog, fish ----------

        IEnumerator Eaten(DeathCause cause, Transform eater, GameObject butterfly, DeathCamera cam, Action<float> scored)
        {
            var cc = butterfly.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Swallow: the butterfly shrinks into the predator's mouth, then is gone.
            Vector3 startScale = butterfly.transform.localScale;
            Vector3 startPos = butterfly.transform.position;
            for (float t = 0f; t < swallowSeconds; t += Time.deltaTime)
            {
                if (eater == null) break;
                float k = t / swallowSeconds;
                butterfly.transform.position = Vector3.Lerp(startPos, Mouth(eater), k);
                butterfly.transform.localScale = startScale * (1f - k);
                yield return null;
            }
            foreach (var r in butterfly.GetComponentsInChildren<Renderer>()) r.enabled = false;
            butterfly.transform.localScale = startScale;
            if (eater != null) StartCoroutine(Gulp(eater));

            // Follow the predator while it digests.
            if (cam) cam.Follow(eater, FollowDistance(eater), FollowDistance(eater) * 0.45f);
            float digest = UnityEngine.Random.Range(digestSeconds.x, digestSeconds.y);
            for (float t = 0f; t < digest; t += Time.deltaTime)
            {
                if (eater == null) break;
                yield return null;
            }

            // Poop: from under the bird, behind the frog, beside the fish.
            Vector3 drop = eater != null ? cause.PoopPoint(eater) : butterfly.transform.position;
            if (eater != null) cause.OnPooped(eater);
            var poop = Poop.Spawn(cause.Poop, drop);
            Pooped?.Invoke(cause);
            // Look at the poop from the side away from the predator, so it sits in the background, not in the way.
            Vector3 away = eater != null ? drop - eater.position : Vector3.zero;
            // Frog and fish poop sit low in grass or water: look down on them from above the grass.
            bool low = cause.Poop != PoopKind.Bird;
            if (cam) cam.Follow(poop.transform, low ? 0.7f : 1.6f, low ? 2.1f : 0.9f, away);
            for (float t = 0f; t < 8f && !poop.Landed; t += Time.deltaTime) yield return null;

            // Splat: the props around it get knocked over and score.
            Vector3 point = poop.Landed ? poop.LandPoint : poop.transform.position;
            cause.OnDeathLanded(butterfly, point);
            var mess = FindFirstObjectByType<MessManager>();
            int before = mess ? mess.Score : 0;
            ChainReaction(point, cause);
            VFX.MessPop(point + Vector3.up * 0.2f);
            int gained = mess ? mess.Score - before : 0;
            ScoreAwarded?.Invoke(gained);
            scored(gained);

            for (float t = 0f; t < endDelay; t += Time.deltaTime) yield return null;
        }

        static Vector3 Mouth(Transform eater) => eater.position + eater.forward * 0.3f + Vector3.up * 0.3f;

        // How far back the camera sits from a predator: a bit more than its size.
        static float FollowDistance(Transform eater)
        {
            float size = 1f;
            foreach (var r in eater.GetComponentsInChildren<Renderer>()) size = Mathf.Max(size, r.bounds.size.magnitude);
            return Mathf.Clamp(size * 2.2f, 3f, 9f);
        }

        // A quick swell so the swallow reads.
        static IEnumerator Gulp(Transform eater)
        {
            Vector3 rest = eater.localScale;
            for (float t = 0f; t < 0.3f && eater; t += Time.deltaTime)
            {
                eater.localScale = rest * (1f + Mathf.Sin(t / 0.3f * Mathf.PI) * 0.15f);
                yield return null;
            }
            if (eater) eater.localScale = rest;
        }

        // ---------- spider web, swatter, net, zapper lamp ----------

        IEnumerator Caught(DeathCause cause, GameObject butterfly, DeathCamera cam)
        {
            // The flight controller moves with a CharacterController, which a Rigidbody can't collide with:
            // swap it for a small sphere so the fall can land.
            var cc = butterfly.GetComponent<CharacterController>();
            var sphere = butterfly.GetComponent<SphereCollider>();
            if (!sphere)
                sphere = butterfly.AddComponent<SphereCollider>();

            sphere.enabled = true;
            sphere.isTrigger = false;

            if (cc)
            {
                cc.enabled = false;
                sphere.center = cc.center;
                sphere.radius = Mathf.Max(0.05f, cc.radius);
            }

            // Go limp: wings stop, the body tips over.
            foreach (var anim in butterfly.GetComponentsInChildren<Animator>()) anim.speed = 0f;

            var rb = butterfly.GetComponent<Rigidbody>();
            if (rb == null) rb = butterfly.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 1.5f; // a butterfly flutters down rather than drops like a stone
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = cause.launchVelocity;
            rb.angularVelocity = new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));

            var landing = butterfly.GetComponent<LandingDetector>();
            if (landing == null) landing = butterfly.AddComponent<LandingDetector>();
            var flight = butterfly.GetComponent<ButterflyController>();
            landing.Init(groundMask, rb, sphere, flight?.LastTerrainPosition);

            if (cam) cam.Follow(butterfly.transform, 2.2f, 1.2f);
            for (float t = 0f; t < maxFallTime && !landing.Landed; t += Time.deltaTime) yield return null;
            cause.OnDeathLanded(butterfly, landing.Landed ? landing.LandPoint : butterfly.transform.position);

            for (float t = 0f; t < endDelay + 0.5f; t += Time.deltaTime) yield return null;
        }

        void ChainReaction(Vector3 origin, DeathCause cause)
        {
            float power = chainPower * cause.chainPowerMultiplier;
            var done = new HashSet<IChainReactionTarget>();
            var hits = Physics.OverlapSphere(origin, chainRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits)
            {
                foreach (var target in h.GetComponentsInParent<IChainReactionTarget>())
                    if (done.Add(target)) target.TriggerChain(origin, power);

                var body = h.attachedRigidbody;
                if (body != null && !body.isKinematic)
                    body.AddExplosionForce(blastImpulse * power, origin, chainRadius, 0.5f, ForceMode.Impulse);
            }
        }
    }
}
