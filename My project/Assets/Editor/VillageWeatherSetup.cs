using Amusnet.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Adds day/night, wind and rain (existing Amusnet.World systems) to the open Village scene.
public static class VillageWeatherSetup
{
    [MenuItem("Tools/Village/Add Weather And Day Night")]
    public static void Build()
    {
        var old = GameObject.Find("Weather and wind");
        if (old) Object.DestroyImmediate(old);

        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (l.type == LightType.Directional) { sun = l; break; }
        RenderSettings.sun = sun;

        var root = new GameObject("Weather and wind");
        var rainGo = new GameObject("Rain");
        rainGo.transform.SetParent(root.transform, false);
        var ps = rainGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = 1.1f;
        main.startSpeed = 0f;
        main.startSize = 0.06f;
        main.startColor = new Color(0.75f, 0.85f, 1f, 0.55f);
        main.gravityModifier = 9f;
        main.maxParticles = 6000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission; emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 0.1f, 40f);

        var r = rainGo.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 6f; r.velocityScale = 0.04f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.color = new Color(0.8f, 0.9f, 1f, 0.6f);
        System.IO.Directory.CreateDirectory("Assets/Materials/Village");
        AssetDatabase.CreateAsset(mat, "Assets/Materials/Village/Rain Streak.mat");
        r.sharedMaterial = mat;

        var weather = root.AddComponent<WeatherSystem>();
        weather.sun = sun; weather.rain = ps;
        weather.dayLength = 180f; weather.weatherPeriod = 90f; weather.startDay = 0.3f;
        var wind = root.AddComponent<WindSystem>();
        wind.prevailingSpeed = 1.6f; wind.directionPeriod = 60f;
        var follow = root.AddComponent<WeatherFollow>();
        follow.rain = rainGo.transform; follow.height = 14f;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
