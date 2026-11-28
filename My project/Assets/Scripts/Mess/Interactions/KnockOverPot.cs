using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class KnockOverPot : Interactable
    {
        [SerializeField] private Transform ceramic;
        [SerializeField, Min(0.01f)] private float baseRadius = 0.19f;

        private void OnTriggerEnter(Collider other) => TryInteractWith(other);

        protected override void OnInteracted(MessManager manager)
        {
            StartCoroutine(Topple());
        }

        private IEnumerator Topple()
        {
            const float duration = 0.81f;
            Vector3 hinge = Vector3.right * baseRadius;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float progress = elapsed / duration;
                float angle = progress < 0.74f
                    ? 78f * Mathf.Pow(progress / 0.74f, 1.8f)
                    : Mathf.Lerp(78f, 84f, (progress - 0.74f) / 0.26f)
                        - 6f * Mathf.Sin(Mathf.PI * (progress - 0.74f) / 0.26f);
                SetAngle(angle, hinge);
                yield return null;
            }
            SetAngle(84f, hinge);
        }

        private void SetAngle(float degrees, Vector3 hinge)
        {
            Quaternion rotation = Quaternion.AngleAxis(-degrees, Vector3.forward);
            ceramic.SetLocalPositionAndRotation(hinge - rotation * hinge, rotation);
        }
    }
}
