using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Main-menu camera: circles slowly high above the village. FlyToPlayer() swoops it down behind the
/// player and then hands over, after which it simply follows the player until a real camera rig replaces it.
/// Put it on the scene's main camera and assign the player.
/// </summary>
public class MenuCamera : MonoBehaviour
{
    public static MenuCamera Instance { get; private set; }

    [SerializeField] Transform player;

    [Header("Menu orbit")]
    [SerializeField] Vector3 orbitCenter = Vector3.zero;
    [SerializeField] float orbitRadius = 48f;
    [SerializeField] float orbitHeight = 34f;
    [SerializeField] float orbitDegreesPerSecond = 4f;

    [Header("Swoop to player")]
    [Tooltip("Where the camera ends up, in the player's local space.")]
    [SerializeField] Vector3 followOffset = new(0f, 1.6f, -5f);
    [SerializeField] float flyDuration = 2.4f;
    [Tooltip("How high the swoop arcs above the straight line, in metres.")]
    [SerializeField] float flyArc = 6f;

    float angle;
    bool flying, following;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // After Restart the scene skips the menu, so start right behind the player.
        if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Menu) following = true;
    }

    void LateUpdate()
    {
        if (flying) return;
        if (following && player != null)
        {
            var (pos, rot) = FollowPose();
            transform.SetPositionAndRotation(pos, rot);
            return;
        }

        angle += orbitDegreesPerSecond * Time.unscaledDeltaTime;
        var offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, orbitHeight, -orbitRadius);
        transform.position = orbitCenter + offset;
        transform.LookAt(orbitCenter);
    }

    /// <summary>Swoops down behind the player, then calls <paramref name="onArrived"/>.</summary>
    public void FlyToPlayer(Action onArrived)
    {
        if (player == null || flying || following)
        {
            following = player != null;
            onArrived?.Invoke();
            return;
        }
        StartCoroutine(Fly(onArrived));
    }

    IEnumerator Fly(Action onArrived)
    {
        flying = true;
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / flyDuration)
        {
            float e = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; // ease in-out cubic
            var (toPos, toRot) = FollowPose();
            transform.SetPositionAndRotation(
                Vector3.Lerp(fromPos, toPos, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * flyArc),
                Quaternion.Slerp(fromRot, toRot, e));
            yield return null;
        }
        flying = false;
        following = true;
        onArrived?.Invoke();
    }

    (Vector3, Quaternion) FollowPose()
    {
        Vector3 pos = player.TransformPoint(followOffset);
        Vector3 lookAt = player.position + Vector3.up * 0.5f;
        return (pos, Quaternion.LookRotation(lookAt - pos));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(orbitCenter + Vector3.up * orbitHeight, 1f);
        Gizmos.DrawLine(orbitCenter, orbitCenter + Vector3.up * orbitHeight);
    }
}
