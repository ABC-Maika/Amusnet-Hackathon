using UnityEngine;
using UnityEngine.UIElements;

namespace ButterflyUI
{
    /// <summary>
    /// A left-to-right dark fade behind the left-third screens (menu, pause, win, lose), so their text reads over the live village.
    /// USS has no gradients, so it is drawn as vertex-coloured quads. Stretch it over the screen with USS.
    /// </summary>
    [UxmlElement]
    public partial class MenuScrim : VisualElement
    {
        [UxmlAttribute] public Color Shade { get; set; } = new Color32(39, 37, 43, 255);

        // (position across the width, opacity) stops
        static readonly Vector2[] Stops = { new(0f, 0.66f), new(0.26f, 0.42f), new(0.48f, 0f) };

        public MenuScrim()
        {
            generateVisualContent += Draw;
            // The menu starts hidden (display: none), so redraw once it is laid out at its real size.
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1f || h < 1f) return;

            var mesh = ctx.Allocate(Stops.Length * 2, (Stops.Length - 1) * 6);
            var verts = new Vertex[Stops.Length * 2];
            for (int i = 0; i < Stops.Length; i++)
            {
                var c = Shade;
                c.a = Stops[i].y;
                float x = Stops[i].x * w;
                verts[i * 2] = new Vertex { position = new Vector3(x, 0f, Vertex.nearZ), tint = c };
                verts[i * 2 + 1] = new Vertex { position = new Vector3(x, h, Vertex.nearZ), tint = c };
            }
            var indices = new ushort[(Stops.Length - 1) * 6];
            for (int i = 0; i < Stops.Length - 1; i++)
            {
                ushort a = (ushort)(i * 2), b = (ushort)(a + 1), c = (ushort)(a + 2), d = (ushort)(a + 3);
                indices[i * 6] = a; indices[i * 6 + 1] = c; indices[i * 6 + 2] = b;
                indices[i * 6 + 3] = b; indices[i * 6 + 4] = c; indices[i * 6 + 5] = d;
            }
            mesh.SetAllVertices(verts);
            mesh.SetAllIndices(indices);
        }
    }
}
