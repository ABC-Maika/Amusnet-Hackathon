using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class OpenGate : Interactable
    {
        [SerializeField] private Transform leaf;
        [SerializeField] private Interactable next;

        private void OnTriggerEnter(Collider other) => TryInteractWith(other);

        protected override void OnInteracted(MessManager manager) => StartCoroutine(Swing(manager));

        private IEnumerator Swing(MessManager manager)
        {
            Quaternion closed = leaf.localRotation;
            Quaternion open = closed * Quaternion.Euler(0f, 102f, 0f);
            const float duration = .6f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                leaf.localRotation = Quaternion.Slerp(closed, open, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            leaf.localRotation = open;
            if (next) next.TryInteract(manager);
        }
    }
}
