using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class StartleChicken : Interactable
    {
        [SerializeField] private KnockOverPot pot;
        [SerializeField] private Transform body;
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Animator animator;
        [SerializeField] private Terrain terrain;

        private Vector3 perch;
        private Vector3 bodyRest;
        private Quaternion restingRotation;
        private float idleStart;

        private void Start()
        {
            perch = transform.position;
            bodyRest = body.localPosition;
            restingRotation = transform.rotation;
            idleStart = Time.time;
        }

        private void Update()
        {
            if (HasInteracted) return;

            float phase = Mathf.Repeat(Time.time - idleStart, 6f);
            float outward = Mathf.Clamp01((phase - 1.2f) / .9f);
            float inward = Mathf.Clamp01((phase - 4.1f) / .9f);
            bool walking = outward is > 0f and < 1f || inward is > 0f and < 1f;
            Vector3 forward = restingRotation * Vector3.forward;
            transform.position = Grounded(perch + forward * (.55f * (outward - inward)));
            Quaternion idleHeading = phase >= 3.7f && phase < 5f
                ? restingRotation * Quaternion.Euler(0f, 180f, 0f) : restingRotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, idleHeading, 350f * Time.deltaTime);
            if (animator)
            {
                animator.SetFloat("Speed", walking ? 1f : 0f, .1f, Time.deltaTime);
                return;
            }
            float stride = walking ? Mathf.Sin(phase * 22f) : 0f;
            float peck = walking ? 0f : Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * 5.8f)), 5f);
            body.localPosition = bodyRest + Vector3.up * (.035f * Mathf.Abs(stride) - .06f * peck);
            body.localRotation = Quaternion.Euler(21f * peck, 0f, 0f);
            leftLeg.localRotation = Quaternion.Euler(21f * stride, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-21f * stride, 0f, 0f);
        }

        private void OnTriggerEnter(Collider other) => TryInteractWith(other);

        protected override void OnInteracted(MessManager manager)
        {
            StartCoroutine(Scurry(manager));
        }

        private IEnumerator Scurry(MessManager manager)
        {
            Vector3 origin = transform.position;
            Vector3 toPot = pot ? Vector3.ProjectOnPlane(pot.transform.position - origin, Vector3.up) : transform.forward * 2f;
            Vector3 direction = toPot.sqrMagnitude > 0.01f ? toPot.normalized : transform.forward;
            Quaternion facing = transform.rotation;
            Quaternion running = Quaternion.LookRotation(direction);
            Quaternion pecking = body.localRotation;

            const float windup = 0.18f;
            for (float elapsed = 0f; elapsed < windup; elapsed += Time.deltaTime)
            {
                float progress = elapsed / windup;
                transform.rotation = Quaternion.Slerp(facing, running, progress);
                body.localRotation = Quaternion.Slerp(pecking, Quaternion.identity, progress);
                body.localPosition = bodyRest - Vector3.up * (0.10f * Mathf.Sin(progress * Mathf.PI));
                yield return null;
            }

            transform.rotation = running;
            body.localRotation = Quaternion.identity;
            if (animator) animator.SetFloat("Speed", 2f);
            float impactDistance = Mathf.Max(0f, toPot.magnitude - 0.40f);
            float runDistance = impactDistance + 1.9f;
            float duration = Mathf.Max(0.6f, runDistance / 3.2f);
            bool reachedPot = false;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float progress = elapsed / duration;
                float distance = runDistance * Mathf.SmoothStep(0f, 1f, progress);
                float stride = Mathf.Sin(elapsed * 37f);
                float hop = Mathf.Abs(stride) * Mathf.Sin(Mathf.PI * progress);
                transform.position = Grounded(origin + direction * distance);
                body.localPosition = bodyRest + Vector3.up * (hop * 0.12f);
                leftLeg.localRotation = Quaternion.Euler(stride * 32f, 0f, 0f);
                rightLeg.localRotation = Quaternion.Euler(-stride * 32f, 0f, 0f);
                float flap = 12f + Mathf.Abs(stride) * 30f;
                leftWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
                rightWing.localRotation = Quaternion.Euler(0f, 0f, flap);
                if (!reachedPot && distance >= impactDistance)
                {
                    reachedPot = true;
                    if (pot) pot.TryInteract(manager, this);
                }
                yield return null;
            }

            transform.position = Grounded(origin + direction * runDistance);
            body.localPosition = bodyRest;
            leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
            leftWing.localRotation = rightWing.localRotation = Quaternion.identity;
            if (animator) animator.SetFloat("Speed", 0f);
            if (!reachedPot && pot) pot.TryInteract(manager, this);
        }

        private Vector3 Grounded(Vector3 position)
        {
            if (Ground(terrain))
                position.y = Ground(terrain).transform.position.y + Ground(terrain).SampleHeight(position) + .04f;
            return position;
        }
    }
}
