using UnityEngine;

namespace Amusnet.Mess
{
    public abstract class Interactable : MonoBehaviour, global::Butterfly.Death.IChainReactionTarget
    {
        [SerializeField, Min(1)] private int messValue = 5;
        [SerializeField] private AudioClip impact;
        [SerializeField] private ParticleSystem debris;
        public Interactable ChainRoot { get; private set; }
        public int ChainLength { get; private set; }
        public int Points => messValue;
        public bool HasInteracted { get; private set; }

        protected void TryInteractWith(Collider other)
        {
            if (!HasInteracted)
                TryInteract(other.GetComponentInParent<MessManager>());
        }

        // Both direct interactions and chains use this entry point. Mark it spent
        // before callbacks so multiple contacts or a circular chain score only once.
        public bool TryInteract(MessManager manager) => TryInteract(manager, null);

        public bool TryInteract(MessManager manager, Interactable cause)
        {
            if (!manager || !manager.CanInteract || !isActiveAndEnabled || HasInteracted || messValue <= 0) return false;
            HasInteracted = true;
            ChainRoot = cause && cause.ChainRoot ? cause.ChainRoot : this;
            ChainRoot.ChainLength++;
            Sound(impact);
            if (debris) debris.Play();
            manager.RecordInteraction(this);
            OnInteracted(manager);
            return true;
        }

        protected void Sound(AudioClip clip, float pitch = 1f)
        {
            if (!clip) return;
            if (!TryGetComponent<AudioSource>(out var source))
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = .82f;
                source.minDistance = 6f;
                source.maxDistance = 28f;
                source.rolloffMode = AudioRolloffMode.Linear;
            }
            source.pitch = pitch;
            source.PlayOneShot(clip, .62f);
        }

        protected virtual void OnInteracted(MessManager manager) { }

        // The martyr's landing blast knocks over nearby props as real mess.
        float global::Butterfly.Death.IChainReactionTarget.TriggerChain(Vector3 origin, float power)
        {
            var manager = FindFirstObjectByType<MessManager>();
            return TryInteract(manager) ? Points * Mathf.Max(0f, power) : 0f;
        }
    }
}
