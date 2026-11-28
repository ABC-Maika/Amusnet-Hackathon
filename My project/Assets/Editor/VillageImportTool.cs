using System.Collections.Generic;
using System.Linq;
using Butterfly.Death;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu: Death > Use Village Map and Butterfly Controller.
/// In the open scene (the Sprint 4 death scene):
///  1. removes the old test terrain and the test butterfly (SimFlyer),
///  2. brings in the village map from Village.unity (terrain, cottages, lanes, creek, props, woods, layout,
///     global volume, sun) and copies its fog / sky / ambient settings,
///  3. brings in "Butterfly Player" (ButterflyController) and "Butterfly Follow Camera" from ButterflyPreview.unity,
///     puts the player on the village PlayerSpawn and drives the Main Camera with a CinemachineBrain,
///  4. moves the death test objects (mess props, frog, predator bird, animals...) to the spawn area, keeping
///     their height above the ground, and points the K-key debug killer at the new butterfly.
/// The source scenes are opened additively and closed without saving, so they are not changed on disk.
/// </summary>
public static class VillageImportTool
{
    const string VillagePath = "Assets/Scenes/Village.unity";
    const string PreviewPath = "Assets/Scenes/Old_Scenes/ButterflyPreview.unity";

    static readonly string[] VillageRoots =
    {
        "Landscape", "Cottages", "Village green and lanes", "Creek crossing",
        "Gardens and village props", "Woodland and hills", "Village Layout", "Global Volume", "Directional Light",
    };
    static readonly string[] PreviewRoots = { "Butterfly Player", "Butterfly Follow Camera" };

