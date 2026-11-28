using UnityEngine;

/// <summary>Scales a piece of the win sequence up from nothing after a delay, with a little overshoot. Unscaled time.</summary>
public class WinGrow : MonoBehaviour
{
    float delay, duration, t;
    Vector3 target;

    public static WinGrow Add(GameObject go, float delay, float duration)
    {
        var g = go.AddComponent<WinGrow>();
        g.target = go.transform.localScale;
        g.delay = delay;
        g.duration = Mathf.Max(0.01f, duration);
        go.transform.localScale = Vector3.zero;
        return g;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01((t - delay) / duration);
        transform.localScale = target * EaseOutBack(k);
        if (k >= 1f) enabled = false;
    }

    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
