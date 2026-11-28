using UnityEngine;
using UnityEngine.UIElements;

namespace ButterflyUI
{
    /// <summary>A butterfly drawn with vector paths whose wings flap. Size it with USS width and height.</summary>
    [UxmlElement]
    public partial class ButterflyLogo : VisualElement
    {
        [UxmlAttribute] public Color UpperWingColor { get; set; } = new Color(0.96f, 0.65f, 0.16f);
        [UxmlAttribute] public Color LowerWingColor { get; set; } = new Color(0.88f, 0.42f, 0.10f);
        [UxmlAttribute] public Color SpotColor { get; set; } = new Color(1f, 0.95f, 0.82f);
        [UxmlAttribute] public Color BodyColor { get; set; } = new Color(0.16f, 0.10f, 0.05f);
        [UxmlAttribute] public float FlapsPerSecond { get; set; } = 0.9f;

        IVisualElementScheduledItem tick;

        public ButterflyLogo()
        {
            generateVisualContent += Draw;
            RegisterCallback<AttachToPanelEvent>(_ => tick = schedule.Execute(MarkDirtyRepaint).Every(16));
            RegisterCallback<DetachFromPanelEvent>(_ => tick?.Pause());
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1f || h < 1f) return;
            var p = ctx.painter2D;
            var c = new Vector2(w * 0.5f, h * 0.5f);
            float hw = w * 0.5f, hh = h * 0.5f;

            // Wings fold toward the body and open again; a sharp curve makes the downstroke snappy.
            float phase = Mathf.Sin(Time.realtimeSinceStartup * FlapsPerSecond * Mathf.PI * 2f);
            float open = 0.25f + 0.75f * Mathf.Pow(Mathf.Abs(phase), 0.6f);

            foreach (float side in new[] { -1f, 1f })
            {
                float sx = side * open * hw;
                Vector2 P(float x, float y) => new(c.x + x * sx, c.y + y * hh);

                p.strokeColor = BodyColor;
                p.lineWidth = 3f;
                p.lineJoin = LineJoin.Round;

                p.fillColor = LowerWingColor;
                p.BeginPath();
                p.MoveTo(P(0f, 0.05f));
                p.BezierCurveTo(P(0.55f, 0.1f), P(0.8f, 0.7f), P(0.42f, 0.85f));
                p.BezierCurveTo(P(0.2f, 0.92f), P(0.05f, 0.55f), P(0f, 0.25f));
                p.ClosePath();
                p.Fill();
                p.Stroke();

                p.fillColor = UpperWingColor;
                p.BeginPath();
                p.MoveTo(P(0f, -0.1f));
                p.BezierCurveTo(P(0.45f, -1.0f), P(1.0f, -0.95f), P(0.95f, -0.3f));
                p.BezierCurveTo(P(0.9f, 0.05f), P(0.35f, 0.12f), P(0f, 0.08f));
                p.ClosePath();
                p.Fill();
                p.Stroke();

                p.fillColor = SpotColor;
                p.BeginPath();
                p.Arc(P(0.68f, -0.5f), hw * 0.07f * open + 1f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
                p.BeginPath();
                p.Arc(P(0.45f, -0.25f), hw * 0.045f * open + 1f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();

                p.lineWidth = 2.5f;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x, c.y - hh * 0.35f));
                p.BezierCurveTo(new Vector2(c.x + side * hw * 0.1f, c.y - hh * 0.8f),
                    new Vector2(c.x + side * hw * 0.25f, c.y - hh * 0.9f),
                    new Vector2(c.x + side * hw * 0.32f, c.y - hh * 0.95f));
                p.Stroke();
            }

            p.lineWidth = hw * 0.11f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x, c.y - hh * 0.35f));
            p.LineTo(new Vector2(c.x, c.y + hh * 0.6f));
            p.Stroke();
        }
    }
}
