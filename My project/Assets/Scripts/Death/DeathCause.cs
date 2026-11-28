using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>What a predator leaves behind after digesting the butterfly.</summary>
    public enum PoopKind { Bird, Frog, Fish }

    /// <summary>
    /// Base class for every killer (spider, swatter, frog, bird, net, zapper lamp, river fish).
    /// Put a subclass on the killer object, then call DeathManager.Instance.Die(this, butterfly).
    /// Predators (EatsVictim) swallow the butterfly: the camera follows them while they digest, then follows
    /// their poop, whose splat knocks props over for score. Other killers make the butterfly go limp and fall.
    /// Override the hooks to play kill animations, spawn VFX, etc.
    /// </summary>
    public abstract class DeathCause : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Unknown";

        [Header("Timing (tunables from the death design doc)")]
        [Tooltip("Seconds the killer telegraphs before it can kill. Used by the killer's own AI.")]
        public float telegraphTime = 1f;
        [Tooltip("Seconds of warning (sound/UI) before the telegraph. Used by the killer's own AI.")]
        public float warningTime = 1.5f;

        [Header("Death")]
        [Tooltip("Velocity given to the butterfly when it goes limp and falls (killers that don't eat it).")]
        public Vector3 launchVelocity = new Vector3(0f, 2f, 0f);
        [Tooltip("Scale of the mess blast when a predator's poop splats.")]
        public float chainPowerMultiplier = 1f;

        /// <summary>True for predators that swallow the butterfly (bird, frog, fish).</summary>
        public virtual bool EatsVictim => false;
        /// <summary>The animal the camera follows while it digests.</summary>
        public virtual Transform Eater => transform;
        /// <summary>The poop it leaves behind.</summary>
        public virtual PoopKind Poop => PoopKind.Bird;

        /// <summary>Where the poop comes out: just under a bird, just behind anything else.</summary>
        public virtual Vector3 PoopPoint(Transform eater)
        {
            // Mesh bounds, not skinned bounds: a skinned frog's bounds include its fully stretched tongue.
            var bounds = new Bounds(eater.position, Vector3.one * 0.4f);
            foreach (var f in eater.GetComponentsInChildren<MeshFilter>()) bounds.Encapsulate(f.GetComponent<Renderer>().bounds);
            Vector3 back = Vector3.ProjectOnPlane(-eater.forward, Vector3.up).normalized;
            return Poop == PoopKind.Bird
                ? new Vector3(bounds.center.x, bounds.min.y - 0.15f, bounds.center.z)
                : bounds.center + back * (Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.2f);
        }

        /// <summary>Called as the predator poops; animate it here (e.g. the frog's squat).</summary>
        public virtual void OnPooped(Transform eater) { }

        /// <summary>Called when the sequence begins (before slow-mo). Play the kill animation here.</summary>
        public virtual void OnDeathBegin(GameObject butterfly) { }

        /// <summary>Called when the butterfly (or, for predators, its poop) hits the ground.</summary>
        public virtual void OnDeathLanded(GameObject butterfly, Vector3 point) { }

        /// <summary>Called when the whole sequence is over; scoreGained is what the poop splat scored (0 for falls).</summary>
        public virtual void OnDeathEnd(GameObject butterfly, float scoreGained) { }
    }
}
