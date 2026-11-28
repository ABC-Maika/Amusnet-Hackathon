using UnityEngine;

namespace Amusnet.Mess
{
    // Sampled centreline of the creek (z for each x), baked by the Boat Drift builder from the terrain.
    // Boats follow it downstream and fish patrol along it.
    public sealed class RiverLane : MonoBehaviour
    {
        [SerializeField] private float startX = -44f;
        [SerializeField] private float step = 2f;
        [SerializeField] private float[] z = new float[0];

        private static RiverLane instance;
        private void Awake() => instance = this;
        private void OnDestroy() { if (instance == this) instance = null; }

        public static float ZAt(float x, float fallback)
        {
            if (!instance) instance = FindAnyObjectByType<RiverLane>();
            if (!instance || instance.z.Length < 2) return fallback;
            float f = Mathf.Clamp((x - instance.startX) / instance.step, 0f, instance.z.Length - 1.001f);
            int i = (int)f;
            return Mathf.Lerp(instance.z[i], instance.z[i + 1], f - i);
        }
    }
}
