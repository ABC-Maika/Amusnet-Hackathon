using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Village polish: invisible borders, distant hills that fade into the fog, fountain water and soft rain drops.
public static class VillageWorldPolish
{
    const string Dir = "Assets/Materials/Village";

    [MenuItem("Tools/Village/Add Borders Horizon And Fountain")]
    public static void Build()
    {
        var drop = MakeSoftDropTexture();
        FixRain(drop);
        AddFountain(drop);
        AddBorders();
        AddHorizon();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    // Round soft-edged dot: replaces the untextured (square) particles.
    static Texture2D MakeSoftDropTexture()
    {
        const string path = Dir + "/Soft Drop.png";
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ParticleMaterial(string name, Texture2D tex, Color color)
    {
        string path = Dir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void FixRain(Texture2D drop)
    {
        var mat = ParticleMaterial("Rain Streak", drop, new Color(0.8f, 0.9f, 1f, 0.45f));
        var rain = GameObject.Find("Weather and wind/Rain");
        if (!rain) return;
        var r = rain.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        var main = rain.GetComponent<ParticleSystem>().main;
        main.startSize = 0.035f;
    }

    static void AddFountain(Texture2D drop)
    {
        var fountain = GameObject.Find("Fountain");
        if (!fountain) return;
        var old = fountain.transform.Find("Fountain Water FX");
        if (old) Object.DestroyImmediate(old.gameObject);

        var bowl = fountain.transform.Find("Top bowl");
        var b = bowl ? bowl.GetComponent<Renderer>().bounds : new Bounds(fountain.transform.position + Vector3.up * 2.7f, Vector3.one * 3f);
        var top = new Vector3(b.center.x, b.max.y, b.center.z);

        var root = new GameObject("Fountain Water FX");
        root.transform.SetParent(fountain.transform, false);
        root.transform.position = top;
        var mat = ParticleMaterial("Fountain Drop", drop, new Color(0.82f, 0.92f, 1f, 0.75f));

        // Central jet: shoots up ~1.3 m and falls back into the top bowl.
        var jet = MakeSystem(root.transform, "Jet", mat);
        jet.transform.localPosition = Vector3.up * 0.05f;
        var m = jet.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.2f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(4.6f, 5.3f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        m.gravityModifier = 1f;
        m.maxParticles = 1500;
        var e = jet.emission; e.rateOverTime = 260f;
        var sh = jet.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9f; sh.radius = 0.06f;

        // Overflow: a curtain of drops spilling off the top bowl rim into the basin.
        var spill = MakeSystem(root.transform, "Overflow", mat);
        spill.transform.localPosition = Vector3.down * 0.05f;
        m = spill.main;
        m.startLifetime = 0.65f;
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
        m.gravityModifier = 1f;
        m.maxParticles = 1500;
        e = spill.emission; e.rateOverTime = 420f;
        sh = spill.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = b.extents.x * 0.97f; sh.radiusThickness = 0f;

        // Splash mist where the water lands in the basin.
        var water = fountain.transform.Find("Water");
        if (water)
        {
            // The water disc sat below the basin rim and was hidden; lift it just above the rim.
            var basin = fountain.transform.Find("Basin");
            if (basin)
            {
                float lift = basin.GetComponent<Renderer>().bounds.max.y + 0.02f - water.GetComponent<Renderer>().bounds.max.y;
                if (lift > 0f) water.position += Vector3.up * lift;
            }
            var wb = water.GetComponent<Renderer>().bounds;
            var mist = MakeSystem(root.transform, "Splash", mat);
            mist.transform.position = new Vector3(wb.center.x, wb.max.y, wb.center.z);
            m = mist.main;
            m.startLifetime = 0.5f;
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            m.gravityModifier = 0.6f;
            m.startColor = new Color(1f, 1f, 1f, 0.5f);
            e = mist.emission; e.rateOverTime = 120f;
            sh = mist.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = b.extents.x * 1.25f; sh.radiusThickness = 0.15f;
        }
    }

    static ParticleSystem MakeSystem(Transform parent, string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // emit upwards / circle lies flat
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = true; m.playOnAwake = true;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startColor = new Color(0.85f, 0.93f, 1f, 0.8f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // Invisible walls just inside the terrain edge, tall enough that the butterfly cannot fly over.
    static void AddBorders()
    {
        var old = GameObject.Find("World Borders");
        if (old) Object.DestroyImmediate(old);
        var t = Terrain.activeTerrain;
        var p = t.transform.position; var s = t.terrainData.size;
        const float inset = 4f, height = 400f, thick = 2f;
        var min = new Vector3(p.x + inset, 0, p.z + inset);
        var max = new Vector3(p.x + s.x - inset, 0, p.z + s.z - inset);
        var c = (min + max) * 0.5f; var size = max - min;

        var root = new GameObject("World Borders");
        void Wall(string n, Vector3 center, Vector3 sz)
        {
            var w = new GameObject(n);
            w.transform.SetParent(root.transform, false);
            w.transform.position = center;
            w.AddComponent<BoxCollider>().size = sz;
        }
        float y = height * 0.5f - 20f;
        Wall("North", new Vector3(c.x, y, max.z + thick * 0.5f), new Vector3(size.x + thick * 2, height, thick));
        Wall("South", new Vector3(c.x, y, min.z - thick * 0.5f), new Vector3(size.x + thick * 2, height, thick));
        Wall("East", new Vector3(max.x + thick * 0.5f, y, c.z), new Vector3(thick, height, size.z + thick * 2));
        Wall("West", new Vector3(min.x - thick * 0.5f, y, c.z), new Vector3(thick, height, size.z + thick * 2));
        Wall("Ceiling", new Vector3(c.x, 80f, c.z), new Vector3(size.x + thick * 2, thick, size.z + thick * 2));
    }

    // Rolling hills around the map that rise away from the terrain edge and dissolve into the fog.
    static void AddHorizon()
    {
        var old = GameObject.Find("Horizon Hills");
        if (old) Object.DestroyImmediate(old);
        var t = Terrain.activeTerrain;
        var d = t.terrainData;
        var p = t.transform.position; var s = d.size;
        var rmin = new Vector2(p.x, p.z); var rmax = new Vector2(p.x + s.x, p.z + s.z);
        var center = (rmin + rmax) * 0.5f;

        const float half = 900f, step = 10f;
        int n = Mathf.RoundToInt(half * 2 / step) + 1;
        var verts = new Vector3[n * n];
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = center.x - half + ix * step, z = center.y - half + iz * step;
                var clamped = new Vector3(Mathf.Clamp(x, rmin.x, rmax.x), 0, Mathf.Clamp(z, rmin.y, rmax.y));
                float edgeH = t.SampleHeight(clamped) + p.y;
                float dist = Vector2.Distance(new Vector2(x, z), new Vector2(clamped.x, clamped.z));
                float h;
                if (dist <= 0f) h = edgeH - 0.4f; // hidden under the terrain
                else
                {
                    float rise = Mathf.SmoothStep(0f, 1f, dist / 220f);
                    float noise = Mathf.PerlinNoise(x * 0.006f + 13f, z * 0.006f + 7f) * 0.7f
                                + Mathf.PerlinNoise(x * 0.02f, z * 0.02f) * 0.3f;
                    h = Mathf.Lerp(edgeH - 0.1f, p.y + 8f + noise * 55f, rise);
                }
                verts[iz * n + ix] = new Vector3(x, h, z);
            }
        var tris = new int[(n - 1) * (n - 1) * 6];
        int k = 0;
        for (int iz = 0; iz < n - 1; iz++)
            for (int ix = 0; ix < n - 1; ix++)
            {
                int a = iz * n + ix;
                tris[k++] = a; tris[k++] = a + n; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = a + n; tris[k++] = a + n + 1;
            }
        var mesh = new Mesh { name = "Horizon Hills", indexFormat = IndexFormat.UInt32 };
        var uvs = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++) uvs[i] = new Vector2(verts[i].x, verts[i].z);
        mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        const string meshPath = Dir + "/Horizon Hills.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        // Match the terrain's main grass layer so the seam is not visible.
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Horizon Grass.mat");
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, Dir + "/Horizon Grass.mat"); }
        var layer = d.terrainLayers.Length > 0 ? d.terrainLayers[0] : null;
        if (layer && layer.diffuseTexture)
        {
            mat.SetTexture("_BaseMap", layer.diffuseTexture);
            mat.SetTextureScale("_BaseMap", Vector2.one / Mathf.Max(1f, layer.tileSize.x));
        }
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0f);
        EditorUtility.SetDirty(mat);

        var go = new GameObject("Horizon Hills");
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }
}
