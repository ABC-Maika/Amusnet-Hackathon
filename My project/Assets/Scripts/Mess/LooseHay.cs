using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class LooseHay : Interactable
    {
        [SerializeField] private bool tumble;
        [SerializeField] private Terrain terrain;
        private void OnTriggerEnter(Collider other) => TryInteractWith(other);

        protected override void OnInteracted(MessManager manager)
        {
            StartCoroutine(Tip());
        }

        private IEnumerator Tip()
        {
            if (tumble)
            {
                Vector3 start = transform.position;
                Quaternion rest = transform.rotation;
                const float halfWidth = .55f;
                for (float t = 0f; ; t += Time.deltaTime)
                {
                    float u = Mathf.Clamp01(t / .86f);
                    float a = Mathf.SmoothStep(0, Mathf.PI * .5f, u);
                    Vector3 p = start + rest * Vector3.forward * (halfWidth * (1f - Mathf.Cos(a)) + .55f * Mathf.Sin(a));
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y + halfWidth * Mathf.Sin(a);
                    transform.SetPositionAndRotation(p, rest * Quaternion.Euler(a * Mathf.Rad2Deg, 0, 0));
                    if (u >= 1f) break;
                    yield return null;
                }
                yield break;
            }
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
