using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class MillSluice : ChainMechanism
    {
        [SerializeField] private bool waterwheel;
        [SerializeField] private Transform moving, current;
        [SerializeField] private Interactable outlet;
        protected override void OnInteracted(MessManager manager) => StartCoroutine(Release(manager));
        private IEnumerator Release(MessManager manager)
        {
            Vector3 rest = moving.localPosition;
            bool sent = false;
            float duration = waterwheel ? 2.5f : 1.2f;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / duration);
                if (waterwheel) moving.localRotation = Quaternion.Euler(0,0,-450f * (1f - (1f-u)*(1f-u)));
                else moving.localPosition = rest + Vector3.up * (1.04f * Mathf.SmoothStep(0,1,Mathf.Min(1,u*2.1f)));
                if (!sent && u >= (waterwheel ? .36f : .29f))
                {
                    sent = true;
                    StartCoroutine(Carry(current,outlet,manager,waterwheel?.62f:.84f,waterwheel?.24f:-.12f,
                        waterwheel?.78f:.84f,false,false));
                }
                if (u >= 1f) break;
                yield return null;
            }
        }
    }
}
