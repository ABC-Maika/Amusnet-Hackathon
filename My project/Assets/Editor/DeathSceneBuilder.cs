using Butterfly.Death;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Death > Populate Test Scene. Adds a Terrain, the Butterfly prefab (with collider + flyer),
/// simulated mess props, a DeathManager and a debug killer to the OPEN scene, then saves it.
/// </summary>
public static class DeathSceneBuilder
{
    const string ButterflyPrefab = "Assets/Prefabs/Butterfly.prefab";
    const string TerrainAsset = "Assets/Scenes/Death Test Terrain.asset";

    [MenuItem("Death/Populate Test Scene")]
    static void Populate()
    {
        if (Object.FindFirstObjectByType<Terrain>() != null)
        {
            Debug.LogWarning("[Death] Scene already has a Terrain, nothing added.");
            return;
        }

        // Terrain (saved as an asset so it survives scene reloads).
        var data = new TerrainData { heightmapResolution = 129, size = new Vector3(200f, 20f, 200f) };
        var heights = new float[129, 129];
        for (int y = 0; y < 129; y++)
            for (int x = 0; x < 129; x++)
                heights[y, x] = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.25f;
        data.SetHeights(0, 0, heights);
        AssetDatabase.CreateAsset(data, TerrainAsset);

        var terrain = Terrain.CreateTerrainGameObject(data);
        terrain.name = "Terrain";
        terrain.transform.position = new Vector3(-100f, 0f, -100f);
        Undo.RegisterCreatedObjectUndo(terrain, "Add terrain");

        // Butterfly
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButterflyPrefab);
        if (prefab == null) { Debug.LogError("[Death] Missing " + ButterflyPrefab); return; }
        var butterfly = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        butterfly.transform.position = new Vector3(0f, 25f, 0f);
        var col = butterfly.AddComponent<SphereCollider>();
        col.radius = 0.3f;
        var rb = butterfly.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        butterfly.AddComponent<SimFlyer>();
        Undo.RegisterCreatedObjectUndo(butterfly, "Add butterfly");

        // Camera + light
        var cam = Camera.main;
        if (cam == null)
        {
            var c = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = c.AddComponent<Camera>();
            c.AddComponent<AudioListener>();
        }
        var chase = cam.gameObject.GetComponent<SimChaseCamera>() ?? cam.gameObject.AddComponent<SimChaseCamera>();
        chase.target = butterfly.transform;
        cam.transform.position = new Vector3(0f, 28f, -7f);

        if (Object.FindFirstObjectByType<Light>() == null)
        {
            var l = new GameObject("Sun").AddComponent<Light>();
            l.type = LightType.Directional;
            l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Death rig
        var rig = new GameObject("DeathRig");
        var mgr = rig.AddComponent<DeathManager>();
        mgr.chainRadius = 12f;
        var killer = rig.AddComponent<DebugDeathCause>();
        killer.displayName = "Debug";
        killer.butterfly = butterfly;

        // Simulated mess props
        var props = new GameObject("SimMessProps");
        var rng = new System.Random(7);
        for (int i = 0; i < 60; i++)
        {
            var p = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            p.name = "SimMessProp";
            p.transform.SetParent(props.transform);
            p.transform.position = new Vector3(R(rng, -40, 40), 1.5f, R(rng, -40, 40));
            p.transform.localScale = Vector3.one * R(rng, 0.6f, 1.6f);
            p.AddComponent<Rigidbody>();
            p.AddComponent<SimMessProp>();
        }

        EditorSceneManager.MarkSceneDirty(terrain.scene);
        EditorSceneManager.SaveScene(terrain.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Death] Test scene populated and saved. Press Play, fly, press K to die.");
    }

    [MenuItem("Death/Add Sim Animals, Frog and Spirit Trail")]
    static void AddAnimalsAndFrog()
    {
        var root = new GameObject("SimAnimalsAndKillers");
        var rng = new System.Random(11);
        for (int i = 0; i < 5; i++)
        {
            var a = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            a.name = "SimAnimal";
            a.transform.SetParent(root.transform);
            a.transform.position = new Vector3(R(rng, -25, 25), 1f, R(rng, -25, 25));
            a.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            var ra = a.AddComponent<Butterfly.Animals.RebelAnimal>();
            ra.animalName = new[] { "Chicken", "Dog", "Horse", "Cow", "Duck" }[i];
        }

        PlaceFrog(root.scene);

        if (Object.FindFirstObjectByType<Butterfly.Animals.SpiritTrail>() == null)
            new GameObject("SpiritTrail").AddComponent<Butterfly.Animals.SpiritTrail>();

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Debug.Log("[Death] Added the sim animals, pond frog and spirit trail.");
    }

    internal static GameObject PlaceFrog(UnityEngine.SceneManagement.Scene scene)
    {
        const string path = "Assets/Prefabs/Frog.prefab";
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var existing in root.GetComponentsInChildren<FrogDeath>(true))
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing.gameObject) == path)
                    return existing.gameObject;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var frog = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(frog, "Place pond frog");
        var position = new Vector3(0f, 1f, 18f);
        foreach (var terrain in Terrain.activeTerrains)
        {
            if (terrain.gameObject.scene != scene) continue;
            var local = position - terrain.transform.position;
            var size = terrain.terrainData.size;
            if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            break;
        }
        frog.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 180, 0));
        PrefabUtility.RecordPrefabInstancePropertyModifications(frog.transform);
        var killer = Undo.AddComponent<FrogDeath>(frog);
        killer.animator = frog.GetComponent<Animator>();
        EditorUtility.SetDirty(killer);
        return frog;
    }

    static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
}
