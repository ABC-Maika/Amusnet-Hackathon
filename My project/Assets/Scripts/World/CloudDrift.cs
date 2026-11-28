using UnityEngine;

/// <summary>Drifts a cloud along a direction and wraps it around when it leaves the area, so the sky never empties.</summary>
public class CloudDrift : MonoBehaviour
{
    [SerializeField] Vector3 velocity = new(1.2f, 0f, 0.4f);
    [Tooltip("Clouds farther than this from the world origin (on the ground plane) reappear on the other side.")]
    [SerializeField] float wrapRadius = 90f;

    void Update()
    {
        var p = transform.position + velocity * Time.deltaTime;
        var flat = new Vector2(p.x, p.z);
        if (flat.magnitude > wrapRadius)
        {
            flat = -flat.normalized * (wrapRadius - 1f);
            p = new Vector3(flat.x, p.y, flat.y);
        }
        transform.position = p;
    }
}
