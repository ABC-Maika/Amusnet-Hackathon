using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class LooseHay : Interactable
    {
        private void OnTriggerEnter(Collider other)
        {
            TryInteract(other.GetComponentInParent<MessManager>());
        }

        protected override void OnInteracted(MessManager manager)
        {
            // Tip the bale so the player can tell which bales have scored.
            transform.Rotate(Vector3.forward, 14f, Space.Self);
        }
    }
}
