using Amusnet.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds grass tufts to the Village terrain and a procedural sky + brighter lighting.
public static class VillageSkyGrassSetup
{
    const string Dir = "Assets/Materials/Village";

    [MenuItem("Tools/Village/Add Grass And Sky")]
    public static void Build()
    {
        AddSky();
        AddGrass();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    static void AddSky()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Village Sky.mat");
        if (!mat)
        {
            mat = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(mat, Dir + "/Village Sky.mat");
        }
        mat.SetFloat("_SunSize", 0.06f);
        mat.SetFloat("_SunSizeConvergence", 5f);
        mat.SetColor("_SkyTint", new Color(0.5f, 0.62f, 0.85f));
        mat.SetColor("_GroundColor", new Color(0.55f, 0.58f, 0.5f));
        EditorUtility.SetDirty(mat);
        RenderSettings.skybox = mat;

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (cam.gameObject.name == "Preview Camera") cam.clearFlags = CameraClearFlags.Skybox;

        var root = GameObject.Find("Weather and wind");
        var atm = root.GetComponent<VillageAtmosphere>();
        if (!atm) atm = root.AddComponent<VillageAtmosphere>();
        atm.weather = root.GetComponent<WeatherSystem>();
        atm.skybox = mat;
    }

    static Texture2D MakeGrassTexture()
    {
        const string path = Dir + "/Grass Tuft.png";
        const int w = 128, h = 128;
        var px = new Color32[w * h];
        var rng = new System.Random(7);
        for (int b = 0; b < 14; b++)
        {
            float x0 = 14 + (float)rng.NextDouble() * (w - 28);
            float lean = ((float)rng.NextDouble() - 0.5f) * 40f;
            float height = 60 + (float)rng.NextDouble() * 64;
            float bw = 3.5f + (float)rng.NextDouble() * 2f;
            float tone = 0.7f + (float)rng.NextDouble() * 0.3f;
            for (int y = 0; y < height && y < h; y++)
            {
                float t = y / height;
                float cx = x0 + lean * t * t;
                float half = bw * (1f - t) * 0.5f + 0.3f;
                for (int x = Mathf.FloorToInt(cx - half); x <= Mathf.CeilToInt(cx + half); x++)
                {
                    if (x < 0 || x >= w) continue;
                    var c = Color.Lerp(new Color(0.55f, 0.75f, 0.3f), new Color(0.25f, 0.5f, 0.15f), 1f - t) * tone;
                    px[y * w + x] = new Color(c.r, c.g, c.b, 1f);
                }
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = true;
        imp.mipMapsPreserveCoverage = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Three crossed quads with an alpha-clipped URP Lit material; ~0.6 m tall.
    static GameObject MakeGrassPrefab(Texture2D tex)
    {
        const string matPath = Dir + "/Grass Tuft.mat", meshPath = Dir + "/Grass Tuft.asset", prefabPath = Dir + "/Grass Tuft.prefab";
        AssetDatabase.DeleteAsset(matPath); AssetDatabase.DeleteAsset(meshPath); AssetDatabase.DeleteAsset(prefabPath);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_Cull", 0); mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Smoothness", 0f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = 2450;
        AssetDatabase.CreateAsset(mat, matPath);

        var v = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var tri = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 3; i++)
        {
            var rot = Quaternion.Euler(0, i * 60f, 0);
            int b = v.Count;
            v.Add(rot * new Vector3(-0.4f, 0, 0)); v.Add(rot * new Vector3(0.4f, 0, 0));
            v.Add(rot * new Vector3(-0.4f, 0.8f, 0)); v.Add(rot * new Vector3(0.4f, 0.8f, 0));
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
            tri.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        }
        var mesh = new Mesh { name = "Grass Tuft" };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var up = new Vector3[v.Count]; for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
        mesh.normals = up; // light like the ground so tufts do not flicker dark
        AssetDatabase.CreateAsset(mesh, meshPath);

        var go = new GameObject("Grass Tuft");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // 3D grass painted into the terrain's own detail list (InstanceCountMode is required for it to draw).
    static void AddGrass()
    {
        var terrain = Terrain.activeTerrain;
        var d = terrain.terrainData;
        var prefab = MakeGrassPrefab(MakeGrassTexture());

        var old = GameObject.Find("Grass");
        if (old) Object.DestroyImmediate(old);

        d.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        d.SetDetailResolution(1024, 16);
        d.detailPrototypes = new[]
        {
            new DetailPrototype
            {
                prototype = prefab, usePrototypeMesh = true, useInstancing = true,
                renderMode = DetailRenderMode.VertexLit, density = 1f, positionJitter = 0.5f,
                minWidth = 0.8f, maxWidth = 1.4f, minHeight = 0.7f, maxHeight = 1.3f,
                noiseSpread = 0.4f, healthyColor = Color.white, dryColor = Color.white
            }
        };

        // Roads and the plaza have no colliders; give them temporary ones so the raycast below skips them.
        var temp = new System.Collections.Generic.List<MeshCollider>();
        var roads = GameObject.Find("Dirt roads");
        var plaza = GameObject.Find("Plaza");
        foreach (var root in new[] { roads, plaza })
            if (root)
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                    if (!mf.GetComponent<Collider>()) temp.Add(mf.gameObject.AddComponent<MeshCollider>());
        Physics.SyncTransforms();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        int dr = d.detailWidth, ar = d.alphamapResolution;
        var alpha = d.GetAlphamaps(0, 0, ar, ar);
        var map = new int[dr, dr];
        var rng = new System.Random(3);
        for (int y = 0; y < dr; y++)
            for (int x = 0; x < dr; x++)
            {
                float u = (x + 0.5f) / dr, v = (y + 0.5f) / dr;
                int ax = Mathf.Min(ar - 1, (int)(u * ar)), ay = Mathf.Min(ar - 1, (int)(v * ar));
                float green = alpha[ay, ax, 0] + alpha[ay, ax, 1] + alpha[ay, ax, 2] + alpha[ay, ax, 5];
                if (green < 0.5f || d.GetSteepness(u, v) > 40f) continue;
                if (rng.NextDouble() >= 0.05) continue;
                // keep grass off the fountain, buildings, props and paths
                var wp = terrain.transform.position + new Vector3(u * d.size.x, 0, v * d.size.z);
                wp.y = terrain.SampleHeight(wp) + terrain.transform.position.y;
                if (Physics.Raycast(wp + Vector3.up * 30f, Vector3.down, out var hit, 60f) && !(hit.collider is TerrainCollider)) continue;
                map[y, x] = 1;
            }
        Physics.queriesHitBackfaces = backfaces;
        foreach (var c in temp) Object.DestroyImmediate(c);
        d.SetDetailLayer(0, 0, 0, map);
        terrain.detailObjectDistance = 100f;
        terrain.detailObjectDensity = 1f;
        terrain.drawTreesAndFoliage = true;
        EditorUtility.SetDirty(d);
    }
}
