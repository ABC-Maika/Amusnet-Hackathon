using Amusnet.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Moves story 1 (goat and the laundry) from ButterflyPreview into the open Village scene and rewires it to the Village's own butterfly, wind and weather.
public static class VillageStoryImport
{
    const string PreviewPath = "Assets/Scenes/Old_Scenes/ButterflyPreview.unity";
    const string RootName = "Goat and Laundry Preview";

    [MenuItem("Tools/Village/Import Story 1 (Goat And Laundry)")]
    public static void Import()
    {
        var village = SceneManager.GetActiveScene();
        var old = GameObject.Find(RootName);
        if (old && old.scene == village) Object.DestroyImmediate(old);

        var preview = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Additive);
        foreach (var root in preview.GetRootGameObjects())
            if (root.name == RootName) SceneManager.MoveGameObjectToScene(root, village);
        EditorSceneManager.CloseScene(preview, true);
        SceneManager.SetActiveScene(village);

        var story = GameObject.Find(RootName);
        var goat = story.GetComponent<GoatLaundryPreview>();
        var gate = story.GetComponentInChildren<LaundryGatePreview>(true);

        // Village systems
        var weatherGo = GameObject.Find("Weather and wind");
        var weather = weatherGo.GetComponent<WeatherSystem>();
        var wind = weatherGo.GetComponent<WindSystem>();
        var rain = weatherGo.GetComponentInChildren<ParticleSystem>(true);
        ParticleSystem pollen = null;
        var player = GameObject.Find("Butterfly Player");
        if (player)
            foreach (var ps in player.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.name == "Pollen Trail") pollen = ps;

        // Drop the preview's own WeatherSystem so it does not fight the Village day/night.
        var dup = gate.GetComponent<WeatherSystem>();
        if (dup && dup != weather) Object.DestroyImmediate(dup);

        var so = new SerializedObject(goat);
        so.FindProperty("pollen").objectReferenceValue = pollen;
        so.FindProperty("wind").objectReferenceValue = wind;
        so.ApplyModifiedPropertiesWithoutUndo();

        so = new SerializedObject(gate);
        so.FindProperty("weather").objectReferenceValue = weather;
        var rainProp = so.FindProperty("rain");
        if (rainProp.objectReferenceValue is ParticleSystem) rainProp.objectReferenceValue = rain;
        else if (rainProp.objectReferenceValue is Transform) rainProp.objectReferenceValue = rain.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Place near the butterfly spawn, on the terrain.
        var spawn = GameObject.Find("PlayerSpawn");
        var terrain = Terrain.activeTerrain;
        if (spawn && terrain)
        {
            var p = spawn.transform.position + spawn.transform.forward * 18f;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            story.transform.position = p;
            story.transform.rotation = Quaternion.Euler(0, spawn.transform.eulerAngles.y + 180f, 0);
        }
        EditorSceneManager.MarkSceneDirty(village);
    }
}
