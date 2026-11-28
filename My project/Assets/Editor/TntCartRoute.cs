using System.Collections.Generic;
using Amusnet.Mess;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sets the TNT cart up as one physics object (Rigidbody + collider, horse and crates as children) and bakes its route from
// the village's dirt roads, so it drives the real paths and crosses all three bridges. Re-runnable.
public static class TntCartRoute
{
    const string PrefabPath = "Assets/Prefabs/TNT Cart.prefab";

    [MenuItem("Tools/Village/Setup TNT Cart (road route + rigidbody)")]
    public static void Setup()
    {
        var cart = Object.FindAnyObjectByType<TntCart>();
        if (!cart) { Debug.LogError("No TntCart in the scene"); return; }

        var roads = new List<List<Vector3>>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mf.name != "Road" || !mf.sharedMesh) continue;
            var v = mf.sharedMesh.vertices;
            var line = new List<Vector3>();
            for (int i = 0; i + 3 < v.Length; i += 4)
                line.Add(mf.transform.TransformPoint((v[i] + v[i + 1] + v[i + 2] + v[i + 3]) * 0.25f));
            roads.Add(line);
        }
        if (roads.Count < 5) { Debug.LogError("Expected 5 roads, found " + roads.Count); return; }

        // Two long east-west roads (north bank / south bank) and three north-south roads over bridges A, B, C.
        var horizontal = new List<List<Vector3>>();
        var vertical = new List<List<Vector3>>();
        foreach (var r in roads)
        {
            float dx = Mathf.Abs(r[r.Count - 1].x - r[0].x), dz = Mathf.Abs(r[r.Count - 1].z - r[0].z);
            (dx > dz ? horizontal : vertical).Add(r);
        }
        horizontal.Sort((a, b) => Avg(a, false).CompareTo(Avg(b, false)));  // [0] south bank, [1] north bank
        vertical.Sort((a, b) => Avg(a, true).CompareTo(Avg(b, true)));      // west (A), centre (B), east (C)
        var south = horizontal[0]; var north = horizontal[1];
        var A = vertical[0]; var B = vertical[1]; var C = vertical[2];

        var walk = new List<Vector3>();
        Append(walk, A, Nearest(A, south), Nearest(A, north));          // up over bridge A
        Append(walk, north, Nearest(north, A), Nearest(north, B));      // east along the north road
        Append(walk, B, Nearest(B, north), Nearest(B, south));          // down over bridge B
        Append(walk, south, Nearest(south, B), Nearest(south, C));      // east along the south road
        Append(walk, C, Nearest(C, south), Nearest(C, north));          // up over bridge C (the weir bridge)

        var route = Resample(walk, 2f);

        var stops = new List<Vector3>();
        foreach (var n in new[] { "Bridge A", "Bridge B", "Bridge C" })
        {
            var go = GameObject.Find(n);
            if (go) stops.Add(go.transform.position);
        }

        var so = new SerializedObject(cart);
        var rp = so.FindProperty("route");
        rp.arraySize = route.Count;
        for (int i = 0; i < route.Count; i++) rp.GetArrayElementAtIndex(i).vector3Value = route[i];
        var sp = so.FindProperty("stopPoints");
        sp.arraySize = stops.Count;
        for (int i = 0; i < stops.Count; i++) sp.GetArrayElementAtIndex(i).vector3Value = stops[i];
        so.FindProperty("speed").floatValue = 4f;
        so.FindProperty("dwell").floatValue = 6f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // One physics object: horse and crates stay children, the root carries a kinematic Rigidbody and a collider.
        var root = cart.gameObject;
        var horse = root.transform.Find("Horse");
        if (horse)
        {
            foreach (var c in horse.GetComponents<MonoBehaviour>()) if (c.GetType().Name == "CreatureMover") Object.DestroyImmediate(c);
            var cc = horse.GetComponent<CharacterController>();
            if (cc) Object.DestroyImmediate(cc);
        }
        var rb = root.TryGetComponent<Rigidbody>(out var existing) ? existing : root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.mass = 900f;

        var colT = root.transform.Find("Cart Collider");
        if (!colT)
        {
            colT = new GameObject("Cart Collider").transform;
            colT.SetParent(root.transform, false);
        }
        var box = colT.TryGetComponent<BoxCollider>(out var existingBox) ? existingBox : colT.gameObject.AddComponent<BoxCollider>();
        var b = LocalBounds(root.transform, root.transform.Find("Cart Model"));
        box.center = b.center;
        box.size = b.size;

        root.transform.position = route[0];
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        System.IO.Directory.CreateDirectory("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();
        Debug.Log($"TNT cart: route {route.Count} points, {stops.Count} stops, prefab {PrefabPath}");
    }

    static float Avg(List<Vector3> l, bool x)
    {
        float s = 0f;
        foreach (var p in l) s += x ? p.x : p.z;
        return s / l.Count;
    }

    static int Nearest(List<Vector3> line, List<Vector3> other)
    {
        float best = float.MaxValue; int bi = 0;
        for (int i = 0; i < line.Count; i++)
            foreach (var q in other)
            {
                float d = (new Vector2(line[i].x, line[i].z) - new Vector2(q.x, q.z)).sqrMagnitude;
                if (d < best) { best = d; bi = i; }
            }
        return bi;
    }

    static void Append(List<Vector3> walk, List<Vector3> line, int from, int to)
    {
        int step = to >= from ? 1 : -1;
        for (int i = from; ; i += step)
        {
            walk.Add(line[i]);
            if (i == to) break;
        }
    }

    // Even spacing along the polyline; straight links bridge the gaps where the road meshes stop at the creek.
    static List<Vector3> Resample(List<Vector3> pts, float spacing)
    {
        var res = new List<Vector3> { pts[0] };
        float carry = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            float len = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            if (len < 0.001f) continue;
            float d = spacing - carry;
            while (d <= len)
            {
                res.Add(Vector3.Lerp(a, b, d / len));
                d += spacing;
            }
            carry = len - (d - spacing);
        }
        res.Add(pts[pts.Count - 1]);
        return res;
    }

    static Bounds LocalBounds(Transform root, Transform model)
    {
        bool any = false;
        Bounds bb = default;
        if (!model) return new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2f, 1.6f, 3.6f));
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.sharedMesh || mf.name.EndsWith("Crate")) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = root.InverseTransformPoint(mf.transform.TransformPoint(c));
                if (!any) { bb = new Bounds(p, Vector3.zero); any = true; } else bb.Encapsulate(p);
            }
        }
        bb.Expand(new Vector3(0f, 1.2f, 0f)); bb.center += new Vector3(0f, 0.6f, 0f); // include the load stacked on top
        return bb;
    }
}
