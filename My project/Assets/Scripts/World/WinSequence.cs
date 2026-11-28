using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// The win cinematic: villagers carry their bundles out of the village along the roads, vines, moss and flowers
/// grow over the houses, trees sprout in the square, and the camera pulls back and up to show it all.
/// Registers itself as GameFlow's WinCinematic, so GameFlow.Win() plays it before the Won state.
/// Everything runs on unscaled time and is built from primitives, so it works with any village:
/// houses, exits and square can be assigned, or are found under the village root at runtime.
/// </summary>
public class WinSequence : MonoBehaviour
{
    [Header("Village (leave empty to find at runtime)")]
    [Tooltip("Village root. Empty: the scene object named Village Name.")]
    [SerializeField] Transform village;
    [SerializeField] string villageName = "Placeholder Village";
    [Tooltip("Houses the villagers leave and nature takes over. Empty: the children of the village's 'Houses' object.")]
    [SerializeField] List<Transform> houses = new();
    [Tooltip("Where the villagers walk off to. Empty: four points past the edge of the village along ±X and ±Z.")]
    [SerializeField] List<Transform> exits = new();
    [Tooltip("The square where trees sprout. Empty: the village's 'Fountain', else the village root.")]
    [SerializeField] Transform square;

    [Header("Look")]
    [Tooltip("URP Lit material that every piece is tinted from.")]
    [SerializeField] Material baseMaterial;

    [Header("Timing (unscaled seconds)")]
    [SerializeField] float duration = 9f;
    [SerializeField] float cameraPullTime = 7f;
    [Tooltip("Camera end pose above the square: height and distance back.")]
    [SerializeField] float cameraHeight = 42f, cameraDistance = 48f;

    static readonly Color[] Shirts = { Hex(0x4FA3E0), Hex(0x1F4E79), Hex(0xFFC94A), Hex(0xE86A5A), Hex(0xFFFFFF) };
    static readonly Color[] Cloths = { Hex(0xE86A5A), Hex(0x4FA3E0), Hex(0xFFC94A), Hex(0xB38BFF) };
    static readonly Color[] Greens = { Hex(0x5DBB63), Hex(0x3E9B4F), Hex(0x8BD17C) };
    static readonly Color[] Petals = { Hex(0xFF8FB1), Hex(0xFFC94A), Hex(0xFFFFFF), Hex(0xB38BFF) };
    static readonly Color Skin = Hex(0xF2C8A0), Sack = Hex(0xC68B59), Wood = Hex(0x8A5A3B);

    readonly Dictionary<Color, Material> materials = new();
    Action<Action> registered;
    bool playing;

    void Start()
    {
        if (GameFlow.Instance == null) return;
        registered = Play;
        GameFlow.Instance.WinCinematic = registered;
    }

    void OnDestroy()
    {
        if (GameFlow.Instance != null && GameFlow.Instance.WinCinematic == registered) GameFlow.Instance.WinCinematic = null;
        foreach (var m in materials.Values) Destroy(m);
    }

