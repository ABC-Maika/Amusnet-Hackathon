using System.Collections.Generic;
using UnityEngine;

namespace Amusnet.Mess
{
    // Code-built explosion effects (no prefabs): fireball, smoke, sparks, flash light, and physics debris chunks.
    public static class BlastFx
    {
        private static Material mat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => mat = null;

        private static Material Mat
        {
            get
            {
                if (mat) return mat;
                var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float d = Mathf.Clamp01(1f - new Vector2(x - 15.5f, y - 15.5f).magnitude / 16f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                    }
                tex.Apply();
                mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
                return mat;
            }
        }

        private static ParticleSystem Make(Transform parent, Vector3 pos, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat;
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            return ps;
        }

        private static Gradient Fade(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        public static void Boom(Vector3 pos, float radius)
        {
            var root = new GameObject("TNT Blast");
            root.transform.position = pos;
            Object.Destroy(root, 8f);

            var fire = Make(root.transform, pos, "Fireball");
            var m = fire.main; m.duration = 1f; m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f); m.startSize = new ParticleSystem.MinMaxCurve(radius * 0.45f, radius * 0.85f);
            m.gravityModifier = -0.15f;
            var e = fire.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
            var s = fire.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.8f;
            var c = fire.colorOverLifetime; c.enabled = true; c.color = Fade(new Color(1f, 0.85f, 0.35f), new Color(0.35f, 0.2f, 0.12f));
            var sz = fire.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));
            fire.Play();

            var smoke = Make(root.transform, pos, "Smoke");
            m = smoke.main; m.duration = 1f; m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f); m.startSize = new ParticleSystem.MinMaxCurve(radius * 0.5f, radius * 0.9f);
            m.gravityModifier = -0.05f; m.startColor = new Color(0.25f, 0.24f, 0.23f, 0.7f);
            e = smoke.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 28) });
            s = smoke.shape; s.shapeType = ParticleSystemShapeType.Hemisphere; s.radius = radius * 0.3f;
            c = smoke.colorOverLifetime; c.enabled = true; c.color = Fade(new Color(0.4f, 0.38f, 0.36f), new Color(0.2f, 0.2f, 0.2f));
            sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
            smoke.Play();

            var sparks = Make(root.transform, pos, "Sparks");
            m = sparks.main; m.duration = 1f; m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(10f, 26f); m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            m.gravityModifier = 1.2f; m.startColor = new Color(1f, 0.8f, 0.3f);
            e = sparks.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
            s = sparks.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.5f;
            c = sparks.colorOverLifetime; c.enabled = true; c.color = Fade(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.3f, 0.1f));
            sparks.Play();

            var flash = new GameObject("Flash").AddComponent<Light>();
            flash.transform.SetParent(root.transform, false);
            flash.transform.position = pos + Vector3.up;
            flash.color = new Color(1f, 0.7f, 0.35f); flash.range = radius * 2.5f; flash.intensity = 60f;
            root.AddComponent<FlashFade>().Init(flash);
        }

        // Looping sparks for a lit fuse.
        public static GameObject FuseSparks(Transform parent, Vector3 localPos)
        {
            var ps = Make(parent, parent.TransformPoint(localPos), "Fuse Sparks");
            var m = ps.main; m.loop = true; m.startLifetime = 0.5f; m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f); m.gravityModifier = 0.8f; m.startColor = new Color(1f, 0.8f, 0.3f);
            var e = ps.emission; e.rateOverTime = 60f;
            var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.05f;
            ps.Play();
            return ps.gameObject;
        }

        // Physics chunks inside `b` thrown away from `origin`. They fade out after `life` seconds.
        public static void Debris(Bounds b, Material material, int count, Vector3 origin, float power, float life)
        {
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Debris";
                go.transform.position = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
                go.transform.rotation = Random.rotation;
                go.transform.localScale = new Vector3(Random.Range(0.15f, 0.55f), Random.Range(0.1f, 0.4f), Random.Range(0.15f, 0.9f));
                if (material) go.GetComponent<Renderer>().sharedMaterial = material;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.AddExplosionForce(power, origin + Vector3.down * 0.5f, 25f, 1.2f, ForceMode.Impulse);
                go.AddComponent<FadeOut>().Init(life + Random.Range(0f, 4f));
            }
        }

        private sealed class FlashFade : MonoBehaviour
        {
            private Light l; private float t;
            public void Init(Light light) { l = light; }
            private void Update() { t += Time.deltaTime; if (l) l.intensity = Mathf.Max(0f, 60f * (1f - t / 0.7f)); }
        }

        private sealed class FadeOut : MonoBehaviour
        {
            private float life, t; private Vector3 scale;
            public void Init(float seconds) { life = seconds; scale = transform.localScale; }
            private void Update()
            {
                t += Time.deltaTime;
                if (t > life) transform.localScale = scale * Mathf.Clamp01(1f - (t - life));
                if (t > life + 1f) Destroy(gameObject);
            }
        }
    }

    // Short on-screen messages ("Bridge destroyed!", "Rescued!") shared by the village events.
    public sealed class HudNotice : MonoBehaviour
    {
        private static HudNotice instance;
        private readonly List<(string text, float until)> lines = new List<(string, float)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public static void Show(string text, float seconds = 3.5f)
        {
            if (!instance)
            {
                instance = new GameObject("Hud Notice").AddComponent<HudNotice>();
                DontDestroyOnLoad(instance.gameObject);
            }
            instance.lines.Add((text, Time.unscaledTime + seconds));
            if (instance.lines.Count > 4) instance.lines.RemoveAt(0);
        }

        private void OnGUI()
        {
            lines.RemoveAll(l => l.until < Time.unscaledTime);
            float k = Screen.height / 1080f;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * k), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            for (int i = 0; i < lines.Count; i++)
            {
                var r = new Rect(0f, Screen.height * 0.12f + i * 44f * k, Screen.width, 44f * k);
                style.normal.textColor = Color.black;
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), lines[i].text, style);
                style.normal.textColor = new Color(1f, 0.92f, 0.6f);
                GUI.Label(r, lines[i].text, style);
            }
        }
    }
}
