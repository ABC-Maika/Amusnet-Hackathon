using UnityEngine;

namespace Amusnet.Mess
{
    // A trail of glowing rings that leads the butterfly from wherever it is straight to a mission's rope. The trail is laid
    // out fresh each time the player comes near, the next ring pulses, and time slows while the player lines up on it.
    public sealed class GuideRings : MonoBehaviour
    {
        [SerializeField] private BoatDrift mission;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private int maxRings = 7;
        [SerializeField] private float spacing = 4.5f;
        [SerializeField] private float radius = 2.3f;
        [SerializeField] private float weave = 2.5f;
        [SerializeField] private float showDistance = 34f;
        [SerializeField] private float slowDistance = 6f;
        [SerializeField] private float slowScale = 0.35f;

        private Transform player;
        private LineRenderer[] rings;
        private Vector3[] centers;
        private int count, next;
        private float alpha;
        private bool built;

        private void Start()
        {
            var c = FindAnyObjectByType<ButterflyController>();
            if (c) player = c.transform;
            rings = new LineRenderer[maxRings];
            for (int i = 0; i < maxRings; i++)
            {
                var go = new GameObject("Guide Ring " + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = 56;
                lr.numCornerVertices = 2;
                lr.sharedMaterial = ringMaterial;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                rings[i] = lr;
            }
            centers = new Vector3[maxRings];
        }

        // Lays rings from `from` to the rope: evenly spaced, weaving gently, each facing the next.
        private void Build(Vector3 from)
        {
            Vector3 end = mission.CutPoint;
            from.y = Mathf.Clamp(from.y, end.y + 1.5f, end.y + 7f);
            float dist = Vector3.Distance(from, end);
            count = Mathf.Clamp(Mathf.RoundToInt(dist / spacing), 2, maxRings);
            Vector3 dir = (end - from).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 1) / (float)count;
                Vector3 p = Vector3.Lerp(from, end, t);
                float w = Mathf.Sin(t * Mathf.PI * 2.2f) * weave * (1f - t);
                centers[i] = p + side * w + Vector3.up * (Mathf.Cos(t * Mathf.PI * 2.2f) * 0.8f * (1f - t));
            }
            centers[count - 1] = end;
            for (int i = 0; i < count; i++)
            {
                Vector3 fwd = i + 1 < count ? centers[i + 1] - centers[i] : end - centers[Mathf.Max(0, i - 1)];
                Draw(rings[i], centers[i], fwd.normalized, radius * Mathf.Lerp(1f, 0.8f, i / (float)Mathf.Max(1, count - 1)));
            }
            next = 0;
            built = true;
        }

        private static void Draw(LineRenderer lr, Vector3 c, Vector3 fwd, float r)
        {
            var rot = Quaternion.LookRotation(fwd, Vector3.up);
            for (int k = 0; k < lr.positionCount; k++)
            {
                float a = k / (float)lr.positionCount * Mathf.PI * 2f;
                lr.SetPosition(k, c + rot * new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
        }

        private void Update()
        {
            if (rings == null || !mission) return;
            bool near = mission.CanBeCut && player &&
                (player.position - mission.CutPoint).sqrMagnitude < showDistance * showDistance;
            if (near && !built) Build(player.position);
            if (!near && alpha <= 0.01f) built = false;
            alpha = Mathf.MoveTowards(alpha, near ? 1f : 0f, Time.unscaledDeltaTime * 2f);

            if (near && built && next < count)
            {
                var d = player.position - centers[next];
                if (d.sqrMagnitude < radius * radius * 0.8f) next++;
                else if (d.sqrMagnitude < slowDistance * slowDistance) TimeSlow.Hold(slowScale);
            }

            for (int i = 0; i < maxRings; i++)
            {
                bool used = built && i < count;
                rings[i].enabled = used && alpha > 0.01f;
                if (!used) continue;
                bool done = i < next, current = i == next;
                float pulse = current ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 7f) : 1f;
                var col = done ? new Color(0.4f, 1f, 0.5f) : current ? new Color(1f, 0.85f, 0.25f) : new Color(0.55f, 0.9f, 1f);
                float a = alpha * (done ? 0.3f : current ? 1f : 0.7f) * pulse;
                rings[i].startColor = rings[i].endColor = new Color(col.r, col.g, col.b, a);
                rings[i].widthMultiplier = current ? 0.5f * (0.9f + 0.2f * pulse) : 0.28f;
            }
        }
    }
}
