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
            legacyMultiplier = 1f;
            launchVelocity = new Vector3(0f, 1.5f, 0f);
        }

        public override void OnDeathBegin(GameObject butterfly)
        {
            // Frog model's tongue animation is driven by the Strike trigger in ProximityKiller.Strike().
        }
    }
}
