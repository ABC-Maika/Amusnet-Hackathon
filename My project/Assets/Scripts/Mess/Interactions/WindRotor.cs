using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class WindRotor : ChainMechanism
    {
        [SerializeField] private Transform rotor;
        [SerializeField] private Transform[] seeds;
        [SerializeField] private Interactable[] targets;
        private float angle;

        private void Update()
        {
            if (HasInteracted) return;
            angle += (16f + 6f * Mathf.Sin(Time.time * 2f + transform.position.x)) * Time.deltaTime;
            rotor.localRotation = Quaternion.Euler(0,0,angle);
        }
        protected override void OnInteracted(MessManager manager) => StartCoroutine(Blow(manager));
        private IEnumerator Blow(MessManager manager)
        {
            int sent = 0;
            for (float t = 0f; t < 2.8f; t += Time.deltaTime)
            {
                angle += Mathf.Lerp(700f, 18f, t / 2.8f) * Time.deltaTime;
                rotor.localRotation = Quaternion.Euler(0,0,angle);
                if (sent < targets.Length && t >= .23f + sent * .24f)
                {
                    StartCoroutine(Carry(seeds[sent],targets[sent],manager,.57f,.42f,1.45f,false));
                    sent++;
                }
                yield return null;
            }
        }
    }
}
