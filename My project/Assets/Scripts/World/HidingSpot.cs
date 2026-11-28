using UnityEngine;

namespace Amusnet.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HidingSpot : MonoBehaviour
    {
        private BoxCollider volume;
        void Awake() => volume = GetComponent<BoxCollider>();

        // Disabling or teleporting a controller can skip the physics exit callback.
        public bool StillOverlaps(Collider body) =>
            volume && volume.enabled && volume.bounds.Intersects(body.bounds);

        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
        void OnTriggerEnter(Collider other) => other.GetComponentInParent<ShelterState>()?.Enter(this, other);
        void OnTriggerExit(Collider other) => other.GetComponentInParent<ShelterState>()?.Leave(this, other);
    }
}
