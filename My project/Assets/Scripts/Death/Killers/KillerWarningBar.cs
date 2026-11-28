using Butterfly.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Butterfly.Death
{
    /// <summary>
    /// Small world-space "loading bar" above a killer's head that fills while it charges an attack.
    /// Created automatically by ProximityKiller (Show Warning Bar). It is not parented to the killer so it
    /// keeps a constant size and always faces the camera.
    /// </summary>
    public class KillerWarningBar : MonoBehaviour
    {
        public ProximityKiller killer;
        public Vector3 localOffset = new Vector3(0f, 1.1f, 0f);
        public float worldWidth = 1.3f;

        CanvasGroup group;
        Image fill;
        Text label;
        Transform cam;

        const float W = 260f, H = 44f; // canvas units, scaled down to worldWidth

        public static KillerWarningBar Create(ProximityKiller owner, Vector3 offset, float width)
        {
            var go = new GameObject("WarningBar_" + owner.name);
            var bar = go.AddComponent<KillerWarningBar>();
            bar.killer = owner;
            bar.localOffset = offset;
            bar.worldWidth = width;
            bar.Build();
            return bar;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(W, H);
            rt.localScale = Vector3.one * (worldWidth / W);
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            // dark rounded frame
            var frame = Child("Frame", rt);
            frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero;
            var fi = frame.gameObject.AddComponent<Image>();
            fi.sprite = DeathHud.Rounded(); fi.type = Image.Type.Sliced; fi.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            fi.raycastTarget = false;

            // fill
            var fr = Child("Fill", rt);
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(6, 6); fr.offsetMax = new Vector2(-6, -6);
            fill = fr.gameObject.AddComponent<Image>();
            fill.sprite = DeathHud.White();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.raycastTarget = false;

            // "!" label
            var lr = Child("Label", rt);
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            label = lr.gameObject.AddComponent<Text>();
            label.text = "!";
            label.fontSize = 30;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.raycastTarget = false;
        }

        static RectTransform Child(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        void LateUpdate()
        {
            if (killer == null) { Destroy(gameObject); return; }
            if (cam == null && Camera.main != null) cam = Camera.main.transform;

            transform.position = killer.transform.TransformPoint(localOffset);
            if (cam != null) transform.rotation = cam.rotation;

            float p = killer.ChargeProgress;
            bool charging = p > 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, charging ? 1f : 0f, 6f * Time.unscaledDeltaTime);
            if (!charging) return;

            fill.fillAmount = p;
            Color c = p < 0.5f ? Color.Lerp(new Color(0.4f, 0.9f, 0.3f), new Color(1f, 0.85f, 0.2f), p * 2f)
                               : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.15f, 0.1f), (p - 0.5f) * 2f);
            // pulse during the final stretch
            if (p > 0.75f) c = Color.Lerp(c, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 16f)));
            fill.color = c;
            label.color = p > 0.75f ? new Color(1f, 0.3f, 0.25f) : Color.white;
        }
    }
}
