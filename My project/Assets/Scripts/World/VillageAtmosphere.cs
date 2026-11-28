using UnityEngine;

namespace Amusnet.World
{
    // Runs after WeatherSystem each frame: brighter sky, gradient ambient and lighter fog, still driven by Daylight/Rain.
    // At night the directional light becomes a blue moon so colours stay readable instead of going grey.
    [DefaultExecutionOrder(100)]
    public sealed class VillageAtmosphere : MonoBehaviour
    {
        public WeatherSystem weather;
        public Material skybox;
        public Color sunColorDay = new Color(1f, 0.94f, 0.82f);
        public Color sunColorLow = new Color(1f, 0.55f, 0.3f);
        public Color moonColor = new Color(0.62f, 0.72f, 1f);
        public float moonIntensity = 0.55f;
        public float dayFogDensity = 0.0032f;
        public float nightFogDensity = 0.0045f;

        void LateUpdate()
        {
            if (!weather) return;
            float d = weather.Daylight, r = weather.Rain;
            if (skybox)
            {
                RenderSettings.skybox = skybox;
                skybox.SetFloat("_Exposure", Mathf.Lerp(0.32f, 1.25f, d) * (1f - r * 0.35f));
                skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.6f, 1.1f, d) + r * 0.6f);
            }

            var sun = weather.sun;
            if (sun)
            {
                sun.shadows = LightShadows.Soft;
                float elevation = Vector3.Dot(-sun.transform.forward, Vector3.up);
                if (elevation >= 0f)
                {
                    sun.color = Color.Lerp(sunColorLow, sunColorDay, Mathf.Clamp01(elevation * 2.2f));
                }
                else
                {
                    // Sun is below the horizon: flip the light to the opposite side of the sky as the moon.
                    sun.transform.rotation = Quaternion.LookRotation(-sun.transform.forward, Vector3.up);
                    sun.color = moonColor;
                    sun.intensity = moonIntensity * Mathf.SmoothStep(0f, 1f, -elevation * 5f) * (1f - r * 0.4f);
                }
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.24f, 0.3f, 0.5f), new Color(0.62f, 0.78f, 1f), d);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.2f, 0.24f, 0.36f), new Color(0.78f, 0.8f, 0.76f), d);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.12f, 0.13f, 0.17f), new Color(0.42f, 0.4f, 0.32f), d);
            var dayFog = Color.Lerp(new Color(0.72f, 0.84f, 0.95f), new Color(0.6f, 0.65f, 0.7f), r);
            RenderSettings.fogColor = Color.Lerp(new Color(0.09f, 0.12f, 0.24f), dayFog, d);
            RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, d) + r * 0.006f;
        }
    }
}
