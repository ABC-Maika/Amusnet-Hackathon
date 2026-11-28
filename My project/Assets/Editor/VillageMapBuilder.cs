#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Re-arranges the existing Village scene (keeps its terrain, creek, materials, mountains and atmosphere)
// to match the 7-section design map: 1 West Hamlet, 2 Village Center, 3 East Quarter, 4 South Bank,
// 5 Flower Field, 6 Pine Forest, 7 Orchard, with bridges A/B/C over the creek.
// The map's river is aligned to the scene's existing creek. Re-runnable: rebuilds the "Village Layout" root.
public static class VillageMapBuilder
{
    const string PrefabDir = "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs/";
    const string EnvAsset = "Assets/Scenes/Village Environment.asset";

    // terrain extents of the existing scene
    static float X0 = -120f, X1 = 120f, Z0 = -127.8f, Z1 = 89.8f;

    static Terrain T;
    static System.Random Rng;
    static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
    static float[] CreekZ;                 // creek centerline z for x = X0..X1 (1 m steps)
    static List<Vector2[]> Roads;
    static readonly List<Vector2> HousePos = new List<Vector2>();

    // map river polyline in map pixels (used to align map rows to the creek)
    static readonly float[] MapRiver = { 30, 345, 100, 350, 150, 362, 240, 385, 320, 398, 420, 402, 500, 415, 580, 440, 650, 452 };

    // ------------------------------------------------------------------ map -> world
    static float MapRiverY(float px)
    {
        for (int i = 0; i < MapRiver.Length - 2; i += 2)
        {
            float ax = MapRiver[i], ay = MapRiver[i + 1], bx = MapRiver[i + 2], by = MapRiver[i + 3];
            if (px <= bx) return Mathf.Lerp(ay, by, Mathf.InverseLerp(ax, bx, px));
        }
        return MapRiver[MapRiver.Length - 1];
    }

    static Vector2 P(float px, float py)
    {
        float x = Mathf.Lerp(X0, X1, (px - 30f) / 620f);
        float ry = MapRiverY(px);
        float zc = Creek(x);
        float z = py <= ry
            ? Mathf.Lerp(Z1 - 2f, zc, (py - 72f) / (ry - 72f))
            : Mathf.Lerp(zc, Z0 + 2f, (py - ry) / (605f - ry));
        return new Vector2(x, z);
    }

    static Vector2[] Poly(params float[] xy)
    {
        var l = new Vector2[xy.Length / 2];
        for (int i = 0; i < l.Length; i++) l[i] = P(xy[i * 2], xy[i * 2 + 1]);
        return l;
    }

    // ------------------------------------------------------------------ entry
    [MenuItem("Tools/Village/Arrange Village To Map")]
    public static void Build()
    {
        Rng = new System.Random(7);
        HousePos.Clear();
        var scene = SceneManager.GetActiveScene();
        T = Terrain.activeTerrain;
        var tp = T.transform.position; var ts = T.terrainData.size;
        X0 = tp.x; X1 = tp.x + ts.x; Z0 = tp.z; Z1 = tp.z + ts.z;
        SampleCreek();

        // remove what gets re-laid; keep terrain, creek meshes, rocks, boat, mountains, lighting
        DestroyChildren("Cottages");
        DestroyChildren("Gardens and village props");
        DestroyNamed("Footpaths and cottage approaches");
        DestroyNamed("Village Layout");
        var woods = GameObject.Find("Woodland and hills");
        var mountains = new List<Transform>();
        if (woods)
            foreach (Transform c in woods.transform)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                if (src && src.name.StartsWith("Mountain")) mountains.Add(c);
            }
        if (woods)
            for (int i = woods.transform.childCount - 1; i >= 0; i--)
                if (!mountains.Contains(woods.transform.GetChild(i))) Object.DestroyImmediate(woods.transform.GetChild(i).gameObject);

        Roads = new List<Vector2[]>
        {
            Poly(40, 270, 150, 262, 240, 264, 300, 262, 360, 262, 430, 260, 530, 263, 620, 270),   // main road, north bank
            Poly(320, 190, 320, 262, 321, 330, 320, 398, 322, 450, 322, 500, 320, 545),          // center road over bridge B
            Poly(40, 518, 120, 522, 200, 520, 320, 515, 450, 515, 530, 518, 600, 520),           // south road
            Poly(155, 200, 155, 262, 155, 320, 153, 375, 160, 430, 155, 480, 150, 520),          // west road over bridge A
            Poly(510, 200, 510, 262, 511, 340, 512, 425, 520, 455, 520, 518),                   // east road over bridge C
        };

