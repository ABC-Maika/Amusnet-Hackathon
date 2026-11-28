using System.Text.RegularExpressions;
using Amusnet.Mess;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The TNT cart: a horse hauls a cart of dynamite around the village. Houses, trees and bridges are marked destructible.
public static class VillageTntSetup
{
    const string CartPrefabPath = "Assets/Prefabs/TNT Cart.prefab";

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
        var scene = EditorSceneManager.GetActiveScene();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CartPrefabPath);
        if (!prefab)
        {
            Debug.LogError("Missing TNT cart prefab at " + CartPrefabPath);
            return;
        }

        int houses = 0, trees = 0, bridges = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.gameObject.scene != scene || t.parent == null) continue;
            string n = t.name;
            if (t.GetComponentInChildren<Renderer>() == null) continue;
            if (Regex.IsMatch(n, @"^(Bridge [ABC])$")) { Mark(t.gameObject, Destructible.Kind.Bridge); bridges++; }
            else if (Regex.IsMatch(n, @" house( \d+| \(\d+\))?$")) { Mark(t.gameObject, Destructible.Kind.House); houses++; }
            else if (Regex.IsMatch(n, @"^(Pine )?Tree(_\d+| \d+| \(\d+\))?$")) { Mark(t.gameObject, Destructible.Kind.Tree); trees++; }
        }
        Debug.Log($"Destructibles: {houses} houses, {trees} trees, {bridges} bridges");

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<TntCart>()) Object.DestroyImmediate(root);
        }
        PrefabUtility.InstantiatePrefab(prefab, scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
