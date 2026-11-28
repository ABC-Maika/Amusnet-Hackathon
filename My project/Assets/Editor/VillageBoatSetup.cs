using Amusnet.Mess;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Wind and the fishing boat: two moored boats on the creek, a gust frees one and the current carries it bow-first
// downstream into the bridge, where it snaps in two. Guide rings lead the player in; fish patrol the creek.
public static class VillageBoatSetup
{
    const string GenDir = "Assets/Generated/Boats";

    [MenuItem("Tools/Village/Build Boat Drift Scenario")]
    public static void Build()
    {
        var fisher = GameObject.Find("Moored rowboat");
        if (!fisher) { Debug.LogError("No 'Moored rowboat' in the open scene."); return; }
        foreach (var n in new[] { "Cargo boat", "Mooring Post A", "Mooring Post B", "Guide Rings A", "Guide Rings B", "River Fish", "Creek Lane", "Rescue Crew A", "Rescue Crew B" })
        {
            var old = GameObject.Find(n);
            if (old) Object.DestroyImmediate(old);
        }
        for (int i = fisher.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(fisher.transform.GetChild(0).gameObject);
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "Boats");

        var cargo = Object.Instantiate(fisher, fisher.transform.parent);
        cargo.name = "Cargo boat";
        cargo.transform.position = fisher.transform.position + new Vector3(15f, 0f, 0.4f);
        cargo.transform.rotation = Quaternion.Euler(0f, 95f, 0f);

        Physics.SyncTransforms();
        var wind = Object.FindAnyObjectByType<Amusnet.World.WindSystem>();
        var ringMat = RingMaterial();
        var a = Rig(fisher, "A", wind, 30, new[] { "Assets/Characters/Prefabs/Harbor.prefab" }, new[] { new Vector4(-0.2f, 0f, 0f, 0f) }, "Cut the fisherman's rope", ringMat);
        var b = Rig(cargo, "B", wind, 15, new[] { "Assets/Characters/Prefabs/Honey.prefab", "Assets/Prefabs/Interactions/Milk churns.prefab" }, new[] { new Vector4(-0.2f, 0f, 0f, 0f), new Vector4(0.5f, 0f, 0f, 0f) }, "Cut the dairy boat's rope", ringMat);
        BakeLane();
        SpawnFish();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // Samples the creek centre from the terrain: the middle of the water span at each x.
    static void BakeLane()
    {
        var t = Terrain.activeTerrain;
        const float x0 = -44f, step = 2f, wy = -0.81f;
        int n = 59;
        var z = new float[n];
        float last = -19f;
        for (int i = 0; i < n; i++)
        {
            float x = x0 + i * step, lo = float.MaxValue, hi = float.MinValue;
            for (float zz = -34f; zz <= -5f; zz += 0.25f)
            {
                float h = t.SampleHeight(new Vector3(x, 0f, zz)) + t.transform.position.y;
                if (h < wy && Mathf.Abs(zz - last) < 8f) { lo = Mathf.Min(lo, zz); hi = Mathf.Max(hi, zz); }
            }
            z[i] = lo < hi ? (lo + hi) * 0.5f : last;
            last = z[i];
        }
        for (int pass = 0; pass < 2; pass++)
            for (int i = 1; i < n - 1; i++) z[i] = (z[i - 1] + z[i] + z[i + 1]) / 3f;
        var go = new GameObject("Creek Lane");
        var lane = go.AddComponent<RiverLane>();
        var so = new SerializedObject(lane);
        so.FindProperty("startX").floatValue = x0;
        so.FindProperty("step").floatValue = step;
        var arr = so.FindProperty("z");
        arr.arraySize = n;
        for (int i = 0; i < n; i++) arr.GetArrayElementAtIndex(i).floatValue = z[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Material RingMaterial()
    {
        const string path = "Assets/Materials/Village/Guide Ring.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(Shader.Find("Sprites/Default"));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void SpawnFish()
    {
        var root = new GameObject("River Fish");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fish/Fish Albedo.mat");
        // kind, count, model scale, swim speed, stalk speed, leap time, mashes, label
        var kinds = new (string name, int n, float scale, float swim, float stalk, float leap, int mash)[]
        {
            ("Carp", 3, 2.0f, 1.0f, 3.2f, 1.25f, 8),
            ("Perch", 3, 2.6f, 1.8f, 5.0f, 0.8f, 7),
            ("Trout", 3, 2.2f, 1.5f, 4.2f, 1.05f, 10),
        };
        foreach (var k in kinds)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Fish/{k.name}.fbx");
            if (!prefab) { Debug.LogWarning("Missing fish model " + k.name); continue; }
            for (int i = 0; i < k.n; i++)
            {
                var go = new GameObject($"{k.name} {i + 1}");
                go.transform.SetParent(root.transform, false);
                var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                m.name = "Model";
                m.transform.localPosition = Vector3.zero;
                m.transform.localScale = Vector3.one * k.scale;
                if (mat) foreach (var r in m.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                var f = go.AddComponent<RiverFish>();
                var so = new SerializedObject(f);
                so.FindProperty("model").objectReferenceValue = m.transform;
                so.FindProperty("swimSpeed").floatValue = k.swim;
                so.FindProperty("stalkSpeed").floatValue = k.stalk;
                so.FindProperty("leapTime").floatValue = k.leap;
                so.FindProperty("mashesNeeded").intValue = k.mash;
                so.FindProperty("label").stringValue = k.name;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // Splits the hull at `mid` along its long axis with a jagged break. Pieces are centred on `pivot`.
    static Mesh[] SplitHull(Mesh src, bool longX, float mid, Vector3 pivot, float bowSign, string name)
    {
        var v = src.vertices; var nrm = src.normals; var uv = src.uv; var tan = src.tangents;
        var halves = new Mesh[2];
        for (int h = 0; h < 2; h++)
        {
            bool bowSide = h == 0;
            var map = new System.Collections.Generic.Dictionary<int, int>();
            var nv = new System.Collections.Generic.List<Vector3>(); var nn = new System.Collections.Generic.List<Vector3>();
            var nu = new System.Collections.Generic.List<Vector2>(); var nt = new System.Collections.Generic.List<Vector4>();
            var m = new Mesh { name = $"{name} {(bowSide ? "Bow" : "Stern")}" };
            m.subMeshCount = src.subMeshCount;
            var subs = new System.Collections.Generic.List<int[]>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tris = src.GetTriangles(s);
                var outT = new System.Collections.Generic.List<int>();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var c = (v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f;
                    float u = longX ? c.x : c.z, perp = longX ? c.z : c.x;
                    float jag = 0.12f * Mathf.Sin(perp * 7f + 1f) + 0.06f * Mathf.Sin(perp * 17f) + 0.05f * Mathf.Sin(c.y * 9f);
                    bool isBow = bowSign * (u - mid - jag) > 0f;
                    if (isBow != bowSide) continue;
                    for (int j = 0; j < 3; j++)
                    {
                        int o = tris[i + j];
                        if (!map.TryGetValue(o, out int idx))
                        {
                            idx = nv.Count; map[o] = idx;
                            nv.Add(v[o] - pivot);
                            nn.Add(nrm.Length > o ? nrm[o] : Vector3.up);
                            nu.Add(uv.Length > o ? uv[o] : Vector2.zero);
                            if (tan.Length > o) nt.Add(tan[o]);
                        }
                        outT.Add(idx);
                    }
                }
                subs.Add(outT.ToArray());
            }
            m.SetVertices(nv); m.SetNormals(nn); m.SetUVs(0, nu);
            if (nt.Count == nv.Count) m.SetTangents(nt);
            for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, $"{GenDir}/{m.name}.asset");
            halves[h] = m;
        }
        return halves;
    }

    static BoatDrift Rig(GameObject boat, string id, Amusnet.World.WindSystem wind, int points, string[] passengerPaths, Vector4[] offsets, string cutLabel, Material ringMat)
    {
        string postName = "Mooring Post " + id;
        var mf = boat.GetComponent<MeshFilter>();
        var mesh = mf.sharedMesh;
        var lb = mesh.bounds;

        // Find the hull floor by raycasting the hull collider from above.
        var hull = boat.GetComponent<MeshCollider>();
        var wb = boat.GetComponent<Renderer>().bounds;
        var hits = new System.Collections.Generic.List<Vector3>();
        for (int ix = 0; ix <= 20; ix++)
            for (int iz = 0; iz <= 20; iz++)
            {
                var o = new Vector3(Mathf.Lerp(wb.min.x, wb.max.x, ix / 20f), wb.max.y + 1f, Mathf.Lerp(wb.min.z, wb.max.z, iz / 20f));
                if (hull.Raycast(new Ray(o, Vector3.down), out var h, 20f) && h.normal.y > 0.7f) hits.Add(h.point);
            }
        float floorY = float.MaxValue;
        foreach (var h in hits) floorY = Mathf.Min(floorY, h.y);
        var floor = hits.FindAll(h => h.y < floorY + 0.45f);
        Vector3 mid = Vector3.zero;
        foreach (var h in floor) mid += h;
        mid /= Mathf.Max(1, floor.Count);
        mid.y = floorY;
        var local = floor.ConvertAll(h => boat.transform.InverseTransformPoint(h));
        var lmin = new Vector3(1e9f, 1e9f, 1e9f); var lmax = -lmin;
        foreach (var l in local) { lmin = Vector3.Min(lmin, l); lmax = Vector3.Max(lmax, l); }
        bool longX = lb.size.x > lb.size.z;
        var midLocal = boat.transform.InverseTransformPoint(mid);

        // The bow is the narrower end of the hull.
        var verts = mesh.vertices;
        float uMin = 1e9f, uMax = -1e9f;
        foreach (var vt in verts) { float u = longX ? vt.x : vt.z; uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u); }
        float slice = (uMax - uMin) * 0.18f, wLo0 = 1e9f, wLo1 = -1e9f, wHi0 = 1e9f, wHi1 = -1e9f;
        foreach (var vt in verts)
        {
            float u = longX ? vt.x : vt.z, p = longX ? vt.z : vt.x;
            if (u < uMin + slice) { wLo0 = Mathf.Min(wLo0, p); wLo1 = Mathf.Max(wLo1, p); }
            if (u > uMax - slice) { wHi0 = Mathf.Min(wHi0, p); wHi1 = Mathf.Max(wHi1, p); }
        }
        float bowSign = (wHi1 - wHi0) < (wLo1 - wLo0) ? 1f : -1f;
        Vector3 axis = longX ? Vector3.right : Vector3.forward;
        Vector3 bowLocal = axis * bowSign;
        float bowYaw = Mathf.Atan2(bowLocal.x, bowLocal.z) * Mathf.Rad2Deg;
        Debug.Log($"Boat {boat.name}: floorY {floorY:F2}, longX {longX}, bow {bowLocal}, length {uMax - uMin:F2}, mid {midLocal}");

        // Passengers: villagers get the seated/panic/swim poser, cargo is just placed. They face the bow.
        var people = new System.Collections.Generic.List<BoatPassenger>();
        var cargoList = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < passengerPaths.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(passengerPaths[i]);
            if (!prefab) continue;
            var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab, boat.transform);
            bool isCargo = passengerPaths[i].Contains("churns");
            p.name = isCargo ? "Cargo" : "Villager " + i;
            var offset = offsets[i];
            p.transform.localPosition = midLocal + axis * offset.x + Vector3.up * (0.05f + offset.y);
            p.transform.localRotation = Quaternion.Euler(0f, bowYaw + offset.w, 0f);
            p.transform.localScale = Vector3.one * (isCargo ? 0.6f : 1f);
            foreach (var ag in p.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>()) Object.DestroyImmediate(ag);
            foreach (var mb in p.GetComponentsInChildren<MonoBehaviour>()) if (mb) Object.DestroyImmediate(mb);
            if (isCargo) cargoList.Add(p.transform); else people.Add(p.AddComponent<BoatPassenger>());
        }

        // Pre-split hull pieces, hidden until the bridge breaks the boat.
        var pieces = SplitHull(mesh, longX, longX ? midLocal.x : midLocal.z, midLocal, bowSign, boat.name);
        var mats = boat.GetComponent<Renderer>().sharedMaterials;
        var halfObjs = new Transform[2];
        for (int h = 0; h < 2; h++)
        {
            var go = new GameObject(h == 0 ? "Bow Half" : "Stern Half");
            go.transform.SetParent(boat.transform, false);
            go.transform.localPosition = midLocal;
            go.AddComponent<MeshFilter>().sharedMesh = pieces[h];
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            go.SetActive(false);
            halfObjs[h] = go.transform;
        }

        // The bridge downstream of the boat.
        float bridgeX = 1e9f;
        Destructible bridgeDest = null;
        foreach (var bn in new[] { "Bridge A", "Bridge B", "Bridge C" })
        {
            var br = GameObject.Find(bn);
            if (!br || !br.TryGetComponent<Collider>(out var bc)) continue;
            if (bc.bounds.center.x > boat.transform.position.x + 5f && bc.bounds.min.x - 0.2f < bridgeX)
            {
                bridgeX = bc.bounds.min.x - 0.2f;
                bridgeDest = VillageTntSetup.Mark(br, Destructible.Kind.Bridge);
            }
        }

        // Gust zone: a trigger box the butterfly's E gust can target.
        var zone = new GameObject("Boat Gust Zone");
        zone.transform.SetParent(boat.transform, false);
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = lb.center;
        box.size = lb.size * 1.4f;

        // Mooring post on the bank and a rope to the boat.
        var terrain = Terrain.activeTerrain;
        var pos = boat.transform.position + new Vector3(0f, 0f, 3.2f);
        if (terrain) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = postName;
        Object.DestroyImmediate(post.GetComponent<Collider>());
        post.transform.position = pos + Vector3.up * 0.6f;
        post.transform.localScale = new Vector3(0.18f, 0.6f, 0.18f);
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Placeholder/Materials/Wood.mat");
        if (wood) post.GetComponent<Renderer>().sharedMaterial = wood;

        var ropeGo = new GameObject("Rope");
        ropeGo.transform.SetParent(boat.transform, false);
        var rope = ropeGo.AddComponent<LineRenderer>();
        rope.useWorldSpace = true;
        rope.positionCount = 2;
        rope.widthMultiplier = 0.05f;
        rope.sharedMaterial = wood ? wood : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var drift = zone.AddComponent<BoatDrift>();
        var so = new SerializedObject(drift);
        so.FindProperty("boat").objectReferenceValue = boat.transform;
        so.FindProperty("mooring").objectReferenceValue = post.transform;
        so.FindProperty("wind").objectReferenceValue = wind;
        var arr = so.FindProperty("passengers");
        arr.arraySize = people.Count;
        for (int i = 0; i < people.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = people[i];
        var carr = so.FindProperty("cargo");
        carr.arraySize = cargoList.Count;
        for (int i = 0; i < cargoList.Count; i++) carr.GetArrayElementAtIndex(i).objectReferenceValue = cargoList[i];
        so.FindProperty("cutLabel").stringValue = cutLabel;
        so.FindProperty("rope").objectReferenceValue = rope;
        so.FindProperty("messValue").intValue = points;
        so.FindProperty("bowLocal").vector3Value = bowLocal;
        so.FindProperty("halfLength").floatValue = (uMax - uMin) * 0.5f;
        so.FindProperty("bridgeX").floatValue = bridgeX;
        so.FindProperty("bowHalf").objectReferenceValue = halfObjs[0];
        so.FindProperty("sternHalf").objectReferenceValue = halfObjs[1];
        so.FindProperty("swimTo").vector3Value = Vector3.forward; // village side bank
        so.FindProperty("bridge").objectReferenceValue = bridgeDest;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Guide rings from the village side down to the rope.
        var rings = new GameObject("Guide Rings " + id);
        var gr = rings.AddComponent<GuideRings>();
        var rso = new SerializedObject(gr);
        rso.FindProperty("mission").objectReferenceValue = drift;
        rso.FindProperty("ringMaterial").objectReferenceValue = ringMat;
        rso.ApplyModifiedPropertiesWithoutUndo();

        // Villagers who live by the mooring and run to help once the boat breaks loose.
        var crew = new GameObject("Rescue Crew " + id);
        var rc = new SerializedObject(crew.AddComponent<RescueCrew>());
        rc.FindProperty("boat").objectReferenceValue = drift;
        string[] names = id == "A" ? new[] { "Juniper", "Iris" } : new[] { "Mulberry", "Saffron" };
        var pf = rc.FindProperty("prefabs");
        pf.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++) pf.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Characters/Prefabs/{names[i]}.prefab");
        var hm = rc.FindProperty("homes");
        hm.arraySize = 2;
        hm.GetArrayElementAtIndex(0).vector3Value = pos + new Vector3(-2.2f, 0f, 1.6f);
        hm.GetArrayElementAtIndex(1).vector3Value = pos + new Vector3(1.6f, 0f, 2.6f);
        rc.ApplyModifiedPropertiesWithoutUndo();
        return drift;
    }
}
