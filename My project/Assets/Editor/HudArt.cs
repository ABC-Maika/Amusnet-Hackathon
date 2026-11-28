using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedural sprites for the "forest lodge" HUD (chamfered gold-framed panels, pills, dial gauge,
/// butterfly emblem). Everything is drawn white so Image.color tints it. Saved to Assets/UI/Generated
/// and re-generated every time the HUD builder runs.
/// </summary>
public static class HudArt
{
    public const string Dir = "Assets/UI/Generated";
    const float Sqrt2 = 1.41421356f;

    // ---------- public sprites ----------

    /// <summary>Chamfered panel body (9-sliced, border 36).</summary>
    public static Sprite PanelFill() => Save("PanelFill", 128, 36, (x, y) => Fill(Chamfer(x, y, 128, 1f, 22f)));

    /// <summary>Gold double frame with corner rivets for PanelFill (9-sliced, border 36).</summary>
    public static Sprite PanelFrame() => Save("PanelFrame", 128, 36, (x, y) =>
    {
        float d = Chamfer(x, y, 128, 1f, 22f);
        float a = Line(d, 2.6f, 2.6f);
        a = Mathf.Max(a, Line(d, 8.5f, 1.3f));
        // rivets near each corner, just inside the inner line
        float qx = Mathf.Abs(x + 0.5f - 64f), qy = Mathf.Abs(y + 0.5f - 64f);
        float r = Mathf.Sqrt((qx - 43f) * (qx - 43f) + (qy - 43f) * (qy - 43f));
        a = Mathf.Max(a, Mathf.Clamp01(2.4f - r + 0.5f));
        return a;
    });

    /// <summary>Small chamfered pill body (9-sliced, border 16).</summary>
    public static Sprite PillFill() => Save("PillFill", 64, 16, (x, y) => Fill(Chamfer(x, y, 64, 0.5f, 10f)));

    /// <summary>Single-line frame for PillFill (9-sliced, border 16).</summary>
    public static Sprite PillFrame() => Save("PillFrame", 64, 16, (x, y) => Line(Chamfer(x, y, 64, 0.5f, 10f), 1.4f, 1.7f));

    /// <summary>Gauge dial: outer and inner rings with minute/hour style ticks.</summary>
    public static Sprite Dial() => Save("Dial", 256, 0, (x, y) =>
    {
        float dx = x + 0.5f - 128f, dy = y + 0.5f - 128f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float a = Mathf.Clamp01(1.2f - Mathf.Abs(r - 124f) + 0.5f);          // outer ring
        a = Mathf.Max(a, Mathf.Clamp01(0.9f - Mathf.Abs(r - 99f) + 0.5f));   // inner ring
        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        float minor = Mathf.Abs(Mathf.Repeat(ang + 3f, 6f) - 3f) * Mathf.Deg2Rad * r;   // px to nearest 6° tick
        float major = Mathf.Abs(Mathf.Repeat(ang + 15f, 30f) - 15f) * Mathf.Deg2Rad * r; // px to nearest 30° tick
        if (r > 106f && r < 116f) a = Mathf.Max(a, Mathf.Clamp01(0.8f - minor + 0.5f) * 0.7f);
        if (r > 103f && r < 120f) a = Mathf.Max(a, Mathf.Clamp01(1.4f - major + 0.5f));
        return a;
    });

    /// <summary>Thin progress arc that sits on the dial ticks. Use Image.Type.Filled, Radial360.</summary>
    public static Sprite Arc() => Save("Arc", 256, 0, (x, y) =>
    {
        float dx = x + 0.5f - 128f, dy = y + 0.5f - 128f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01(Mathf.Min(r - 104f, 119f - r) + 0.5f);
    });

    /// <summary>Filled disc.</summary>
    public static Sprite Disc() => Save("Disc", 128, 0, (x, y) =>
    {
        float dx = x + 0.5f - 64f, dy = y + 0.5f - 64f;
        return Mathf.Clamp01(63f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
    });

    /// <summary>Right-pointing needle for the anger bar.</summary>
    public static Sprite Needle() => Save("Needle", 32, 0, (x, y) =>
    {
        // counter-clockwise triangle A(4,4) -> C(28,16) -> B(4,28); inside = positive on every edge
        var p = new Vector2(x + 0.5f, y + 0.5f);
        var A = new Vector2(4f, 4f); var C = new Vector2(28f, 16f); var B = new Vector2(4f, 28f);
        float d = Mathf.Min(Edge(A, C, p), Mathf.Min(Edge(C, B, p), Edge(B, A, p)));
        return Mathf.Clamp01(d + 0.5f);
    });

    static float Edge(Vector2 p0, Vector2 p1, Vector2 p)
    {
        Vector2 e = p1 - p0, v = p - p0;
        return (e.x * v.y - e.y * v.x) / e.magnitude;
    }

    /// <summary>Engraved butterfly emblem (lace-filled wings, outline, body and antennae).</summary>
    public static Sprite Butterfly()
    {
        const int size = 256;
        var alpha = new float[size * size];

        // butterfly curve r(t) = e^cos t - 2cos4t - sin^5(t/12)
        Vector2 P(float t)
        {
            float r = Mathf.Exp(Mathf.Cos(t)) - 2f * Mathf.Cos(4f * t) - Mathf.Pow(Mathf.Sin(t / 12f), 5f);
            return new Vector2(Mathf.Sin(t) * r, Mathf.Cos(t) * r);
        }
        const float scale = 30f;
        Vector2 center = new Vector2(128f, 118f);
        Vector2 ToPx(Vector2 p) => center + p * scale;

        // wing fill: even-odd fill of one turn gives a lace pattern
        const int n = 720;
        var poly = new Vector2[n];
        for (int i = 0; i < n; i++) poly[i] = ToPx(P(i / (float)n * Mathf.PI * 2f));
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                    if ((poly[i].y > py) != (poly[j].y > py) &&
                        px < (poly[j].x - poly[i].x) * (py - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                        inside = !inside;
                if (inside) alpha[y * size + x] = 0.55f;
            }

        // outline of the full curve
        const int steps = 24000;
        for (int i = 0; i < steps; i++)
            Stamp(alpha, size, ToPx(P(i / (float)steps * Mathf.PI * 12f)), 1.1f, 1f);

        // body and antennae
        for (int i = 0; i <= 60; i++)
        {
            float k = i / 60f;
            Stamp(alpha, size, new Vector2(128f, Mathf.Lerp(70f, 150f, k)), Mathf.Lerp(3.2f, 5f, Mathf.Sin(k * Mathf.PI)), 1f);
        }
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i <= 80; i++)
            {
                float k = i / 80f;
                var p = new Vector2(128f + s * (k * 26f + k * k * 8f), 150f + k * 46f - k * k * 6f);
                Stamp(alpha, size, p, 1.2f, 1f);
                if (i == 80) Stamp(alpha, size, p, 3f, 1f);
            }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px32 = new Color32[size * size];
        for (int i = 0; i < px32.Length; i++) px32[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha[i]) * 255));
        tex.SetPixels32(px32);
        tex.Apply();
        return Write("Butterfly", tex, 0);
    }

    // ---------- helpers ----------

    /// <summary>Signed distance (px, negative inside) to a centred chamfered square.</summary>
    static float Chamfer(int x, int y, int size, float inset, float cut)
    {
        float h = size / 2f - inset;
        float qx = Mathf.Abs(x + 0.5f - size / 2f), qy = Mathf.Abs(y + 0.5f - size / 2f);
        float box = Mathf.Max(qx - h, qy - h);
        float diag = (qx + qy - (2f * h - cut)) / Sqrt2;
        return Mathf.Max(box, diag);
    }

    static float Fill(float d) => Mathf.Clamp01(0.5f - d);

    /// <summary>Line of the given width, `inset` px inside the shape edge.</summary>
    static float Line(float d, float inset, float width) => Mathf.Clamp01(width / 2f - Mathf.Abs(d + inset) + 0.5f);

    static void Stamp(float[] a, int size, Vector2 c, float radius, float value)
    {
        int x0 = Mathf.FloorToInt(c.x - radius - 1), x1 = Mathf.CeilToInt(c.x + radius + 1);
        int y0 = Mathf.FloorToInt(c.y - radius - 1), y1 = Mathf.CeilToInt(c.y + radius + 1);
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(size - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(size - 1, x1); x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float v = Mathf.Clamp01(radius - d + 0.5f) * value;
                if (v > a[y * size + x]) a[y * size + x] = v;
            }
    }

    static Sprite Save(string name, int size, int border, Func<int, int, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255));
        tex.SetPixels32(px);
        tex.Apply();
        return Write(name, tex, border);
    }

    static Sprite Write(string name, Texture2D tex, int border)
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        string path = $"{Dir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = new Vector4(border, border, border, border);
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
