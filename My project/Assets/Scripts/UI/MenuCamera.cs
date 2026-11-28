using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Main-menu camera: a Cinemachine camera that circles slowly high above the village.
/// FlyToPlayer() activates the gameplay camera, and the CinemachineBrain blends the shot down to the
/// player; when the blend finishes the run starts. Put it on its own CinemachineCamera (not the main
/// camera) and assign the gameplay camera.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class MenuCamera : MonoBehaviour
{
    public static MenuCamera Instance { get; private set; }

    [Tooltip("The player's follow camera. It stays off on the menu and takes over on Play.")]
    [SerializeField] CinemachineCamera gameplayCamera;

    [Header("Menu orbit")]
    [SerializeField] Vector3 orbitCenter = Vector3.zero;
    [SerializeField] float orbitRadius = 48f;
    [SerializeField] float orbitHeight = 34f;
    [SerializeField] float orbitDegreesPerSecond = 4f;

    [Header("Swoop to player")]
    [SerializeField] float flyDuration = 2.4f;

    float angle;
    bool flying;

    void Awake()
    {
        Instance = this;
        if (gameplayCamera != null) gameplayCamera.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // After Restart the scene skips the menu: cut straight to the gameplay camera.
        if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Menu) HandOver();
    }

    void LateUpdate()
    {
        angle += orbitDegreesPerSecond * Time.unscaledDeltaTime;
        var offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, orbitHeight, -orbitRadius);
        transform.position = orbitCenter + offset;
        transform.LookAt(orbitCenter);
    }

    /// <summary>Blends down to the player's camera, then calls <paramref name="onArrived"/>.</summary>
    public void FlyToPlayer(Action onArrived)
    {
        if (gameplayCamera == null || flying)
        {
            onArrived?.Invoke();
            return;
        }
        StartCoroutine(Fly(onArrived));
    }

    IEnumerator Fly(Action onArrived)
    {
        flying = true;
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, flyDuration);
        gameplayCamera.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(flyDuration);
        HandOver();
        onArrived?.Invoke();
    }

    void HandOver()
    {
        if (gameplayCamera != null) gameplayCamera.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(orbitCenter + Vector3.up * orbitHeight, 1f);
        Gizmos.DrawLine(orbitCenter, orbitCenter + Vector3.up * orbitHeight);
    }
}
