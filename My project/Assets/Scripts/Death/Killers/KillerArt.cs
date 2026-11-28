using System;
using System.Collections;
using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Placeholder art for the killers: coloured primitives without colliders, plus a helper that runs
    /// short kill animations in unscaled time (the death sequence slows time right after the strike).
    /// </summary>
    public static class KillerArt
    {
        public static Transform Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale,
                                     Color color, Vector3 euler = default, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();
            var col = go.GetComponent<Collider>();
            if (col) UnityEngine.Object.Destroy(col);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = scale;
            go.GetComponent<Renderer>().material.color = color;
            return t;
        }

        public static Transform Pivot(Transform parent, Vector3 localPos, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        public static void Glow(Transform part, Color color, float intensity)
        {
            var mat = part.GetComponent<Renderer>().material;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
        }

        /// <summary>Runs step(t) for t = 0..1 over seconds of unscaled time, holding while paused.</summary>
        public static IEnumerator Tween(float seconds, Action<float> step)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                while (Butterfly.UI.PauseMenu.IsPaused) yield return null;
                step(t / seconds);
                yield return null;
            }
            step(1f);
        }

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        public static float EaseIn(float t) => t * t * t;
    }
}
