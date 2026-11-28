using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Frog killer: lives at the pond, only hunts a butterfly flying low over the water.
    /// Drop it on the frog model, assign the Animator (triggers: Warn, Telegraph, Strike, Reset).
    /// Defaults match docs/DeathDesign.md: ripple/croak warning, 4 s charge (2.5 s warning + 1.5 s tongue wind-up) shown by the warning bar.
    /// </summary>
    public class FrogDeath : ProximityKiller
    {
        static readonly int AimUp = Animator.StringToHash("AimUp");

        void LateUpdate()
        {
            if (!animator || !butterfly || (Current != State.Warning && Current != State.Telegraph)) return;
            Vector3 direction = Vector3.ProjectOnPlane(butterfly.position - transform.position, transform.up);
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, transform.up);

            // The tongue tip rests 0.71 units forward and 0.47 above the prefab origin.
            Vector3 fromMouth = transform.InverseTransformPoint(butterfly.position) - new Vector3(0f, 0.47f, 0.71f);
            float elevation = Mathf.Atan2(fromMouth.y, Mathf.Max(0f, fromMouth.z)) * Mathf.Rad2Deg;
            animator.SetFloat(AimUp, Mathf.Clamp01(elevation / 65f));
        }

        void Reset()
        {
            displayName = "Frog";
            warnRadius = 6f;
            strikeRadius = 3f;
            maxHeightAbove = 3f;
            warningTime = 2.5f;     // bar fills over warning + telegraph = 4 s to get away
            telegraphTime = 1.5f;
            warningBarOffset = new Vector3(0f, 1.0f, 0f);
            cooldown = 3f;
            launchVelocity = new Vector3(0f, 1.5f, 0f);
        }

        public override bool EatsVictim => true;
        public override PoopKind Poop => PoopKind.Frog;

        // Just past its rump, on the ground. Measured from the frog's current pose: the skinned bounds would
        // include the stretched tongue and put the poop metres away.
        public override Vector3 PoopPoint(Transform eater)
        {
            Vector3 back = -Vector3.ProjectOnPlane(eater.forward, Vector3.up).normalized;
            float rump = 0.5f;
            var skin = eater.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin)
            {
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                var toWorld = skin.transform.localToWorldMatrix;
                foreach (var v in baked.vertices)
                    rump = Mathf.Max(rump, Vector3.Dot(toWorld.MultiplyPoint3x4(v) - eater.position, back));
                Destroy(baked);
            }
            return eater.position + back * (rump + 0.12f) + Vector3.up * 0.08f;
        }

        public override void OnPooped(Transform eater) => StartCoroutine(Squat());

        // Squash down and spring back as it pushes the poop out.
        System.Collections.IEnumerator Squat()
        {
            Vector3 rest = transform.localScale;
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / 0.45f * Mathf.PI);
                transform.localScale = new Vector3(rest.x * (1f + 0.12f * k), rest.y * (1f - 0.18f * k), rest.z * (1f + 0.12f * k));
                yield return null;
            }
            transform.localScale = rest;
        }

        public override void OnDeathBegin(GameObject butterfly)
        {
            // Frog model's tongue animation is driven by the Strike trigger in ProximityKiller.Strike().
        }
    }
}
