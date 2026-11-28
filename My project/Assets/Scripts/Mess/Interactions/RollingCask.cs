using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class RollingCask : Interactable
    {
        [SerializeField] private Transform drum, junction;
        [SerializeField] private OpenGate gate;
        [SerializeField] private Interactable bells, cans;
        [SerializeField] private Terrain terrain;
        [SerializeField] private Transform wedge;
        private static readonly HashSet<RollingCask> rolling = new();
        public static IEnumerable<RollingCask> InMotion => rolling;
        public Transform Drum => drum;
        public bool IsRolling => rolling.Contains(this);
        public bool CanFlick { get; private set; }
        private bool flicked;
        private float flickTime;
        [SerializeField] private TrailRenderer slipstream;

        private void OnDisable() => rolling.Remove(this);
        public bool Flick()
        {
            if (!CanFlick) return false;
            flicked = true;
            flickTime = Time.time;
            slipstream.emitting = true;
            CanFlick = false;
            return true;
        }

        private void OnTriggerEnter(Collider other) => TryInteractWith(other);
        protected override void OnInteracted(MessManager manager) => StartCoroutine(Run(manager));

        private IEnumerator Run(MessManager manager)
        {
            rolling.Add(this);
            CanFlick = true;
            Vector3 wedgeStart = wedge.localPosition;
            Vector3 start = drum.position;
            Vector3 fork = junction.position;
            float turn = 0f;
            Quaternion rest = drum.localRotation;
            Vector3 previous = start;
            Interactable target = null;
            Vector3 end = fork;
            for (int leg = 0; leg < 3; leg++)
            {
                if (leg == 1)
                {
                    target = gate.HasInteracted ? cans : bells;
                    start = fork;
                    end = target.transform.position;
                    end -= Vector3.ProjectOnPlane(end - fork, Vector3.up).normalized * .33f;
                }
                if (leg == 2)
                {
                    CanFlick = false;
                    target.TryInteract(manager, this);
                    if (!flicked) break;
                    start = end;
                    target = target == bells ? cans : bells;
                    end = target.transform.position;
                    end -= Vector3.ProjectOnPlane(end - start, Vector3.up).normalized * .33f;
                }
                float duration = leg == 0 ? 1.15f : Vector3.Distance(start, end) / 3.6f;
                Vector3 direction = Vector3.ProjectOnPlane(end - start, Vector3.up).normalized;
                Quaternion facing = Quaternion.LookRotation(direction);
                for (float t = 0f; ; t += Time.deltaTime)
                {
                    float u = Mathf.Clamp01(t / duration);
                    Vector3 p = Vector3.Lerp(start, end, leg == 0 ? u * (.35f + .65f * u) : u);
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y + .47f;
                    if (leg == 0)
                    {
                        float kick = Mathf.Clamp01(u * 3f);
                        wedge.localPosition = wedgeStart + new Vector3(.47f * kick, .21f * Mathf.Sin(kick * Mathf.PI), .35f * kick);
                        wedge.localRotation = Quaternion.Euler(-31f * kick, 22f * kick, 0f);
                    }
                    if (flicked && Time.time - flickTime < .45f)
                        p.y += .5f * Mathf.Sin((Time.time - flickTime) / .45f * Mathf.PI);
                    if (leg == 2) p.y += .82f * Mathf.Sin(u * Mathf.PI);
                    turn += Vector3.ProjectOnPlane(p - previous, Vector3.up).magnitude * Mathf.Rad2Deg / .47f;
                    previous = p;
                    drum.SetPositionAndRotation(p, facing * Quaternion.Euler(turn, 0, 2f * Mathf.Sin(turn * Mathf.Deg2Rad)) * rest);
                    if (u >= 1f) break;
                    yield return null;
                }
            }
            if (flicked) target.TryInteract(manager, this);
            slipstream.emitting = false;
            rolling.Remove(this);
        }
    }
}
