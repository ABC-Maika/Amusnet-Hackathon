using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class LooseHay : Interactable
    {
        private void OnTriggerEnter(Collider other) => TryInteractWith(other);

        protected override void OnInteracted(MessManager manager)
        {
            StartCoroutine(Tip());
        }

        private IEnumerator Tip()
        {
            Quaternion upright = transform.localRotation;
            const float duration = 0.55f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                float progress = elapsed / duration;
                float angle = 25f * Mathf.SmoothStep(0f, 1f, progress)
                    + 6f * Mathf.Sin(progress * Mathf.PI * 2f) * progress * (1f - progress);
                transform.localRotation = upright * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            transform.localRotation = upright * Quaternion.Euler(0f, 0f, 25f);
        }
    }
}
