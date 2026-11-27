using UnityEngine;
using UnityEngine.Rendering;

namespace Amusnet.World
{
    public sealed class WeatherSystem : MonoBehaviour
    {
        public Light sun;
        public ParticleSystem rain;
        [Min(1)] public float dayLength = 120f;
        [Min(1)] public float weatherPeriod = 42f;
        [Range(0,1)] public float startDay = 0.25f;
        private float elapsed;
        public float Daylight { get; private set; }
        public float Rain { get; private set; }
        public float LiftMultiplier => Mathf.Lerp(1f, 0.60f, Rain);
        public float RainSink => Rain * 0.8f;

        void Update()
        {
            elapsed += Time.deltaTime;
            float angle = 360f * (startDay + elapsed / Mathf.Max(1f, dayLength));
            Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.08f, 0.5f, Mathf.Sin(angle * Mathf.Deg2Rad)));
            Rain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, -Mathf.Cos(elapsed * Mathf.PI * 2f / Mathf.Max(1f, weatherPeriod))));
            sun.transform.rotation = Quaternion.Euler(angle, -32f, 0f);
            sun.intensity = Mathf.Lerp(0.03f, 1.5f, Daylight) * (1f - Rain * 0.3f);
            Color sky = Color.Lerp(new Color(0.06f,0.09f,0.16f), new Color(0.68f,0.82f,0.88f), Daylight);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = sky;
            RenderSettings.fogDensity = Mathf.Lerp(0.047f, 0.012f, Daylight) + Rain * 0.012f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.18f,0.21f,0.32f), Color.white * 0.8f, Daylight);
            if (rain) { var emission = rain.emission; emission.rateOverTime = Rain * 850f; }
        }
    }
}