    [MenuItem("Death/Use Village Map and Butterfly Controller")]
    static void Run()
    {
        var target = SceneManager.GetActiveScene();
        if (target.path == VillagePath || target.path == PreviewPath)
        {
            EditorUtility.DisplayDialog("Village import", "Open your own scene (e.g. Sprint4 - Death system) first.", "OK");
            return;
        }
        if (Object.FindFirstObjectByType<ButterflyController>() != null)
        {
            EditorUtility.DisplayDialog("Village import", "This scene already has the ButterflyController player. Nothing to do.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Village import",
                $"Replace the terrain in '{target.name}' with the village map and the test butterfly with the real ButterflyController?\n\nThe scene is saved afterwards.",
                "Replace", "Cancel"))
            return;
        if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { target })) return;

        // ---- remember where the old test world was, then remove the old terrain + test butterfly ----
        var oldTerrain = target.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).FirstOrDefault();
        var oldButterfly = Object.FindFirstObjectByType<SimFlyer>();
        Vector3 oldAnchor = oldButterfly != null ? oldButterfly.transform.position : Vector3.zero;

        // relocation list: every remaining world object, with its height above the old ground
        var relocate = new List<(Transform t, float above)>();
        foreach (var root in target.GetRootGameObjects())
        {
            if (oldTerrain != null && root == oldTerrain.transform.root.gameObject) continue;
            if (oldButterfly != null && root == oldButterfly.transform.root.gameObject) continue;
            if (root.GetComponentInChildren<Camera>(true) || root.GetComponentInChildren<Light>(true) || root.GetComponentInChildren<Canvas>(true)) continue;
            if (root.GetComponentInChildren<Renderer>(true) == null) continue; // pure managers (AngerLevels, DeathRig...)

            bool container = root.GetComponent<Renderer>() == null && root.GetComponent<Collider>() == null
                             && root.GetComponent<MonoBehaviour>() == null && root.transform.childCount > 0
                             && PrefabUtility.GetPrefabInstanceHandle(root) == null;
            if (container)
                foreach (Transform c in root.transform) relocate.Add((c, c.position.y - Ground(oldTerrain, c.position)));
            else
                relocate.Add((root.transform, root.transform.position.y - Ground(oldTerrain, root.transform.position)));
        }

        if (oldTerrain != null) Object.DestroyImmediate(oldTerrain.transform.root.gameObject);
        if (oldButterfly != null) Object.DestroyImmediate(oldButterfly.transform.root.gameObject);
        var oldLight = target.GetRootGameObjects().FirstOrDefault(r => r.GetComponent<Light>() != null && r.GetComponent<Light>().type == LightType.Directional);

        // ---- village map ----
        var village = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(village);
        var fog = new FogAndSky();
        SceneManager.SetActiveScene(target);
        fog.Apply();

        bool movedSun = false;
        foreach (var root in village.GetRootGameObjects())
            if (VillageRoots.Contains(root.name))
            {
                SceneManager.MoveGameObjectToScene(root, target);
                if (root.name == "Directional Light") movedSun = true;
            }
        if (movedSun && oldLight != null) Object.DestroyImmediate(oldLight);
        EditorSceneManager.CloseScene(village, true);

        // ---- butterfly player + follow camera ----
        var preview = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Additive);
        foreach (var root in preview.GetRootGameObjects())
            if (PreviewRoots.Contains(root.name)) SceneManager.MoveGameObjectToScene(root, target);
        EditorSceneManager.CloseScene(preview, true);
        SceneManager.SetActiveScene(target);

        var player = Object.FindFirstObjectByType<ButterflyController>();
        var spawn = target.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "PlayerSpawn");
        var terrain = target.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).FirstOrDefault();

        Vector3 spawnPos = spawn != null ? spawn.position : Vector3.zero;
        if (terrain != null) spawnPos.y = Mathf.Max(spawnPos.y, Ground(terrain, spawnPos) + 1.5f);
        if (player != null)
        {
            player.transform.position = spawnPos;
            if (spawn != null) player.transform.rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
        }

        // ---- main camera driven by Cinemachine ----
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            foreach (var chase in cam.GetComponents<SimChaseCamera>()) Object.DestroyImmediate(chase);
            if (cam.GetComponent<CinemachineBrain>() == null) cam.gameObject.AddComponent<CinemachineBrain>();
            if (player != null) cam.transform.SetPositionAndRotation(player.transform.position + new Vector3(0, 3, -6), Quaternion.Euler(20, 0, 0));
        }

        // ---- test objects follow the player to the village ----
        Vector3 offset = new Vector3(spawnPos.x - oldAnchor.x, 0f, spawnPos.z - oldAnchor.z);
        foreach (var (t, above) in relocate)
        {
            if (t == null) continue;
            Vector3 p = t.position + offset;
            p.y = Ground(terrain, p) + above;
            t.position = p;
        }

        // ---- K-key debug killer uses the new butterfly ----
        if (player != null)
            foreach (var dbg in Object.FindObjectsByType<DebugDeathCause>(FindObjectsSortMode.None))
            {
                dbg.butterfly = player.gameObject;
                EditorUtility.SetDirty(dbg);
            }

        if (player != null) Selection.activeGameObject = player.gameObject;
        EditorSceneManager.MarkSceneDirty(target);
        EditorSceneManager.SaveScene(target);
        Debug.Log($"[Village import] Done. Village map + ButterflyController player at {spawnPos}. Moved {relocate.Count} test objects next to the spawn.");
    }

    static float Ground(Terrain terrain, Vector3 world)
    {
        if (terrain == null || terrain.terrainData == null) return 0f;
        return terrain.transform.position.y + terrain.SampleHeight(world);
    }

    /// <summary>Copies the lighting look (fog, skybox, ambient) of the active scene.</summary>
    class FogAndSky
    {
        readonly bool fog = RenderSettings.fog;
        readonly FogMode mode = RenderSettings.fogMode;
        readonly Color fogColor = RenderSettings.fogColor;
        readonly float start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance, density = RenderSettings.fogDensity;
        readonly Material sky = RenderSettings.skybox;
        readonly UnityEngine.Rendering.AmbientMode ambientMode = RenderSettings.ambientMode;
        readonly Color ambSky = RenderSettings.ambientSkyColor, ambEq = RenderSettings.ambientEquatorColor, ambGround = RenderSettings.ambientGroundColor;
        readonly float ambIntensity = RenderSettings.ambientIntensity;

        public void Apply()
        {
            RenderSettings.fog = fog;
            RenderSettings.fogMode = mode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            RenderSettings.fogDensity = density;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambSky;
            RenderSettings.ambientEquatorColor = ambEq;
            RenderSettings.ambientGroundColor = ambGround;
            RenderSettings.ambientIntensity = ambIntensity;
        }
    }
}
