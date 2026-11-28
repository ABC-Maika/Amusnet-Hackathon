using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class DropLaundry : Interactable
    {
        [SerializeField] private Transform[] laundry;
        [SerializeField] private Interactable next;

        private void OnTriggerEnter(Collider other) => TryInteractWith(other);

        private void Update()
        {
            if (HasInteracted) return;
            for (int i = 0; i < laundry.Length; i++)
                laundry[i].localRotation = Quaternion.Euler(6f * Mathf.Sin(Time.time * 2.1f + i), 0f, 0f);
        }

        protected override void OnInteracted(MessManager manager) => StartCoroutine(Fall(manager));

        private IEnumerator Fall(MessManager manager)
        {
            var starts = new Vector3[laundry.Length];
            var rotations = new Quaternion[laundry.Length];
            for (int i = 0; i < laundry.Length; i++)
            {
                starts[i] = laundry[i].localPosition;
                rotations[i] = laundry[i].localRotation;
            }

            const float fallTime = 0.74f;
            const float stagger = 0.1f;
            float duration = fallTime + (laundry.Length - 1) * stagger;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                for (int i = 0; i < laundry.Length; i++)
                {
                    float progress = Mathf.Clamp01((elapsed - i * stagger) / fallTime);
                    Vector3 start = starts[i];
                    laundry[i].localPosition = start
                        + new Vector3((i - 1) * .2f * progress, (0.065f - start.y) * progress * progress, .42f * progress);
                    laundry[i].localRotation = rotations[i] * Quaternion.Euler(
                        progress * (90f + 25f * Mathf.Sin(progress * Mathf.PI * 2f)), 0f, 0f);
                }
                yield return null;
            }

            for (int i = 0; i < laundry.Length; i++)
            {
                laundry[i].localPosition = new Vector3(starts[i].x + (i - 1) * .2f, 0.065f, starts[i].z + .42f);
                laundry[i].localRotation = rotations[i] * Quaternion.Euler(90f, 0f, 0f);
            }
            if (next) next.TryInteract(manager, this);
        }
    }
}
