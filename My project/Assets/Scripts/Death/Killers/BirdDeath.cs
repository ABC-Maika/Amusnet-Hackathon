using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// The predator's kill. Triggered by PredatorMeter (not by proximity). Hooks for the Flycatcher model:
    /// assign Animator + Flycatcher prefab and it dives at the butterfly during OnDeathBegin.
    /// </summary>
    public class BirdDeath : DeathCause
    {
        [Tooltip("Optional: Flycatcher prefab spawned to dive at the butterfly.")]
        public GameObject birdPrefab;
        public string strikeTrigger = "Attack";

        void Reset()
        {
            displayName = "Bird";
            warningTime = 3f;
            telegraphTime = 1.5f;
            legacyMultiplier = 1.2f;
            launchVelocity = new Vector3(0f, 2.5f, 0f);
        }

        void Awake() { if (displayName == "Unknown") Reset(); }

        public override void OnDeathBegin(GameObject butterfly)
        {
            if (birdPrefab == null) return;
            Vector3 start = butterfly.transform.position + new Vector3(4f, 3f, 4f);
            var bird = Instantiate(birdPrefab, start, Quaternion.LookRotation(butterfly.transform.position - start));
            var anim = bird.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(strikeTrigger);
            Destroy(bird, 4f);
        }
    }
}
