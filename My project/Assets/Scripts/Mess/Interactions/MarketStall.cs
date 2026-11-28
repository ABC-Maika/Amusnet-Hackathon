using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class MarketStall : Interactable
    {
        [SerializeField] private Transform canopy, tray;
        [SerializeField] private Transform[] goods;
        [SerializeField] private Interactable barrel, chicken;
        [SerializeField] private Terrain terrain;
        private Quaternion canopyRest, trayRest;
        private float phase;

        private void Awake()
        {
            canopyRest = canopy.localRotation;
            trayRest = tray.localRotation;
            phase = transform.position.x * .33f;
        }

        private void Update()
        {
            if (!HasInteracted)
                canopy.localRotation = canopyRest * Quaternion.Euler(2.4f * Mathf.Sin(Time.time * 2.1f + phase), 0f, 0f);
        }

        private void OnTriggerEnter(Collider other) => TryInteractWith(other);
        protected override void OnInteracted(MessManager manager) => StartCoroutine(Spill(manager));

        private IEnumerator Spill(MessManager manager)
        {
            float t = 0f;
            while (t < .44f)
            {
                t += Time.deltaTime;
                float drop = Mathf.Clamp01(t / .48f);
                canopy.localRotation = canopyRest * Quaternion.Euler(-54f * drop * drop, 0f, -5f * drop);
                yield return null;
            }
            for (int i = 0; i < goods.Length; i++)
            {
                var target = i % 2 == 0 ? barrel : chicken;
                StartCoroutine(RollOff(goods[i], target, i, manager));
            }
            while (t < 1.6f)
            {
                t += Time.deltaTime;
                float drop = Mathf.Clamp01(t / .48f);
                float bounce = Mathf.Sin((t - .48f) * 17f) * Mathf.Exp(-5f * Mathf.Max(0f, t - .48f));
                canopy.localRotation = canopyRest * Quaternion.Euler(-54f * drop * drop - 9f * bounce, 0f, -5f * drop);
                tray.localRotation = trayRest * Quaternion.Euler(26f * Mathf.SmoothStep(0f, 1f, (t - .33f) / .35f), 0f, 0f);
                yield return null;
            }
        }

        private IEnumerator RollOff(Transform item, Interactable target, int index, MessManager manager)
        {
            yield return new WaitForSeconds(index * .054f);
            Vector3 start = item.position;
            Quaternion rest = item.rotation;
            Vector3 end = target.transform.position;
            Vector3 side = transform.right * (.22f * (index / 2 - 2));
            if (index > 1) end += side;
            float flight = .43f + index * .023f;
            float duration = flight + .66f + (index % 2) * .46f;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / duration);
                Vector3 p = Vector3.Lerp(start, end, u);
                float floor = Ground(terrain).SampleHeight(p) + Ground(terrain).transform.position.y + .16f;
                float fall = Mathf.Clamp01(t / flight);
                float bounce = Mathf.Abs(Mathf.Sin((t - flight) * 10f)) * .28f * Mathf.Exp(-3f * (t - flight));
                p.y = t < flight ? Mathf.Lerp(start.y, floor, fall * fall) : floor + bounce * (1f - u);
                item.SetPositionAndRotation(p, Quaternion.AngleAxis(u * (295f + index * 25f), transform.right) * rest);
                if (u >= 1f) break;
                yield return null;
            }
            target.TryInteract(manager, this);
        }
    }
}
