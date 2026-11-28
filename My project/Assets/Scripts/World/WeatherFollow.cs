using UnityEngine;

namespace Amusnet.World
{
    // Keeps the rain emitter above the camera (or target) so a large map needs only one small emitter.
    public sealed class WeatherFollow : MonoBehaviour
    {
        public Transform rain;
        public Transform target;
        public float height = 14f;

        void LateUpdate()
        {
            if (!rain) return;
            Transform t = target ? target : (Camera.main ? Camera.main.transform : null);
            if (t) rain.position = t.position + Vector3.up * height;
        }
    }
}
