using UnityEngine;
using UnityEngine.UIElements;

namespace ButterflyUI
{
    /// <summary>A ring that fills clockwise and shifts from LowColor to HighColor, easing toward each new Value.</summary>
    [UxmlElement]
    public partial class RadialMeter : VisualElement
    {
        float target, shown;
        IVisualElementScheduledItem tick;

        [UxmlAttribute]
        public float Value
        {
            get => target;
            set
            {
                target = Mathf.Clamp01(value);
                tick?.Resume();
            }
        }

        [UxmlAttribute] public Color TrackColor { get; set; } = new Color(0.05f, 0.09f, 0.06f, 0.85f);
        [UxmlAttribute] public Color LowColor { get; set; } = new Color(0.56f, 0.82f, 0.44f);
        [UxmlAttribute] public Color HighColor { get; set; } = new Color(0.9f, 0.28f, 0.3f);
        [UxmlAttribute] public float Thickness { get; set; } = 11f;

        public RadialMeter()
        {
            generateVisualContent += Draw;
            RegisterCallback<AttachToPanelEvent>(_ => tick = schedule.Execute(Step).Every(16));
            RegisterCallback<DetachFromPanelEvent>(_ => tick?.Pause());
        }

        void Step()
        {
            shown = Mathf.Abs(target - shown) < 0.001f ? target : Mathf.Lerp(shown, target, 0.15f);
            MarkDirtyRepaint();
            if (shown == target) tick.Pause();
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1f || h < 1f) return;
            var p = ctx.painter2D;
            var center = new Vector2(w * 0.5f, h * 0.5f);
            float radius = Mathf.Min(w, h) * 0.5f - Thickness * 0.5f;

            p.fillColor = TrackColor;
            p.BeginPath();
            p.Arc(center, radius + Thickness * 0.5f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();

            p.lineWidth = Thickness;
            p.lineCap = LineCap.Round;
            p.strokeColor = new Color(1f, 1f, 1f, 0.1f);
            p.BeginPath();
            p.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Stroke();

            if (shown <= 0.001f) return;
            p.strokeColor = Color.Lerp(LowColor, HighColor, shown);
            p.BeginPath();
            p.Arc(center, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * shown));
            p.Stroke();
        }
    }
}
