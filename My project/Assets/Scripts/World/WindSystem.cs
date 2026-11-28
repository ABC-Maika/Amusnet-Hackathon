using UnityEngine;

namespace Amusnet.World
{
    public sealed class WindSystem : MonoBehaviour
    {
        [Min(0)] public float prevailingSpeed = 1.1f;
        [Min(1)] public float directionPeriod = 42f;
        public WindZoneVolume[] zones = {};

        // World-space velocity in metres per second, including overlapping volumes.
        public Vector3 Sample(Vector3 position, float seconds)
        {
            float phase = seconds * 2f * Mathf.PI / Mathf.Max(1f, directionPeriod);
            Vector3 velocity = Quaternion.Euler(0, 45f + 65f * Mathf.Sin(phase), 0) *
                Vector3.forward * (prevailingSpeed * (1f + 0.35f * Mathf.Sin(phase * 1.6f)));
            foreach (var zone in zones)
                if (zone && zone.isActiveAndEnabled) velocity += zone.Sample(position, seconds);
            return velocity;
        }
    }
}
