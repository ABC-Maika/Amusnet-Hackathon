using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class CiderPress : ChainMechanism
    {
        [SerializeField] private Transform screw, platen, juice, cork;
        [SerializeField] private Interactable bell;
        protected override void OnInteracted(MessManager manager) => StartCoroutine(Press(manager));
        private IEnumerator Press(MessManager manager)
        {
            Vector3 head = platen.localPosition;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / 1.34f);
                screw.localRotation = Quaternion.Euler(0f, 510f * u, 0f);
                platen.localPosition = head - Vector3.up * (.43f * Mathf.SmoothStep(0,1,u));
                juice.localScale = new Vector3(1, Mathf.Max(.015f, u), 1);
                if (u >= 1f) break;
                yield return null;
            }
            StartCoroutine(Carry(cork,bell,manager,.83f,1.9f,1.72f,true));
        }
    }
}