        var root = new GameObject("Village Layout");
        Transform G(string n, Transform parent = null)
        {
            var g = new GameObject(n);
            g.transform.SetParent(parent ? parent : root.transform, false);
            return g.transform;
        }
        var sections = G("Sections");
        var west = G("1 West Hamlet", sections);
        var center = G("2 Village Center", sections);
        var east = G("3 East Quarter", sections);
        var south = G("4 South Bank", sections);
        var flowerField = G("5 Flower Field", sections);
        var pine = G("6 Pine Forest", sections);
        var orchard = G("7 Orchard", sections);
        var paths = G("Dirt roads");
        var creekSide = G("Creek-side trees");
        var borderWoods = G("Border woods");

        // ---- houses ----
        var fountain = P(322, 262);
        var houseSets = new (Transform parent, float[] pts)[]
        {
            (west, new float[] { 85,215, 122,232, 188,215, 218,235, 88,300, 122,295, 188,298, 218,300 }),
            (east, new float[] { 443,233, 547,215, 582,237, 478,308, 550,300, 582,296 }),
            (south, new float[] { 248,482, 287,525, 355,480, 395,485, 355,525, 397,525, 245,560 }),
            (flowerField, new float[] { 133,472 }),
        };
        int hi = 0;
        foreach (var set in houseSets)
            for (int i = 0; i < set.pts.Length; i += 2)
                PlaceHouse(set.parent, P(set.pts[i], set.pts[i + 1]), hi++, null);
        float[] ring = { 295,205, 347,205, 268,228, 375,228, 268,300, 375,300, 293,330, 350,332 };
        for (int i = 0; i < ring.Length; i += 2)
            PlaceHouse(center, P(ring[i], ring[i + 1]), hi++, fountain);

