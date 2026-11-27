using UnityEngine;

namespace Amusnet.World
{
    public sealed class WindZoneVolume : MonoBehaviour
    {
        [Min(0.1f)] public float radius = 5f;
        public Vector3 velocity = Vector3.up * 4f;
        public bool gusts;

        public Vector3 Sample(Vector3 position, float seconds)
        {
            float weight = Mathf.Clamp01(1f - Vector3.Distance(position, transform.position) / Mathf.Max(radius, 0.1f));
            float pulse = gusts ? Mathf.SmoothStep(0f, 1f, 0.5f + 0.5f * Mathf.Sin(seconds * 1.8f)) : 1f;
            return velocity * weight * pulse;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawRay(transform.position, velocity);
        }
    }
}
