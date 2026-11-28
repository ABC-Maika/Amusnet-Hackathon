using System.Collections.Generic;
using UnityEngine;

/// <summary>Walks a villager (or cart) along waypoints with a bouncy gait, then shrinks away at the end. Unscaled time.</summary>
public class WinWalker : MonoBehaviour
{
    const float PopTime = 0.25f;

    readonly List<Vector3> points = new();
    float delay, speed, t, travelled, bobHeight;
    Transform body;
    Vector3 fullScale;

    /// <param name="body">Child that bobs and tilts; the root only moves along the ground.</param>
    public static void Add(GameObject go, Transform body, IEnumerable<Vector3> route, float delay, float speed, float bobHeight)
    {
        var w = go.AddComponent<WinWalker>();
        w.points.AddRange(route);
        w.delay = delay;
        w.speed = speed;
        w.bobHeight = bobHeight;
        w.body = body;
        w.fullScale = go.transform.localScale;
        go.transform.position = w.points[0];
        go.transform.localScale = Vector3.zero;
        if (w.points.Count > 1) w.Face(w.points[1] - w.points[0]);
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (t < delay) return;
        float active = t - delay;

        float total = PathLength();
        travelled = Mathf.Min(total, travelled + speed * Time.unscaledDeltaTime);
        var (pos, dir) = Sample(travelled);
        transform.position = pos;
        Face(dir);

        float remaining = (total - travelled) / Mathf.Max(0.01f, speed);
        float s = Mathf.Min(WinGrow.EaseOutBack(Mathf.Clamp01(active / PopTime)), Mathf.Clamp01(remaining / PopTime));
        transform.localScale = fullScale * s;

        if (body != null)
        {
            float phase = active * speed * 2.2f;
            body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * bobHeight);
            body.localRotation = Quaternion.Euler(8f, 0f, Mathf.Sin(phase) * 7f);
        }
        if (travelled >= total) gameObject.SetActive(false);
    }

    void Face(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir);
    }

    float PathLength()
    {
        float len = 0f;
        for (int i = 1; i < points.Count; i++) len += Vector3.Distance(points[i - 1], points[i]);
        return len;
    }

    (Vector3, Vector3) Sample(float distance)
    {
        for (int i = 1; i < points.Count; i++)
        {
            Vector3 a = points[i - 1], b = points[i];
            float seg = Vector3.Distance(a, b);
            if (distance <= seg || i == points.Count - 1)
                return (Vector3.Lerp(a, b, seg > 0f ? Mathf.Clamp01(distance / seg) : 1f), b - a);
            distance -= seg;
        }
        return (points[^1], Vector3.forward);
    }
}
