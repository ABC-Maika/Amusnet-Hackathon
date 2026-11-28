using System;
using System.Collections.Generic;
using System.Linq;
using Amusnet.Mess;
using Amusnet.Playtest;
using Amusnet.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ComboTestSeeder
{
    private const string Source = "Assets/Scenes/Old_Scenes/Sprint3Village.unity";
    private const string Destination = "Assets/Scenes/Old_Scenes/Sprint3ComboTests.unity";
    private const string GroundData = "Assets/Scenes/Combo Test Ground.asset";

    private sealed class Recipe
    {
        public readonly string title, district, hint;
        public readonly string[] hits;
        public string[] alternatives = Array.Empty<string>();
        public float side;

        public Recipe(string title, string district, string hint, params string[] hits)
        {
            this.title = title;
            this.district = district;
            this.hint = hint;
            this.hits = hits;
        }
    }

    private static readonly Recipe[] Recipes = {
        new("Timber fork", "Pine Forest", "Gust the first timber. Both rows should fall from the same hit.",
            "First timber", "Middle billets", "Forked sawhorse", "Sawyer’s left stack", "Sawyer’s right stack", "Latched Farm Gate", "Orchard bells", "Trail Chicken", "Terracotta Pot", "Bound Hay"),
        new("Pot and bale", "South Bank", "Gust the pot once. It should tip the bale and score each prop once.", "Terracotta Pot", "Bound Hay"),
        new("Springboard · blue", "East Quarter", "Gust from the blue side. The second board should ring the bells.",
            "Split launch", "Bell jump", "Orchard bells") { side = -1.05f, alternatives = new[] { "Milk churns", "Terracotta Pot", "Bound Hay" } },
        new("Springboard · red", "East Quarter", "Gust from the red side. The sack should hit the churns, then the pot and bale.",
            "Split launch", "Milk churns", "Terracotta Pot", "Bound Hay") { side = 1.05f, alternatives = new[] { "Bell jump", "Orchard bells" } },
        new("Weaving shuttle", "West Hamlet", "Gust the shuttle. Follow the yarn to the washing, hen, pot, and bale.",
            "Flying shuttle", "Dye reel", "Laundry Line", "Trail Chicken", "Terracotta Pot", "Bound Hay"),
        new("Mill sluice", "South Bank", "Gust the sluice. Watch the water reach the wheel and topple the churns.",
            "Mill sluice", "Paddle wheel", "Milk churns", "Terracotta Pot", "Bound Hay"),
        new("Pollen split", "Flower Field", "Gust the red poppy. Watch the cornflower and buttercup carry separate branches.",
            "Poppy vane", "Cornflower vane", "Buttercup vane", "Laundry Line", "Orchard bells", "Trail Chicken", "Terracotta Pot", "Bound Hay"),
        new("Apple press", "Orchard", "Gust the bough. The loaded basket should trip the press and send the cork into the bell.",
            "Windfall bough", "Picking basket", "Cider press", "Orchard bells"),
        new("Cider spill", "Village Center", "Gust the awning once. Let the cask take the closed-gate route to the bells.",
            "Cider stall", "Loose cask", "Orchard bells", "Trail Chicken", "Terracotta Pot", "Bound Hay") { alternatives = new[] { "Latched Farm Gate", "Milk churns" } },
        new("Cask rebound", "Village Center", "Gust the gate. Gust again when the cask starts rolling to strike both displays.",
            "Latched Farm Gate", "Loose cask", "Milk churns", "Orchard bells") { alternatives = new[] { "Cider stall", "Trail Chicken", "Terracotta Pot", "Bound Hay" } }
    };

    [MenuItem("Tools/Sprint 3/Regenerate Combo Test Scene")]
    public static void Generate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before reseeding the scene.");
        var previous = SceneManager.GetActiveScene();
        var existing = SceneManager.GetSceneByPath(Destination);
        if (existing.isLoaded && existing.isDirty)
            throw new InvalidOperationException("Save or discard your edits to the generated test scene before regenerating it.");
        var village = SceneManager.GetSceneByPath(Source);
        bool openedSource = !village.isLoaded;
        bool emptyWorkspace = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(previous.path) && !previous.isDirty;
        if (openedSource)
            village = EditorSceneManager.OpenScene(Source, emptyWorkspace ? OpenSceneMode.Single : OpenSceneMode.Additive);
        var lab = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        bool saved = false;
        try
        {
            SceneManager.SetActiveScene(lab);
            var roots = village.GetRootGameObjects();
            var pilotSource = roots.SelectMany(t => t.GetComponentsInChildren<EnvironmentFlightPreview>()).Single();
            var districts = roots.SelectMany(t => t.GetComponentsInChildren<MarketDistrict>()).ToDictionary(d => d.name);
            var bench = new GameObject("Combo test bench").AddComponent<ComboTestBench>();
            bench.overview = MakeCamera();
            Terrain ground = MakeGround();
            var light = new GameObject("Afternoon light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .94f, .82f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(51f, -32f, 0f);
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .67f, .69f);
            RenderSettings.fog = false;

            bench.pilotTemplate = MakePilot(pilotSource, bench.overview);
            var groups = new Dictionary<string, Transform>();
            var seeds = new List<ComboTestSeed>();
            foreach (var recipe in Recipes)
            {
                if (!groups.TryGetValue(recipe.district, out var group))
                {
                    group = MakeTemplate(districts[recipe.district], ground, recipe.hits[0]);
                    groups.Add(recipe.district, group);
                }
                seeds.Add(MakeSeed(group, recipe));
            }
            bench.seeds = seeds.ToArray();
            var first = bench.seeds[0];
            first.Prepare();
            first.gameObject.SetActive(true);
            bench.pilotTemplate.transform.SetPositionAndRotation(first.arrival.position, first.arrival.rotation);
            bench.pilotTemplate.gameObject.SetActive(true);
            if (existing.isLoaded) EditorSceneManager.CloseScene(existing, true);
            if (!EditorSceneManager.SaveScene(lab, Destination))
                throw new InvalidOperationException("Unity could not save the combo test scene.");
            saved = true;
            Debug.Log($"Seeded {seeds.Count} repeatable combo tests from {groups.Count} districts.");
        }
        finally
        {
            if (!saved) EditorSceneManager.CloseScene(lab, true);
            if (openedSource) EditorSceneManager.CloseScene(village, true);
            if (previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (saved && previous.isLoaded && previous.path != Destination)
                EditorSceneManager.CloseScene(lab, true);
        }
    }

    private static Camera MakeCamera()
    {
        var go = new GameObject("Combo overview");
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        camera.orthographic = true;
        camera.orthographicSize = 7.2f;
        camera.farClipPlane = 105f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.19f, .26f, .28f);
        go.transform.rotation = Quaternion.Euler(39f, -20f, 0f);
        go.transform.position = new Vector3(0, 1, 2) - go.transform.forward * 35f;
        return camera;
    }

    private static Terrain MakeGround()
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(GroundData);
        if (!data)
        {
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(64, 2, 64) };
            AssetDatabase.CreateAsset(data, GroundData);
        }
        var floor = Terrain.CreateTerrainGameObject(data);
        floor.name = "Level test ground";
        floor.transform.position = new Vector3(-32, 0, -32);
        var terrain = floor.GetComponent<Terrain>();
        terrain.drawHeightmap = false;
        // Use the same ground surface for flight collision, animal movement and rolling props.
        var top = GameObject.CreatePrimitive(PrimitiveType.Plane);
        top.name = "Practice floor";
        Object.DestroyImmediate(top.GetComponent<Collider>());
        top.transform.localScale = new Vector3(6.4f, 1, 6.4f);
        top.GetComponent<MeshRenderer>().sharedMaterial = FloorMaterial(data);
        return terrain;
    }

    private static Material FloorMaterial(TerrainData ground)
    {
        var material = AssetDatabase.LoadAllAssetsAtPath(GroundData).OfType<Material>().FirstOrDefault();
        if (material) return material;
        const int size = 128;
        var grid = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Metre grid", wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var color = new Color32(93, 111, 116, 255);
            if (x % 32 == 0 || y % 32 == 0) color = new Color32(103, 122, 125, 255);
            if (x == 0 || y == 0) color = new Color32(113, 131, 132, 255);
            pixels[y * size + x] = color;
        }
        grid.SetPixels32(pixels);
        grid.Apply(true, false);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Test floor" };
        material.SetTexture("_BaseMap", grid);
        material.SetTextureScale("_BaseMap", Vector2.one * 16f);
        material.SetFloat("_Smoothness", .05f);
        AssetDatabase.AddObjectToAsset(grid, ground);
        AssetDatabase.AddObjectToAsset(material, ground);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static EnvironmentFlightPreview MakePilot(EnvironmentFlightPreview source, Camera camera)
    {
        var pilot = Object.Instantiate(source);
        SceneManager.MoveGameObjectToScene(pilot.gameObject, SceneManager.GetActiveScene());
        pilot.name = "Test butterfly";
        pilot.gameObject.SetActive(false);
        var calm = new GameObject("Calm test conditions");
        pilot.wind = calm.AddComponent<WindSystem>();
        pilot.wind.prevailingSpeed = 0f;
        pilot.weather = calm.AddComponent<WeatherSystem>();
        pilot.weather.enabled = false;
        pilot.view = camera;
        var fields = new SerializedObject(pilot);
        fields.FindProperty("comboTest").boolValue = true;
        fields.FindProperty("districts").arraySize = 0;
        fields.FindProperty("xLimits").vector2Value = new Vector2(-24, 24);
        fields.FindProperty("zLimits").vector2Value = new Vector2(-24, 24);
        fields.FindProperty("yLimits").vector2Value = new Vector2(.3f, 12);
        fields.ApplyModifiedPropertiesWithoutUndo();
        return pilot;
    }

    private static Transform MakeTemplate(MarketDistrict district, Terrain terrain, string first)
    {
        var clone = Object.Instantiate(district);
        var root = clone.transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, SceneManager.GetActiveScene());
        root.name = district.name;
        root.gameObject.SetActive(false);
        var pivot = root.Find(first);
        var rotation = Quaternion.Euler(0, -pivot.eulerAngles.y, 0);
        root.SetPositionAndRotation(rotation * (root.position - pivot.position), rotation * root.rotation);
        Object.DestroyImmediate(clone);
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (!child.TryGetComponent<Interactable>(out var prop))
            {
                Object.DestroyImmediate(child.gameObject);
                continue;
            }
            var p = child.position;
            child.position = new Vector3(p.x, .025f, p.z);
            var fields = new SerializedObject(prop);
            var surface = fields.FindProperty("terrain");
            if (surface != null) surface.objectReferenceValue = terrain;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
        if (root.name == "Pine Forest")
        {
            var hen = root.GetComponentInChildren<StartleChicken>(true).transform;
            var pot = root.GetComponentInChildren<KnockOverPot>(true);
            var bale = (LooseHay)new SerializedObject(pot).FindProperty("bale").objectReferenceValue;
            var gate = root.GetComponentInChildren<OpenGate>(true).transform;
            hen.position = gate.position + new Vector3(.45f, 0, 1.25f);
            var oldPot = pot.transform.position;
            pot.transform.position = hen.position + new Vector3(2.55f, 0f, .15f);
            bale.transform.position += pot.transform.position - oldPot;
        }
        return root;
    }

    private static ComboTestSeed MakeSeed(Transform group, Recipe recipe)
    {
        var all = group.GetComponentsInChildren<Interactable>(true);
        Interactable Find(string name)
        {
            if (name == "Bound Hay")
            {
                var pot = all.OfType<KnockOverPot>().Single();
                return (Interactable)new SerializedObject(pot).FindProperty("bale").objectReferenceValue;
            }
            return all.Single(p => p.name == name);
        }
        var seed = group.gameObject.AddComponent<ComboTestSeed>();
        seed.title = recipe.title;
        seed.instruction = recipe.hint;
        seed.expected = recipe.hits.Select(Find).ToArray();
        seed.visible = seed.expected.Concat(recipe.alternatives.Select(Find)).Distinct().ToArray();
        var target = seed.expected[0].transform;
        var spawn = new GameObject(recipe.title + " approach").transform;
        spawn.SetParent(group, false);
        spawn.position = target.position - target.forward * 2.85f + target.right * recipe.side + Vector3.up * 1.48f;
        spawn.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target.position - spawn.position, Vector3.up));
        seed.arrival = spawn;
        return seed;
    }
}
