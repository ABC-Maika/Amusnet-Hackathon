using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dangling legs. Each butterfly leg mesh (LeftLeg1 Segments ... RightLeg3 Segments) is three separate segments:
/// thigh, shin and foot. At startup the shin and foot are split off into their own object that pivots at the
/// knee. The thigh keeps the Animator's pose; the lower leg is bent at the knee and hangs straight down in
/// world space with a soft sway (it lags behind movement and swings a little in the air).
/// While the butterfly is standing, the lower leg returns to its normal pose.
/// Added to the butterfly automatically by ButterflyController.
/// </summary>
public sealed class ButterflyLegs : MonoBehaviour
{
    [Range(0f, 1f)] public float hangAmount = 1f;
    [Tooltip("How quickly the lower leg follows gravity (higher is stiffer).")]
    public float stiffness = 7f;
    [Tooltip("Degrees the lower leg trails behind the body's movement, per m/s.")]
    public float trailPerSpeed = 14f;
    [Tooltip("Gentle idle swing in degrees.")]
    public float swayDegrees = 6f;

    class Leg
    {
        public Transform lower;
        public Vector3 restDir;   // direction of the lower leg in its parent's space, knee to tip
        public float phase;
        public Vector3 dir;       // current world direction of the lower leg
    }

    readonly List<Leg> legs = new List<Leg>();
    bool supported;
    Vector3 lastPos, bodyVelocity;

    static readonly string[] LegNames = { "LeftLeg1", "LeftLeg2", "LeftLeg3", "RightLeg1", "RightLeg2", "RightLeg3" };

    public void SetSupported(bool value) => supported = value;

    void Awake()
    {
        foreach (var name in LegNames)
        {
            var hip = FindDeep(transform, name);
            if (hip == null) continue;
            var filter = hip.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var leg = Split(filter);
            if (leg != null) legs.Add(leg);
        }
        lastPos = transform.position;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || legs.Count == 0) return;

        bodyVelocity = Vector3.Lerp(bodyVelocity, (transform.position - lastPos) / dt, 1f - Mathf.Exp(-6f * dt));
        lastPos = transform.position;
        hangAmount = Mathf.MoveTowards(hangAmount, supported ? 0f : 1f, 4f * dt);

        foreach (var leg in legs)
        {
            var parent = leg.lower.parent;
            Vector3 restWorld = parent.TransformDirection(leg.restDir);
            // Hang down, trail the movement, and sway a little.
            Vector3 trail = -bodyVelocity * (trailPerSpeed * Mathf.Deg2Rad);
            Vector3 sway = (Vector3.right * Mathf.Sin(Time.time * 2.1f + leg.phase) + Vector3.forward * Mathf.Cos(Time.time * 1.7f + leg.phase * 1.3f))
                           * (swayDegrees * Mathf.Deg2Rad);
            Vector3 target = (Vector3.down + trail + sway).normalized;

            if (leg.dir == Vector3.zero) leg.dir = restWorld;
            leg.dir = Vector3.Slerp(leg.dir, target, 1f - Mathf.Exp(-stiffness * dt)).normalized;

            Vector3 dir = Vector3.Slerp(restWorld, leg.dir, hangAmount);
            leg.lower.rotation = Quaternion.FromToRotation(restWorld, dir) * parent.rotation;
        }
    }

    // ---------- mesh split ----------

    static Leg Split(MeshFilter filter)
    {
        Mesh source = filter.sharedMesh;
        if (source.subMeshCount != 1 || !source.isReadable) return null;

        Vector3[] verts = source.vertices;
        int[] tris = source.triangles;

        // Weld by position so flat-shaded duplicate vertices still count as one piece.
        var parentOf = new int[verts.Length];
        for (int i = 0; i < parentOf.Length; i++) parentOf[i] = i;
        int Find(int x) { while (parentOf[x] != x) { parentOf[x] = parentOf[parentOf[x]]; x = parentOf[x]; } return x; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parentOf[a] = b; }

        var byPosition = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < verts.Length; i++)
        {
            var key = Vector3Int.RoundToInt(verts[i] * 2000f);
            if (byPosition.TryGetValue(key, out int first)) Union(i, first); else byPosition[key] = i;
        }
        for (int t = 0; t < tris.Length; t += 3) { Union(tris[t], tris[t + 1]); Union(tris[t], tris[t + 2]); }

        // Pieces ordered from the hip outwards.
        var pieceSum = new Dictionary<int, Vector3>();
        var pieceCount = new Dictionary<int, int>();
        for (int i = 0; i < verts.Length; i++)
        {
            int r = Find(i);
            pieceSum[r] = (pieceSum.TryGetValue(r, out var s) ? s : Vector3.zero) + verts[i];
            pieceCount[r] = (pieceCount.TryGetValue(r, out int c) ? c : 0) + 1;
        }
        if (pieceSum.Count < 2) return null;
        var order = new List<int>(pieceSum.Keys);
        order.Sort((a, b) => (pieceSum[a] / pieceCount[a]).sqrMagnitude.CompareTo((pieceSum[b] / pieceCount[b]).sqrMagnitude));
        int thigh = order[0];

        var upperTris = new List<int>();
        var lowerTris = new List<int>();
        for (int t = 0; t < tris.Length; t += 3)
            (Find(tris[t]) == thigh ? upperTris : lowerTris).AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
        if (lowerTris.Count == 0) return null;

        // Knee: the far end of the thigh.
        float far = 0f;
        for (int i = 0; i < verts.Length; i++) if (Find(i) == thigh) far = Mathf.Max(far, verts[i].magnitude);
        Vector3 knee = Vector3.zero; int n = 0;
        for (int i = 0; i < verts.Length; i++)
            if (Find(i) == thigh && verts[i].magnitude > far * 0.85f) { knee += verts[i]; n++; }
        knee /= Mathf.Max(1, n);

        Vector3 lowerCentre = Vector3.zero; int lc = 0;
        foreach (int idx in lowerTris) { lowerCentre += verts[idx]; lc++; }
        lowerCentre /= lc;

        // Thigh stays on the original object.
        var upper = new Mesh { name = source.name + " Thigh", vertices = verts, normals = source.normals, uv = source.uv };
        upper.SetTriangles(upperTris, 0);
        upper.RecalculateBounds();
        filter.sharedMesh = upper;

        // Shin and foot pivot at the knee.
        var shifted = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) shifted[i] = verts[i] - knee;
        var lowerMesh = new Mesh { name = source.name + " Lower", vertices = shifted, normals = source.normals, uv = source.uv };
        lowerMesh.SetTriangles(lowerTris, 0);
        lowerMesh.RecalculateBounds();

        var go = new GameObject(filter.name + " Lower Leg");
        go.layer = filter.gameObject.layer;
        go.transform.SetParent(filter.transform, false);
        go.transform.localPosition = knee;
        go.AddComponent<MeshFilter>().sharedMesh = lowerMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        var original = filter.GetComponent<MeshRenderer>();
        if (original != null)
        {
            renderer.sharedMaterials = original.sharedMaterials;
            renderer.shadowCastingMode = original.shadowCastingMode;
        }

        return new Leg { lower = go.transform, restDir = (lowerCentre - knee).normalized, phase = Random.value * 6.28f };
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var found = FindDeep(c, name);
            if (found != null) return found;
        }
        return null;
    }
}
