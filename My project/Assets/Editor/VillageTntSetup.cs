using System.Text.RegularExpressions;
using Amusnet.Mess;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The TNT cart: a horse hauls a cart of dynamite around the village. Houses, trees and bridges are marked destructible.
public static class VillageTntSetup
{
    static readonly Vector3[] Stops =
    {
        new Vector3(-22f, 0f, -9f), new Vector3(-7.7f, 0f, -9f), new Vector3(-7.7f, 0f, -19.6f), new Vector3(-7.7f, 0f, -9f),
        new Vector3(25f, 0f, -9f), new Vector3(55f, 0f, -9f), new Vector3(66.6f, 0f, -9f), new Vector3(66.6f, 0f, -17.8f),
    };

    public static Destructible Mark(GameObject go, Destructible.Kind kind)
    {
        var d = go.GetComponent<Destructible>();
        if (!d) d = go.AddComponent<Destructible>();
        var so = new SerializedObject(d);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.ApplyModifiedPropertiesWithoutUndo();
        return d;
    }

    [MenuItem("Tools/Village/Build TNT Cart Scenario")]
    public static void Build()
    {
        var old = GameObject.Find("TNT Cart");
        if (old) Object.DestroyImmediate(old);

        int houses = 0, trees = 0, bridges = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!t.gameObject.scene.IsValid() || t.parent == null) continue;
            string n = t.name;
            if (t.GetComponentInChildren<Renderer>() == null) continue;
            if (Regex.IsMatch(n, @"^(Bridge [ABC])$")) { Mark(t.gameObject, Destructible.Kind.Bridge); bridges++; }
            else if (Regex.IsMatch(n, @" house( \d+| \(\d+\))?$")) { Mark(t.gameObject, Destructible.Kind.House); houses++; }
            else if (Regex.IsMatch(n, @"^(Pine )?Tree(_\d+| \d+| \(\d+\))?$")) { Mark(t.gameObject, Destructible.Kind.Tree); trees++; }
        }
        Debug.Log($"Destructibles: {houses} houses, {trees} trees, {bridges} bridges");

        var terrain = Terrain.activeTerrain;
        var stops = new Vector3[Stops.Length];
        for (int i = 0; i < stops.Length; i++)
        {
            stops[i] = Stops[i];
            var o = new Vector3(Stops[i].x, 30f, Stops[i].z);
            stops[i].y = Physics.Raycast(o, Vector3.down, out var h, 60f) ? h.point.y : terrain.SampleHeight(Stops[i]) + terrain.transform.position.y;
        }

        var root = new GameObject("TNT Cart");
        root.transform.position = stops[0];
        var cartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Props/Wooden Cart/Wooden Cart.fbx");
        var cart = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab, root.transform);
        cart.name = "Cart Model";
        cart.transform.localPosition = Vector3.zero;
        cart.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // the model's shafts point to -Z; the rig drives toward +Z
        foreach (var r in cart.GetComponentsInChildren<Transform>())
            if (r.name.EndsWith("Crate")) r.gameObject.SetActive(false);

        var horsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Animals_FREE/Prefabs/Horse_001.prefab");
        var horse = (GameObject)PrefabUtility.InstantiatePrefab(horsePrefab, root.transform);
        horse.name = "Horse";
        horse.transform.localPosition = new Vector3(0f, 0f, 4.1f);
        horse.transform.localRotation = Quaternion.identity;
        foreach (var c in horse.GetComponentsInChildren<MonoBehaviour>()) Object.DestroyImmediate(c);
        var cc = horse.GetComponent<CharacterController>();
        if (cc) Object.DestroyImmediate(cc);

        var red = TntMaterial();
        var hide = new System.Collections.Generic.List<Transform> { cart.transform, horse.transform };
        var crates = new[] { new Vector3(-0.38f, 1.45f, -0.5f), new Vector3(0.38f, 1.45f, -0.5f), new Vector3(-0.38f, 1.45f, -1.35f), new Vector3(0.38f, 1.45f, -1.35f), new Vector3(0f, 2.28f, -0.92f) };
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (var cp in crates)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "TNT Crate";
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = cp;
            box.transform.localScale = new Vector3(0.78f, 0.82f, 0.78f);
            box.GetComponent<Renderer>().sharedMaterial = red;
            foreach (var side in new[] { 90f, -90f })
            {
                var label = new GameObject("TNT Label");
                label.transform.SetParent(box.transform, false);
                label.transform.localPosition = new Vector3(side > 0 ? 0.51f : -0.51f, 0f, 0f);
                label.transform.localRotation = Quaternion.Euler(0f, side, 0f);
                label.transform.localScale = Vector3.one * 0.05f;
                var tm = label.AddComponent<TextMesh>();
                tm.text = "TNT"; tm.font = font; tm.fontSize = 64; tm.characterSize = 1f;
                tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white;
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            hide.Add(box.transform);
        }

        var cartComp = root.AddComponent<TntCart>();
        var so = new SerializedObject(cartComp);
        var sp = so.FindProperty("stops");
        sp.arraySize = stops.Length;
        for (int i = 0; i < stops.Length; i++) sp.GetArrayElementAtIndex(i).vector3Value = stops[i];
        so.FindProperty("horse").objectReferenceValue = horse.transform;
        var hp = so.FindProperty("hideOnBoom");
        hp.arraySize = hide.Count;
        for (int i = 0; i < hide.Count; i++) hp.GetArrayElementAtIndex(i).objectReferenceValue = hide[i];
        so.FindProperty("fuseLocal").vector3Value = new Vector3(0f, 2.8f, -0.92f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static Material TntMaterial()
    {
        const string path = "Assets/Materials/Village/TNT Crate.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.78f, 0.12f, 0.1f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
