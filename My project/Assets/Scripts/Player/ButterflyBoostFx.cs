using UnityEngine;

/// <summary>
/// Speed effects built in code (no prefab): wind streaks that rush past when flying fast, and glowing ribbons off both
/// wing tips while boosting with Shift. Added to the butterfly automatically by ButterflyController.
/// </summary>
public sealed class ButterflyBoostFx : MonoBehaviour
{
    [SerializeField] private float streakStartSpeed = 1.05f; // fraction of flight speed where wind starts to show
    [SerializeField] private int maxStreaksPerSecond = 45;

    private ButterflyController butterfly;
    private Transform visual;
    private ParticleSystem streaks;
    private ParticleSystemRenderer streaksRenderer;
    private TrailRenderer[] ribbons;
    private float boostBlend;
    private float k = 1f; // butterfly model scale: ribbon width and streak size follow it
    private static Material glow, ribbonMat;
    private static Texture2D glowTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { glow = null; glowTex = null; ribbonMat = null; }

    private static Material Glow
    {
        get
        {
            if (glow) return glow;
            glowTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Mathf.Clamp01(1f - new Vector2(x - 15.5f, y - 15.5f).magnitude / 16f);
                    glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, d));
                }
            glowTex.Apply();
            glow = new Material(Shader.Find("Sprites/Default")) { mainTexture = glowTex };
            return glow;
        }
    }

    // Ribbon texture: soft edges across the width, solid along the length.
    private static Material Ribbon
    {
        get
        {
            if (ribbonMat) return ribbonMat;
            var tex = new Texture2D(4, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
            {
                float a = 1f - Mathf.Abs(y - 15.5f) / 16f;
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a * 1.6f)));
            }
            tex.Apply();
            ribbonMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            return ribbonMat;
        }
    }

    public void Init(ButterflyController owner, Transform visualRoot)
    {
        butterfly = owner;
        visual = visualRoot != null ? visualRoot : transform;
        Build();
    }

    private void Build()
    {
        if (streaks != null) return;

        k = Mathf.Max(0.05f, visual.lossyScale.x);
        float sizeK = Mathf.Max(k, 0.35f);
        var go = new GameObject("Wind Streaks"); // not parented: the butterfly's small scale must not shrink the effect
        streaks = go.AddComponent<ParticleSystem>();
        streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        streaksRenderer = go.GetComponent<ParticleSystemRenderer>();
        streaksRenderer.sharedMaterial = Glow;
        streaksRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        streaksRenderer.lengthScale = 3f;
        streaksRenderer.velocityScale = 0.06f;

        var main = streaks.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f * sizeK, 0.06f * sizeK);
        main.startSpeed = 0f;
        main.maxParticles = 300;
        main.startColor = Color.white;

        var emission = streaks.emission;
        emission.rateOverTime = 0f;

        var shape = streaks.shape;
        // A thin outer shell of a sphere: wind shows up around the edges of the view, never as a cloud on the butterfly.
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 4f;
        shape.radiusThickness = 0.15f;
        shape.scale = new Vector3(1f, 0.6f, 1f);

        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.4f, 0.3f), new GradientAlphaKey(0f, 1f) });
        var col = streaks.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        // Wing-tip ribbons.
        ribbons = new TrailRenderer[2];
        float half = WingHalfSpan();
        for (int i = 0; i < 2; i++)
        {
            var rg = new GameObject(i == 0 ? "Boost Ribbon L" : "Boost Ribbon R");
            rg.transform.SetParent(visual, false);
            rg.transform.localPosition = new Vector3(i == 0 ? -half : half, 0f, -0.05f);
            var tr = rg.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Ribbon;
            tr.time = 0.6f;
            tr.minVertexDistance = Mathf.Max(0.01f, 0.04f * k);
            tr.alignment = LineAlignment.View;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.22f), new Keyframe(0.5f, 0.13f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(0.55f, 0.85f, 1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.emitting = false;
            ribbons[i] = tr;
        }
    }

    // Roughly where the wing tips are, measured from the model so it works with any butterfly mesh.
    private float WingHalfSpan()
    {
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0.4f;
        Bounds b = default;
        bool any = false;
        foreach (var r in rs)
        {
            if (r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return 0.4f;
        return Mathf.Clamp(b.extents.x / Mathf.Max(0.0001f, visual.lossyScale.x) * 0.9f, 0.1f, 3f);
    }

    private void LateUpdate()
    {
        if (butterfly == null || streaks == null) return;

        float speed01 = butterfly.Speed01;
        bool boosting = butterfly.IsBoosting;
        boostBlend = Mathf.MoveTowards(boostBlend, boosting ? 1f : 0f, Time.deltaTime * (boosting ? 4f : 2.5f));

        float wind = Mathf.Clamp01((speed01 - streakStartSpeed) / (1.6f - streakStartSpeed));
        Vector3 vel = butterfly.GetComponent<CharacterController>().velocity;
        float mag = vel.magnitude;

        var emission = streaks.emission;
        emission.rateOverTime = mag > 0.5f ? Mathf.Lerp(0f, maxStreaksPerSecond, Mathf.Max(wind, boostBlend)) : 0f;

        if (mag > 0.5f)
        {
            // Streaks are born ahead of and around the butterfly and stream backwards past it.
            Vector3 dir = vel / mag;
            var t = streaks.transform;
            t.position = transform.position + dir * 2f;
            t.rotation = Quaternion.LookRotation(dir);
            var main = streaks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(-mag * 0.5f, -mag * 0.25f);
            streaksRenderer.lengthScale = Mathf.Lerp(3f, 9f, Mathf.Max(wind, boostBlend));
            if (!streaks.isPlaying) streaks.Play();
        }

        foreach (var tr in ribbons)
        {
            if (tr == null) continue;
            tr.emitting = boosting;
            tr.widthMultiplier = k * Mathf.Lerp(0.6f, 1.2f, boostBlend);
        }
    }

    private void OnDestroy() { if (streaks) Destroy(streaks.gameObject); }

    private void OnDisable()
    {
        if (ribbons != null)
            foreach (var tr in ribbons) if (tr) { tr.emitting = false; tr.Clear(); }
        if (streaks != null) streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