        // ---- village center: fountain plaza, benches, lanterns, village green patch ----
        BuildFountain(center, fountain);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            PlaceOne(center, "Lantern", fountain + dir * 9.5f, Rand(0, 360), 1f);
            var bp = fountain + new Vector2(Mathf.Cos(a + 0.4f), Mathf.Sin(a + 0.4f)) * 8f;
            PlaceOne(center, "Bench", bp, Yaw(fountain - bp), 1f);
        }
        var green = GameObject.Find("Grassy common");
        if (green) green.transform.position += new Vector3(fountain.x - green.GetComponent<Renderer>().bounds.center.x, 0, fountain.y + 14f - green.GetComponent<Renderer>().bounds.center.z);

        // ---- dirt roads (old footpath materials) ----
        var envMats = LoadEnvMaterials();
        foreach (var r in Roads) BuildRoad(paths, r, envMats);

        // ---- bridges A/B/C on the creek ----
        var creekRoot = GameObject.Find("Creek crossing");
        Transform oldBridge = null;
        if (creekRoot)
        {
            // re-runs: drop the bridges this script added, keep the scene's original bridge as Bridge B
            DestroyNamed("Creek crossing/Bridge A");
            DestroyNamed("Creek crossing/Bridge C");
            foreach (Transform c in creekRoot.transform)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                if (src && src.name == "Bridge") { oldBridge = c; break; }
            }
        }
        Vector3 bScale = oldBridge ? oldBridge.localScale : new Vector3(1.2f, 1f, 1.5f);
        float bY = oldBridge ? oldBridge.position.y : 0f;
        var bridgeParent = creekRoot ? creekRoot.transform : root.transform;
        float[] bx = { 153, 320, 512 };
        string[] bn = { "Bridge A", "Bridge B", "Bridge C" };
        for (int i = 0; i < 3; i++)
        {
            var b = i == 1 && oldBridge ? oldBridge.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(Prefab("Bridge"), bridgeParent);
            float x = Mathf.Lerp(X0, X1, (bx[i] - 30f) / 620f);
            float z = Creek(x);
            float slope = (Creek(x + 2f) - Creek(x - 2f)) / 4f;
            var across = new Vector2(-slope, 1f).normalized;
            b.name = bn[i];
            b.transform.position = new Vector3(x, bY, z);
            b.transform.rotation = Quaternion.Euler(0, Yaw(across), 0);
            b.transform.localScale = bScale;
            foreach (var s in new[] { -1f, 1f })
                PlaceOne(root.transform.Find("Sections"), "Lantern", new Vector2(x, z) + across * s * 10f + new Vector2(across.y, -across.x) * 2.4f, Rand(0, 360), 1f);
        }

        // ---- orchard (rows of round trees) ----
        for (float px = 250; px <= 412; px += 27)
            for (float py = 132; py <= 180; py += 22)
                PlaceOne(orchard, Pick("Tree_1", "Tree_2", "Tree_5"), P(px + Rand(-3, 3), py + Rand(-3, 3)), Rand(0, 360), Rand(0.7f, 0.9f));
        Scatter(orchard, Flowers, 240, 125, 420, 186, 6f, 0.8f, 1.2f);

        // ---- pine forest (southeast) ----
        Scatter(pine, Pines, 432, 466, 612, 598, 5.2f, 0.75f, 1.15f);

        // ---- border woods (NW, NE pines, a few leafy clusters) ----
        Scatter(borderWoods, Pines, 40, 100, 205, 198, 6f, 0.75f, 1.1f);
        Scatter(borderWoods, Pines, 425, 100, 570, 165, 6f, 0.75f, 1.1f);
        Scatter(borderWoods, Pines, 590, 100, 645, 330, 8f, 0.75f, 1.1f);
        Scatter(borderWoods, Leafy, 36, 312, 60, 470, 9f, 0.75f, 1.05f);
        float[] lone = { 420,300, 418,322, 434,327, 225,352, 255,345, 62,410, 88,404, 102,414 };
        for (int i = 0; i < lone.Length; i += 2)
            PlaceOne(creekSide, Pick(Leafy), P(lone[i], lone[i + 1]), Rand(0, 360), Rand(0.75f, 1f));

        // ---- creek-side trees, like the original scene ----
        for (int i = 0; i < 70; i++)
        {
            float x = Rand(X0 + 6, X1 - 6);
            float off = Rand(11f, 24f) * (Rng.Next(2) == 0 ? -1 : 1);
            var p = new Vector2(x, Creek(x) + off);
            if (Blocked(p, 6f)) continue;
            PlaceOne(creekSide, Rng.Next(3) == 0 ? Pick(Pines) : Pick(Leafy), p, Rand(0, 360), Rand(0.7f, 1f));
        }

        // ---- flower field ----
        Scatter(flowerField, Flowers, 45, 432, 215, 590, 2.3f, 0.9f, 1.5f);
        // fence along the flower field's road side
        var ffA = P(50, 505); var ffB = P(210, 505);
        for (float t = 0; t <= 1f; t += 3.1f / Vector2.Distance(ffA, ffB))
        {
            var fp = Vector2.Lerp(ffA, ffB, t);
            if (RoadDist(fp) > 2.5f) PlaceOne(flowerField, "Fence", fp, Yaw(ffB - ffA) + 90f, 1f);
        }

        // ---- small gardens by each house: flowers, pots, crates, fence bits ----
        foreach (var h in HousePos)
        {
            for (int k = 0; k < 5; k++)
            {
                float a = Rand(0, 6.28f);
                var fp = h + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rand(6.5f, 9f);
                if (!Blocked(fp, 1.5f, true)) PlaceOne(FindParent(h, sections), Pick(Flowers), fp, Rand(0, 360), Rand(0.8f, 1.3f));
            }
            if (Rng.Next(2) == 0) TryProp(FindParent(h, sections), "Flower Pot", h, 6f);
            if (Rng.Next(3) == 0) TryProp(FindParent(h, sections), "Crate", h, 6.5f);
            if (Rng.Next(3) == 0)
            {
                float a = Rand(0, 6.28f);
                var fp = h + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8.5f;
                if (!Blocked(fp, 2f, true)) PlaceOne(FindParent(h, sections), "Fence", fp, a * Mathf.Rad2Deg, 1f);
            }
        }

        // ---- mountains ring the map like the brown border on the design ----
        ArrangeMountains(mountains, woods ? woods.transform : root.transform);

        // ---- spawn point ----
        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.SetParent(root.transform, false);
        spawn.transform.position = new Vector3(fountain.x, Ground(fountain) + 6f, fountain.y);

        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.ReflectionProbeStatic);
        spawn.isStatic = false;

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("VillageMapBuilder: arranged village to map, " + HousePos.Count + " houses.");
    }

    // ------------------------------------------------------------------ prefab lists
    static readonly string[] Pines = { "Pine Tree_1", "Pine Tree_2", "Pine Tree_3", "Pine Tree_4", "Pine Tree_5" };
    static readonly string[] Leafy = { "Tree_1", "Tree_2", "Tree_3", "Tree_4", "Tree_5", "Tree_6", "Tree_7", "Tree_8" };
    static readonly string[] Flowers = { "Flower_1", "Flower_2", "Flower_3", "Flower_4", "Flower_5" };

    static string Pick(params string[] a) { return a[Rng.Next(a.Length)]; }
    static float Rand(float a, float b) { return a + (float)Rng.NextDouble() * (b - a); }
    static float Yaw(Vector2 d) { return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }

    // ------------------------------------------------------------------ creek
    static void SampleCreek()
    {
        int n = Mathf.CeilToInt(X1 - X0) + 1;
        CreekZ = new float[n];
        var sum = new float[n]; var cnt = new int[n];
        var creek = GameObject.Find("Creek surface");
        if (creek)
        {
            var mf = creek.GetComponent<MeshFilter>();
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = creek.transform.TransformPoint(v);
                int i = Mathf.RoundToInt(w.x - X0);
                if (i < 0 || i >= n) continue;
                sum[i] += w.z; cnt[i]++;
            }
        }
        float last = -19f;
        for (int i = 0; i < n; i++) { if (cnt[i] > 0) last = sum[i] / cnt[i]; CreekZ[i] = last; }
        // back-fill the gap before the first sampled column
        int first = FirstHit(cnt);
        for (int i = 0; i < first; i++) CreekZ[i] = CreekZ[first];
        // smooth
        var s = (float[])CreekZ.Clone();
        for (int i = 0; i < n; i++)
        {
            float a = 0; int c = 0;
            for (int k = -3; k <= 3; k++) { int j = i + k; if (j >= 0 && j < n) { a += s[j]; c++; } }
            CreekZ[i] = a / c;
        }
    }

    static int FirstHit(int[] cnt) { for (int i = 0; i < cnt.Length; i++) if (cnt[i] > 0) return i; return 0; }

    static float Creek(float x)
    {
        float f = Mathf.Clamp(x - X0, 0, CreekZ.Length - 1.001f);
        int i = (int)f;
        return Mathf.Lerp(CreekZ[i], CreekZ[Mathf.Min(i + 1, CreekZ.Length - 1)], f - i);
    }

    static float CreekDist(Vector2 p) { return Mathf.Abs(p.y - Creek(p.x)); }

    // ------------------------------------------------------------------ placement
    static float Ground(Vector2 xz) { return T.SampleHeight(new Vector3(xz.x, 0, xz.y)) + T.transform.position.y; }

    static GameObject Prefab(string name)
    {
        if (!PrefabCache.TryGetValue(name, out var p) || !p)
            PrefabCache[name] = p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + name + ".prefab");
        return p;
    }

    static GameObject PlaceOne(Transform parent, string prefab, Vector2 xz, float yaw, float scale)
    {
        var p = Prefab(prefab);
        if (!p) { Debug.LogWarning("Missing prefab " + prefab); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
        go.transform.position = new Vector3(xz.x, Ground(xz), xz.y);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static bool InTerrain(Vector2 p, float m) { return p.x > X0 + m && p.x < X1 - m && p.y > Z0 + m && p.y < Z1 - m; }

    static bool Blocked(Vector2 p, float houseClear, bool allowNearHouse = false)
    {
        if (!InTerrain(p, 2f)) return true;
        if (CreekDist(p) < 10.5f) return true;
        if (RoadDist(p) < 3.2f) return true;
        foreach (var h in HousePos)
            if ((h - p).sqrMagnitude < (allowNearHouse ? 5.5f * 5.5f : houseClear * houseClear + 60f)) return true;
        return false;
    }

    static void Scatter(Transform parent, string[] names, float px0, float py0, float px1, float py1, float spacing, float smin, float smax)
    {
        var a = P(px0, py0); var b = P(px1, py1);
        float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.y, b.y), z1 = Mathf.Max(a.y, b.y);
        for (float x = x0; x <= x1; x += spacing)
            for (float z = z0; z <= z1; z += spacing)
            {
                var p = new Vector2(x + Rand(-spacing * 0.45f, spacing * 0.45f), z + Rand(-spacing * 0.45f, spacing * 0.45f));
                if (Blocked(p, 7f)) continue;
                PlaceOne(parent, Pick(names), p, Rand(0, 360), Rand(smin, smax));
            }
    }

    static void PlaceHouse(Transform parent, Vector2 xz, int index, Vector2? faceTarget)
    {
        Vector2 target = faceTarget ?? NearestRoadPoint(xz, out _);
        var go = PlaceOne(parent, "House_" + (index % 3 + 1), xz, Yaw(target - xz), Rand(0.7f, 0.82f));
        if (go) go.name = parent.name.Substring(2) + " house " + (parent.childCount);
        HousePos.Add(xz);
    }

    static void TryProp(Transform parent, string prefab, Vector2 h, float r)
    {
        for (int k = 0; k < 4; k++)
        {
            float a = Rand(0, 6.28f);
            var p = h + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (!Blocked(p, 1f, true)) { PlaceOne(parent, prefab, p, Rand(0, 360), 1f); return; }
        }
    }

    static Transform FindParent(Vector2 h, Transform sections)
    {
        // houses are created under their section, find it by nearest house child
        Transform best = sections; float bd = float.MaxValue;
        foreach (Transform s in sections)
            foreach (Transform c in s)
            {
                if (!c.name.Contains("house")) continue;
                float d = (new Vector2(c.position.x, c.position.z) - h).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
        return best;
    }

    // ------------------------------------------------------------------ fountain
    static void BuildFountain(Transform parent, Vector2 c)
    {
        var stone = GetMat("Village Stone", new Color(0.66f, 0.65f, 0.62f));
        var mats = LoadEnvMaterials();
        mats.TryGetValue("Dirt path", out var dirt);
        var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Creek Water.mat");
        float y = Ground(c);
        var g = new GameObject("Fountain").transform; g.SetParent(parent, false);
        Cyl(g, "Plaza", c, y + 0.04f, 10.5f, 0.08f, dirt ? dirt : stone, false);
        Cyl(g, "Basin", c, y + 0.4f, 4.2f, 0.8f, stone, true);
        Cyl(g, "Water", c, y + 0.72f, 3.6f, 0.06f, water ? water : stone, false);
        Cyl(g, "Pillar", c, y + 1.4f, 0.6f, 2.8f, stone, true);
        Cyl(g, "Top bowl", c, y + 2.8f, 1.5f, 0.25f, stone, true);
    }

    static void Cyl(Transform parent, string name, Vector2 c, float y, float radius, float height, Material m, bool collider)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        o.name = name; o.transform.SetParent(parent, false);
        o.transform.position = new Vector3(c.x, y, c.y);
        o.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        if (!collider) Object.DestroyImmediate(o.GetComponent<Collider>());
        if (m) o.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    static Material GetMat(string name, Color col)
    {
        const string dir = "Assets/Materials/Village";
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Materials", "Village");
        string path = dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", col);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Dictionary<string, Material> LoadEnvMaterials()
    {
        var d = new Dictionary<string, Material>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(EnvAsset))
            if (o is Material m) d[m.name] = m;
        return d;
    }

    // ------------------------------------------------------------------ roads
    static void BuildRoad(Transform parent, Vector2[] pts, Dictionary<string, Material> mats)
    {
        var line = Resample(pts, 1.5f);
        mats.TryGetValue("Dirt path", out var dirt);
        mats.TryGetValue("Path edges", out var edge);
        var go = new GameObject("Road");
        go.transform.SetParent(parent, false);
        var mesh = new Mesh { name = "Road ribbon" };
        var v = new List<Vector3>(); var uv = new List<Vector2>();
        var edgeTris = new List<int>(); var dirtTris = new List<int>();
        float u = 0;
        int prevStart = -1;
        for (int i = 0; i < line.Count; i++)
        {
            var p = line[i];
            bool ok = CreekDist(p) > 5.8f && InTerrain(p, 0.5f);
            var t = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
            var n = new Vector2(-t.y, t.x);
            float wobble = Mathf.PerlinNoise(p.x * 0.05f, p.y * 0.05f) * 0.8f;
            float hwDirt = 1.6f + wobble, hwEdge = hwDirt + 0.55f;
            int start = v.Count;
            foreach (var off in new[] { hwEdge, hwDirt, -hwDirt, -hwEdge })
            {
                var q = p + n * off;
                float y = Ground(q) + (Mathf.Abs(off) > hwDirt ? 0.04f : 0.07f);
                v.Add(new Vector3(q.x, y, q.y));
                uv.Add(new Vector2(off, u));
            }
            u += 0.4f;
            if (ok && prevStart >= 0)
            {
                int a = prevStart, b = start;
                // edge strips (0-1, 2-3), dirt strip (1-2)
                Quad(edgeTris, a + 0, a + 1, b + 0, b + 1);
                Quad(dirtTris, a + 1, a + 2, b + 1, b + 2);
                Quad(edgeTris, a + 2, a + 3, b + 2, b + 3);
            }
            prevStart = ok ? start : -1;
        }
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v); mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(edgeTris, 0); mesh.SetTriangles(dirtTris, 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { edge ? edge : dirt, dirt };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void Quad(List<int> tris, int a0, int a1, int b0, int b1)
    {
        tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
    }

    static List<Vector2> Resample(Vector2[] src, float step)
    {
        var res = new List<Vector2>();
        for (int i = 0; i < src.Length - 1; i++)
        {
            var p0 = src[Mathf.Max(i - 1, 0)]; var p1 = src[i]; var p2 = src[i + 1]; var p3 = src[Mathf.Min(i + 2, src.Length - 1)];
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
            for (int k = 0; k < n; k++)
            {
                float t = (float)k / n;
                res.Add(0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t));
            }
        }
        res.Add(src[src.Length - 1]);
        return res;
    }

    static float DistSeg(Vector2 p, Vector2 a, Vector2 b, out Vector2 nearest)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        nearest = a + ab * t; return Vector2.Distance(p, nearest);
    }

    static float RoadDist(Vector2 p) { NearestRoadPoint(p, out float d); return d; }

    static Vector2 NearestRoadPoint(Vector2 p, out float dist)
    {
        float best = float.MaxValue; Vector2 bp = p;
        foreach (var r in Roads)
            for (int i = 0; i < r.Length - 1; i++)
            {
                float d = DistSeg(p, r[i], r[i + 1], out var n);
                if (d < best) { best = d; bp = n; }
            }
        dist = best; return bp;
    }

    // ------------------------------------------------------------------ mountains
    static void ArrangeMountains(List<Transform> existing, Transform parent)
    {
        // ring just outside the terrain edge: heavy on the north and east like the design's brown border
        var spots = new (Vector2 pos, float yaw, float scale)[]
        {
            (new Vector2(-75, Z1 + 22), 10, 0.75f), (new Vector2(0, Z1 + 30), 350, 0.85f), (new Vector2(75, Z1 + 22), 20, 0.75f),
            (new Vector2(X1 + 28, 30), 100, 0.8f), (new Vector2(X1 + 28, -55), 80, 0.8f),
            (new Vector2(X0 - 24, 30), 270, 0.7f), (new Vector2(X0 - 24, -60), 250, 0.7f),
            (new Vector2(-60, Z0 - 22), 190, 0.7f), (new Vector2(40, Z0 - 22), 170, 0.7f),
        };
        string[] names = { "Mountain_1", "Mountain_2", "Mountain_3", "Mountain_4" };
        for (int i = 0; i < spots.Length; i++)
        {
            Transform m = i < existing.Count ? existing[i] : ((GameObject)PrefabUtility.InstantiatePrefab(Prefab(names[i % 4]), parent)).transform;
            m.name = "Border mountain " + (i + 1);
            m.position = new Vector3(spots[i].pos.x, -1.5f, spots[i].pos.y);
            m.rotation = Quaternion.Euler(0, spots[i].yaw, 0);
            m.localScale = Vector3.one * spots[i].scale;
        }
    }

    static void DestroyChildren(string rootName)
    {
        var g = GameObject.Find(rootName);
        if (!g) return;
        for (int i = g.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(g.transform.GetChild(i).gameObject);
    }

    static void DestroyNamed(string n)
    {
        var g = GameObject.Find(n);
        if (g) Object.DestroyImmediate(g);
    }
}
#endif
