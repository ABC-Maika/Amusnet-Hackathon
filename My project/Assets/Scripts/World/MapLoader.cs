using Amusnet.World;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads the village map scene alongside this (systems) scene, so the map's owner can keep editing it
/// on its own. The map becomes the active scene so its sky, fog and lighting apply; its own camera
/// and any preview butterfly rig are switched off because this scene owns the camera and the player,
/// and the player is moved to the map's spawn point.
/// </summary>
public class MapLoader : MonoBehaviour
{
    [SerializeField] string mapScene = "Village";
    [SerializeField] string spawnPointName = "PlayerSpawn";
    [SerializeField] Transform player;
    [Tooltip("Optional sky to use instead of the map's. Its ground colour should match the map's fog, so the " +
             "empty band past the map edge blends into the haze from the high menu camera.")]
    [SerializeField] Material skyOverride;

    void Awake()
    {
        if (SceneManager.GetSceneByName(mapScene).isLoaded) Prepare(SceneManager.GetSceneByName(mapScene));
        else
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(mapScene, LoadSceneMode.Additive);
        }
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != mapScene) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Prepare(scene);
    }

    void Prepare(Scene map)
    {
        SceneManager.SetActiveScene(map);
        if (skyOverride != null)
        {
            RenderSettings.skybox = skyOverride;
            DynamicGI.UpdateEnvironment();
        }
        var roots = map.GetRootGameObjects();
        foreach (var root in roots)
        {
            // This scene owns the player and the cameras: switch off the map's own preview rig
            // (its butterfly, follow camera, pollen trail and cameras) so there is only one of each.
            if (root.GetComponentInChildren<ButterflyController>(true) || root.GetComponentInChildren<EnvironmentFlightPreview>(true) ||
                root.GetComponentInChildren<CinemachineCamera>(true) || root.GetComponentInChildren<ButterflyPollen>(true))
            {
                root.SetActive(false);
                continue;
            }
            foreach (var cam in root.GetComponentsInChildren<Camera>()) cam.gameObject.SetActive(false);
            foreach (var listener in root.GetComponentsInChildren<AudioListener>()) listener.enabled = false;
        }

        foreach (var root in roots)
        {
            if (player == null) continue;
            var spawn = FindDeep(root.transform, spawnPointName);
            if (spawn == null) continue;
            // A CharacterController overrides transform moves while enabled.
            var motor = player.GetComponent<CharacterController>();
            if (motor != null) motor.enabled = false;
            player.SetPositionAndRotation(spawn.position, spawn.rotation);
            if (motor != null) motor.enabled = true;
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var found = FindDeep(c, name);
            if (found != null) return found;
        }
        return null;
    }
}
