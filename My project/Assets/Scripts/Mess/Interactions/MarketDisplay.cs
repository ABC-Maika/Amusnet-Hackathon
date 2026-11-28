using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class MarketDisplay : Interactable
    {
        [SerializeField] private bool hanging;
        [SerializeField] private Transform[] pieces;
        [SerializeField] private Terrain terrain;
        [SerializeField] private AudioClip peal;
        [SerializeField] private KnockOverPot nearbyPot;
        private float phase;
        private void Awake() => phase = transform.position.x * .37f;
        private void Update()
        {
            if (!hanging || HasInteracted) return;
            for (int i = 0; i < pieces.Length; i++)
                pieces[i].localRotation = Quaternion.Euler(3f * Mathf.Sin(Time.time * 1.7f + i + phase), 0, 0);
        }
        private void OnTriggerEnter(Collider other) => TryInteractWith(other);
        protected override void OnInteracted(MessManager manager)
        {
            for (int i = 0; i < pieces.Length; i++)
                StartCoroutine(hanging ? Ring(pieces[i], i) : Scatter(pieces[i], i, manager));
        }

        private IEnumerator Ring(Transform bell, int index)
        {
            yield return new WaitForSeconds(index * .14f);
            Sound(peal, .81f + .19f * index);
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                float swing = 67f * Mathf.Sin(t * (7.4f + index * .6f)) * Mathf.Exp(-t * .95f);
                bell.localRotation = Quaternion.Euler(swing, 0, swing * .16f);
                yield return null;
            }
            bell.localRotation = Quaternion.identity;
        }

        private IEnumerator Scatter(Transform jug, int index, MessManager manager)
        {
            yield return new WaitForSeconds(.07f * index);
            Vector3 start = jug.position;
            Quaternion rest = jug.rotation;
            Vector3 end = start + transform.TransformVector(new Vector3((index % 3 - 1) * (.42f + .12f * index), 0, .74f + .22f * index));
            if (nearbyPot && index == 0) end = nearbyPot.transform.position;
            end.y = terrain.SampleHeight(end) + terrain.transform.position.y + .17f;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / (1.02f + index * .06f));
                float fall = Mathf.Clamp01(u / .72f);
                float hop = (u < .72f ? Mathf.Sin(fall * Mathf.PI) * .63f : Mathf.Abs(Mathf.Sin((u - .72f) * 19f)) * (1f - u) * .6f);
                Vector3 p = Vector3.Lerp(start, end, u);
                p.y = Mathf.Lerp(start.y, end.y, fall * fall) + hop;
                jug.SetPositionAndRotation(p, rest * Quaternion.Euler(84f * u, 0f, (index % 3 - 1) * 38f * u));
                if (u >= 1f) break;
                yield return null;
            }
            if (nearbyPot && index == 0) nearbyPot.TryInteract(manager, this);
        }
    }
}
