using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Moves the playable butterfly rig (player, follow camera, brain camera) from ButterflyPreview into Village.
public static class VillageButterflyImport
{
    const string PreviewPath = "Assets/Scenes/Old_Scenes/ButterflyPreview.unity";
    static readonly string[] Rig = { "Butterfly Player", "Butterfly Follow Camera", "Preview Camera" };

    [MenuItem("Tools/Village/Import Butterfly Rig")]
    public static void Import()
    {
        var village = SceneManager.GetActiveScene();
        foreach (var n in Rig) { var e = GameObject.Find(n); if (e && e.scene == village) Object.DestroyImmediate(e); }

        var preview = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Additive);
        foreach (var root in preview.GetRootGameObjects())
            if (System.Array.IndexOf(Rig, root.name) >= 0)
                SceneManager.MoveGameObjectToScene(root, village);
        EditorSceneManager.CloseScene(preview, true);
        SceneManager.SetActiveScene(village);

        // Retire the old village camera: the Preview Camera (with Cinemachine Brain) takes over.
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (cam.gameObject.name != "Preview Camera" && cam.gameObject.scene == village)
                cam.gameObject.SetActive(false);
        foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
            if (l.gameObject.name != "Preview Camera") l.enabled = false;

        var player = GameObject.Find("Butterfly Player");
        var spawn = GameObject.Find("PlayerSpawn");
        if (player && spawn)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.SetPositionAndRotation(spawn.transform.position + Vector3.up * 3f, spawn.transform.rotation);
            cc.enabled = true;
        }
        EditorSceneManager.MarkSceneDirty(village);
    }
}
