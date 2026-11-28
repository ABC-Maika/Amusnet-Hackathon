using UnityEngine;
using UnityEngine.InputSystem;

namespace Butterfly.Death
{
    /// <summary>
    /// Test killer for the death scene. Put it on any object together with a DeathManager in the scene,
    /// assign the butterfly (or a capsule), press K to trigger the Martyr's Last Flutter.
    /// Real killers (spider, swatter...) copy this pattern: subclass DeathCause and call DeathManager.Instance.Die(this, butterfly).
    /// </summary>
    public class DebugDeathCause : DeathCause
    {
        public GameObject butterfly;

        void Reset() { displayName = "Debug"; }

        void Update()
        {
            if (Butterfly.UI.PauseMenu.IsPaused) return;
            if (Keyboard.current != null &&Keyboard.current.kKey.wasPressedThisFrame && DeathManager.Instance != null)
                DeathManager.Instance.Die(this, butterfly);
        }

        public override void OnDeathLanded(GameObject b, Vector3 point)
            => Debug.Log($"[Death] {displayName}: landed at {point}");

        public override void OnDeathEnd(GameObject b, float legacyGained)
            => Debug.Log($"[Death] {displayName}: score +{legacyGained:0.#}");
    }
}
