using System.Collections.Generic;
using UnityEngine;

namespace Amusnet.Mess
{
    // Marks a house, tree or bridge that TNT can wreck. Blast() collapses everything whose bounds reach the blast radius.
    public sealed class Destructible : MonoBehaviour
    {
        public enum Kind { House, Tree, Bridge }

        [SerializeField] private Kind kind = Kind.House;

        private static readonly List<Destructible> all = new List<Destructible>();
        public static event System.Action<Destructible> Collapsed;

        private Bounds bounds;
        private Renderer[] renderers;
        private Collider[] colliders;
        private Vector3 startPos;
        private Quaternion startRot;
        private Vector3 fallAxis;
        private float t;

        public Kind Type => kind;
        public bool IsDestroyed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { all.Clear(); Collapsed = null; }

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            startPos = transform.position;
            startRot = transform.rotation;
            all.Add(this);
            enabled = false; // only ticks while collapsing
        }

        private void OnDestroy() => all.Remove(this);

        public static void Blast(Vector3 origin, float radius, float power)
        {
            foreach (var d in all.ToArray())
            {
                if (!d || d.IsDestroyed) continue;
                // Big things (bridges, houses) feel the blast across their whole footprint.
                if ((d.bounds.ClosestPoint(origin) - origin).sqrMagnitude <= radius * radius) d.Collapse(origin, radius, power);
            }
        }

        private void Collapse(Vector3 origin, float radius, float power)
        {
            IsDestroyed = true;
            enabled = true;
            t = 0f;
            foreach (var c in colliders) if (c) c.enabled = false;
            var away = transform.position - origin; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.right;
            fallAxis = Vector3.Cross(Vector3.up, away.normalized);
            Material mat = null;
            foreach (var r in renderers) if (r && r.sharedMaterial) { mat = r.sharedMaterial; break; }
            int n = kind == Kind.Tree ? 4 : kind == Kind.Bridge ? 14 : 16;
            BlastFx.Debris(bounds, mat, n, origin, power * (kind == Kind.Tree ? 0.5f : 1f), kind == Kind.Tree ? 10f : 30f);
            Collapsed?.Invoke(this);
        }

        private void Update()
        {
            t += Time.deltaTime;
            float h = Mathf.Max(1f, bounds.size.y);
            switch (kind)
            {
                case Kind.Tree:
                    // Topples away from the blast.
                    transform.rotation = Quaternion.AngleAxis(Mathf.Clamp01(t / 1.1f) * Mathf.Clamp01(t / 1.1f) * 86f, fallAxis) * startRot;
                    if (t > 6f) { foreach (var r in renderers) if (r) r.enabled = false; enabled = false; }
                    break;
                case Kind.Bridge:
                    transform.rotation = Quaternion.AngleAxis(Mathf.Min(t, 2f) * 5f, transform.right) * startRot;
                    transform.position = startPos + Vector3.down * (Mathf.Clamp01((t - 0.3f) / 2.5f) * 3f) + Random.insideUnitSphere * (t < 0.6f ? 0.05f : 0f);
                    if (t > 3f) { foreach (var r in renderers) if (r) r.enabled = false; enabled = false; }
                    break;
                default:
                    // Houses shudder, then slump into the ground.
                    float sink = Mathf.Clamp01((t - 0.35f) / 1.6f);
                    transform.position = startPos + Vector3.down * (sink * sink * h * 0.95f) + (t < 0.5f ? Random.insideUnitSphere * 0.08f : Vector3.zero);
                    transform.rotation = Quaternion.AngleAxis(sink * 7f, fallAxis) * startRot;
                    if (t > 2.2f) { foreach (var r in renderers) if (r) r.enabled = false; enabled = false; }
                    break;
            }
        }
    }
}