    /// <summary>Plays the cinematic, then calls <paramref name="onDone"/>.</summary>
    public void Play(Action onDone)
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Run(onDone));
    }

    IEnumerator Run(Action onDone)
    {
        ResolveVillage();
        // Pieces are placed in world space under this root, which sits at the origin.
        var fx = new GameObject("Win Sequence FX").transform;
        Vector3 center = square != null ? square.position : village != null ? village.position : Vector3.zero;
        center.y = village != null ? village.position.y : 0f;
        var exitPoints = ExitPoints(center);

        float maxDist = 1f;
        foreach (var h in houses) if (h != null) maxDist = Mathf.Max(maxDist, Flat(h.position - center).magnitude);

        foreach (var house in houses)
        {
            if (house == null) continue;
            var bounds = HouseBounds(house);
            float wave = Flat(house.position - center).magnitude / maxDist;
            SpawnVillagers(fx, house, center, exitPoints);
            GrowNature(fx, house, bounds, 1.4f + wave * 2.2f);
        }
        SpawnCart(fx, center, exitPoints[0]);
        SproutSquareTrees(fx, center);

        yield return MoveCamera(center);
        playing = false;
        onDone?.Invoke();
    }

    void ResolveVillage()
    {
        if (village == null)
        {
            var go = GameObject.Find(villageName);
            if (go != null) village = go.transform;
        }
        if (village == null) return;
        if (houses.Count == 0)
        {
            var root = village.Find("Houses");
            if (root != null) foreach (Transform h in root) houses.Add(h);
        }
        if (square == null) square = village.Find("Fountain");
    }

    List<Vector3> ExitPoints(Vector3 center)
    {
        var points = new List<Vector3>();
        foreach (var e in exits) if (e != null) points.Add(e.position);
        if (points.Count > 0) return points;

        float reach = 10f;
        foreach (var h in houses) if (h != null) reach = Mathf.Max(reach, Flat(h.position - center).magnitude);
        reach += 8f;
        foreach (var dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            points.Add(center + dir * reach);
        return points;
    }

    // ---------- Villagers ----------

    void SpawnVillagers(Transform fx, Transform house, Vector3 center, List<Vector3> exitPoints)
    {
        var door = house.Find("Door");
        Vector3 start = door != null ? door.position : house.position + Flat(center - house.position).normalized * 3f;
        start.y = center.y;
        Vector3 exit = NearestExit(center, start, exitPoints);
        // Step out of the door, join the road that runs from the square to the exit, then follow it out.
        Vector3 onRoad = ClosestOnSegment(center, exit, start);

        int count = Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            var side = Vector3.Cross(Vector3.up, Flat(exit - onRoad).normalized) * (i == 0 ? 0.6f : -0.6f);
            var route = new[] { start, onRoad + side, exit + side };
            float delay = 0.2f + i * 0.45f + Random.Range(0f, 0.6f);
            float length = Vector3.Distance(route[0], route[1]) + Vector3.Distance(route[1], route[2]);
            float speed = Mathf.Max(3f, length / Mathf.Max(1f, duration - 1.2f - delay));
            var villager = BuildVillager(fx);
            WinWalker.Add(villager.gameObject, villager.GetChild(0), route, delay, speed, 0.22f);
        }
    }

    Transform BuildVillager(Transform parent)
    {
        var root = new GameObject("Villager").transform;
        root.SetParent(parent, false);
        var body = new GameObject("Body").transform;
        body.SetParent(root, false);

        Piece(PrimitiveType.Capsule, body, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.6f), Pick(Shirts));
        Piece(PrimitiveType.Sphere, body, new Vector3(0f, 1.35f, 0f), Vector3.one * 0.5f, Skin);
        // Bundle on the back, tied with a bright cloth knot.
        Piece(PrimitiveType.Cube, body, new Vector3(0f, 0.95f, -0.42f), new Vector3(0.55f, 0.6f, 0.4f), Sack, Quaternion.Euler(0f, 0f, 8f));
        Piece(PrimitiveType.Sphere, body, new Vector3(0f, 1.35f, -0.45f), Vector3.one * 0.38f, Pick(Cloths));
        return root;
    }

    void SpawnCart(Transform fx, Vector3 center, Vector3 exit)
    {
        Vector3 dir = Flat(exit - center).normalized;
        Vector3 start = center + dir * 5f + Vector3.Cross(Vector3.up, dir) * 1.2f;
        Vector3 end = exit + Vector3.Cross(Vector3.up, dir) * 1.2f;

        var root = new GameObject("Cart").transform;
        root.SetParent(fx, false);
        var body = new GameObject("Body").transform;
        body.SetParent(root, false);
        Piece(PrimitiveType.Cube, body, new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 0.45f, 2.4f), Wood);
        foreach (var x in new[] { -0.85f, 0.85f })
        foreach (var z in new[] { -0.75f, 0.75f })
            Piece(PrimitiveType.Cylinder, body, new Vector3(x, 0.45f, z), new Vector3(0.9f, 0.08f, 0.9f), Hex(0x5A3A26), Quaternion.Euler(0f, 0f, 90f));
        // Piled-up belongings.
        Piece(PrimitiveType.Cube, body, new Vector3(-0.3f, 1.25f, -0.4f), new Vector3(0.7f, 0.6f, 0.7f), Sack);
        Piece(PrimitiveType.Cube, body, new Vector3(0.35f, 1.15f, 0.4f), new Vector3(0.6f, 0.45f, 0.8f), Hex(0x4FA3E0));
        Piece(PrimitiveType.Sphere, body, new Vector3(0.1f, 1.7f, 0f), Vector3.one * 0.6f, Hex(0xE86A5A));
        // The villager pulling it walks in front.
        var puller = BuildVillager(body);
        puller.localPosition = new Vector3(0f, 0f, 1.9f);

        float delay = 0.8f;
        float speed = Vector3.Distance(start, end) / (duration - 1.2f - delay);
        WinWalker.Add(root.gameObject, body, new[] { start, end }, delay, speed, 0.08f);
    }

    // ---------- Nature ----------

    void GrowNature(Transform fx, Transform house, Bounds b, float startDelay)
    {
        var root = new GameObject($"Nature {house.name}").transform;
        root.SetParent(fx, false);
        float ground = house.position.y;

        // Vines climbing the walls.
        for (int i = 0; i < 7; i++)
        {
            Vector3 p = PointOnSide(b, Random.Range(0, 4), Random.Range(0.1f, 0.9f), out Vector3 outward);
            p.y = ground;
            float height = b.size.y * Random.Range(0.45f, 0.8f);
            var vine = new GameObject("Vine").transform;
            vine.SetParent(root, false);
            vine.SetPositionAndRotation(p + outward * 0.08f, Quaternion.LookRotation(outward) * Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f)));
            Piece(PrimitiveType.Cube, vine, new Vector3(0f, 0.5f, 0f), new Vector3(0.22f, 1f, 0.12f), Greens[1]);
            vine.localScale = new Vector3(1f, height, 1f);
            float d = startDelay + Random.Range(0f, 0.8f);
            WinGrow.Add(vine.gameObject, d, 1.3f);
            // Leaves bud along the vine once it has grown.
            for (int k = 1; k <= 3; k++)
            {
                var leaf = Piece(PrimitiveType.Sphere, root, vine.position + Vector3.up * height * k / 3.5f + outward * 0.18f,
                    new Vector3(0.55f, 0.4f, 0.3f), Pick(Greens), Quaternion.LookRotation(outward));
                WinGrow.Add(leaf, d + 0.5f + k * 0.25f, 0.5f);
            }
        }

        // Moss over the roof.
        for (int i = 0; i < 3; i++)
        {
            float f = (i - 1) * 0.3f;
            Vector3 top = new(b.center.x + b.size.x * f * (b.size.x > b.size.z ? 1f : 0f), b.max.y - b.size.y * 0.22f,
                b.center.z + b.size.z * f * (b.size.x > b.size.z ? 0f : 1f));
            var moss = Piece(PrimitiveType.Sphere, root, top, new Vector3(b.size.x * 0.55f, b.size.y * 0.22f, b.size.z * 0.55f), Greens[0]);
            WinGrow.Add(moss, startDelay + 1.2f + i * 0.3f, 0.9f);
        }

        // Grass and flowers around the walls.
        for (int i = 0; i < 12; i++)
        {
            Vector3 p = PointOnSide(b, Random.Range(0, 4), Random.Range(0f, 1f), out Vector3 outward);
            p += outward * Random.Range(0.3f, 1.6f);
            p.y = ground;
            float d = startDelay + 0.3f + Random.Range(0f, 1.5f);
            if (i % 2 == 0)
            {
                var tuft = Piece(PrimitiveType.Sphere, root, p + Vector3.up * 0.3f, new Vector3(0.7f, 0.8f, 0.7f), Pick(Greens));
                WinGrow.Add(tuft, d, 0.5f);
            }
            else
            {
                var flower = new GameObject("Flower").transform;
                flower.SetParent(root, false);
                flower.position = p;
                Piece(PrimitiveType.Cylinder, flower, new Vector3(0f, 0.35f, 0f), new Vector3(0.08f, 0.35f, 0.08f), Greens[1]);
                Piece(PrimitiveType.Sphere, flower, new Vector3(0f, 0.75f, 0f), Vector3.one * 0.4f, Pick(Petals));
                WinGrow.Add(flower.gameObject, d, 0.45f);
            }
        }
    }

    void SproutSquareTrees(Transform fx, Vector3 center)
    {
        const int count = 7;
        const float radius = 6.5f;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + 0.5f) / count * Mathf.PI * 2f + 0.2f;
            var tree = new GameObject("Tree").transform;
            tree.SetParent(fx, false);
            tree.position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius + Random.Range(-0.6f, 0.8f));
            tree.localScale = Vector3.one * Random.Range(0.9f, 1.3f);
            Piece(PrimitiveType.Cylinder, tree, new Vector3(0f, 1f, 0f), new Vector3(0.45f, 1f, 0.45f), Wood);
            Piece(PrimitiveType.Sphere, tree, new Vector3(0f, 2.6f, 0f), Vector3.one * 2.2f, Pick(Greens));
            Piece(PrimitiveType.Sphere, tree, new Vector3(0.3f, 3.5f, 0.1f), Vector3.one * 1.5f, Pick(Greens));
            WinGrow.Add(tree.gameObject, 2.2f + i * 0.3f, 0.9f);
        }
    }

    // ---------- Camera ----------

    IEnumerator MoveCamera(Vector3 center)
    {
        var cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out MenuCamera follow)) follow.enabled = false;

        Vector3 fromPos = cam != null ? cam.transform.position : Vector3.zero;
        Quaternion fromRot = cam != null ? cam.transform.rotation : Quaternion.identity;
        Vector3 back = cam != null ? Flat(center - fromPos) : Vector3.forward;
        back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            if (cam != null)
            {
                float k = Mathf.Clamp01(t / cameraPullTime);
                float e = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f; // ease in-out cubic
                // Keep a slow drift around the square after the pull so the shot never freezes.
                Vector3 dir = Quaternion.Euler(0f, t * 3f, 0f) * back;
                Vector3 toPos = center - dir * cameraDistance + Vector3.up * cameraHeight;
                Quaternion toRot = Quaternion.LookRotation(center - toPos);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, e), Quaternion.Slerp(fromRot, toRot, e));
            }
            yield return null;
        }
    }

    // ---------- Helpers ----------

    GameObject Piece(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(localPos, rot ?? Quaternion.identity);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Tint(color);
        return go;
    }

    Material Tint(Color c)
    {
        if (materials.TryGetValue(c, out var m)) return m;
        m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        materials[c] = m;
        return m;
    }

    static Bounds HouseBounds(Transform house)
    {
        var rs = house.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(house.position + Vector3.up * 2f, new Vector3(5f, 4f, 5f));
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // A point along one side of the bounds' footprint (0..1 along it) and that side's outward normal.
    static Vector3 PointOnSide(Bounds b, int side, float along, out Vector3 outward)
    {
        Vector3 min = b.min, max = b.max;
        switch (side)
        {
            case 0: outward = Vector3.right; return new Vector3(max.x, 0f, Mathf.Lerp(min.z, max.z, along));
            case 1: outward = Vector3.left; return new Vector3(min.x, 0f, Mathf.Lerp(min.z, max.z, along));
            case 2: outward = Vector3.forward; return new Vector3(Mathf.Lerp(min.x, max.x, along), 0f, max.z);
            default: outward = Vector3.back; return new Vector3(Mathf.Lerp(min.x, max.x, along), 0f, min.z);
        }
    }

    // The exit whose direction from the square best matches the direction of p.
    static Vector3 NearestExit(Vector3 center, Vector3 p, List<Vector3> exitPoints)
    {
        Vector3 best = exitPoints[0];
        float bestDot = float.MinValue;
        var dir = Flat(p - center).normalized;
        foreach (var e in exitPoints)
        {
            float dot = Vector3.Dot(dir, Flat(e - center).normalized);
            if (dot > bestDot) { bestDot = dot; best = e; }
        }
        return best;
    }

    static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
        return a + ab * t;
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    static Color Pick(Color[] set) => set[Random.Range(0, set.Length)];
    static Color Hex(int rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
}
