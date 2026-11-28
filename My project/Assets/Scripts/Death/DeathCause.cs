using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Base class for every killer (spider, swatter, frog, bird, net, zapper lamp).
    /// Put a subclass on the killer object, then call DeathManager.Instance.Die(this, butterfly).
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

        [Header("Martyr fall")]
        [Tooltip("Velocity given to the butterfly at the moment of death.")]
        public Vector3 launchVelocity = new Vector3(0f, 2f, 0f);
        [Tooltip("Multiplier for legacy score earned by this death.")]
        public float legacyMultiplier = 1f;
        [Tooltip("Scale of the final chain reaction blast.")]
        public float chainPowerMultiplier = 1f;

        /// <summary>Called when the sequence begins (before slow-mo). Play the kill animation here.</summary>
        public virtual void OnDeathBegin(GameObject butterfly) { }

        /// <summary>Called when the butterfly lands and the chain reaction fires.</summary>
        public virtual void OnDeathLanded(GameObject butterfly, Vector3 point) { }

        /// <summary>Called when the whole sequence is over.</summary>
        public virtual void OnDeathEnd(GameObject butterfly, float legacyGained) { }
    }
}
