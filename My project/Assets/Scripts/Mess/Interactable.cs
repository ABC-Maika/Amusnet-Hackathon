using UnityEngine;

namespace Amusnet.Mess
{
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField, Min(1)] private int messValue = 5;
        public bool HasInteracted { get; private set; }

        // Both direct interactions and chains use this entry point. Mark it spent
        // before callbacks so multiple contacts or a circular chain score only once.
        public bool TryInteract(MessManager manager)
        {
            if (!manager || !isActiveAndEnabled || HasInteracted || messValue <= 0) return false;
            HasInteracted = true;
            manager.AddMess(messValue);
            OnInteracted(manager);
            return true;
        }

        protected virtual void OnInteracted(MessManager manager) { }
    }
}
