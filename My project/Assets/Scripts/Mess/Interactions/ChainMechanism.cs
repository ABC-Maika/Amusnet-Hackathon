using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public abstract class ChainMechanism : Interactable
    {
        protected void OnTriggerEnter(Collider other) => TryInteractWith(other);

        // The prop scores on arrival, so a player can reach it before the payload does.
        protected IEnumerator Carry(Transform payload, Interactable target, MessManager manager,
            float duration, float arc, float contactHeight, bool keep, bool spin = true)
        {
            Vector3 start = payload.position;
            Quaternion rest = payload.rotation;
            payload.gameObject.SetActive(true);
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / duration);
                Vector3 end = target.transform.position + Vector3.up * contactHeight;
                Vector3 p = Vector3.Lerp(start, end, u) + Vector3.up * (4f * arc * u * (1f - u));
                payload.position = p;
                if (spin) payload.rotation = Quaternion.AngleAxis(u * 295f, transform.right) * rest;
                if (u >= 1f) break;
                yield return null;
            }
            target.TryInteract(manager, this);
            if (!keep) payload.gameObject.SetActive(false);
        }
    }
}
