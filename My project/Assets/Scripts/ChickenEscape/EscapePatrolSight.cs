using UnityEngine;
using UnityEngine.Rendering;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(EscapeKeeper))]
    public sealed class EscapePatrolSight : MonoBehaviour
    {
        const int ConeSegments = 80;
        const int StrikeSegments = 24;
        static readonly Vector3 Lift = Vector3.up * .075f;
        static readonly int Tint = Shader.PropertyToID("_Color");
        static readonly Color PatrolColor = new(.76f, .39f, .11f, .22f);
        static readonly Color ChaseColor = new(.91f, .20f, .10f, .29f);
        static readonly Color DecoyColor = new(.20f, .53f, .60f, .20f);

        [SerializeField] MarketEscapeRun run;
        [SerializeField] Material material;

        EscapeKeeper keeper;
        Mesh cone, strike;
        MeshRenderer coneRenderer, strikeRenderer;
        MaterialPropertyBlock appearance;
        readonly Vector3[] conePoints = new Vector3[2 * ConeSegments + 1];
        readonly Vector3[] coneDirections = new Vector3[ConeSegments + 1];

        void Awake()
        {
            keeper = GetComponent<EscapeKeeper>();
            appearance = new MaterialPropertyBlock();

            for (int i = 0; i <= ConeSegments; i++)
            {
                float angle = Mathf.Lerp(-EscapeKeeper.SightAngle, EscapeKeeper.SightAngle,
                    (float)i / ConeSegments) * Mathf.Deg2Rad;
                coneDirections[i] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            cone = MakeFan("Keeper sight", conePoints, ConeSegments, 2);
            cone.MarkDynamic();
            coneRenderer = AddMarker(cone);
            var disk = new Vector3[StrikeSegments + 2];
            for (int i = 0; i <= StrikeSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / StrikeSegments;
                disk[i + 1] = new Vector3(Mathf.Sin(angle) * 1.15f, 0f, Mathf.Cos(angle) * 1.15f);
            }
            strike = MakeFan("Keeper lunge indicator", disk, StrikeSegments, 1);
            strikeRenderer = AddMarker(strike);
            coneRenderer.enabled = strikeRenderer.enabled = false;
        }

        void LateUpdate()
        {
            bool show = run.Current == MarketEscapeRun.Phase.Farm && !run.IsPaused;
            bool aboutToGrab = keeper.Current is EscapeKeeper.PatrolState.Windup or EscapeKeeper.PatrolState.Lunging;
            strikeRenderer.enabled = show && aboutToGrab;
            coneRenderer.enabled = show && !aboutToGrab && !keeper.IsHandlingHen && !run.DogCaptured;
            if (!strikeRenderer.enabled && !coneRenderer.enabled) return;
            if (aboutToGrab)
            {
                Vector3 center = keeper.StrikePoint;
                center.y = run.YardHeight + .08f;
                strikeRenderer.transform.position = center;
                SetTint(strikeRenderer, new Color(.91f, .16f, .08f, Mathf.Lerp(.32f, .65f, Mathf.PingPong(Time.time * 5f, 1f))));
            }
            else
            {
                SetTint(coneRenderer, keeper.HasSpottedSomeone ? ChaseColor : keeper.IsDistracted ? DecoyColor : PatrolColor);
                ProjectCone();
            }
        }

        void ProjectCone()
        {
            conePoints[0] = Lift;
            float previous = ScanDirection(coneDirections[0]);
            for (int i = 0; i < ConeSegments; i++)
            {
                float next = ScanDirection(coneDirections[i + 1]);
                // Give the whole slice the nearer edge so its triangle cannot cut through cover.
                float distance = Mathf.Min(previous, next);
                conePoints[2 * i + 1] = coneDirections[i] * distance + Lift;
                conePoints[2 * i + 2] = coneDirections[i + 1] * distance + Lift;
                previous = next;
            }
            cone.vertices = conePoints;
            cone.RecalculateBounds();
        }

        float ScanDirection(Vector3 direction)
        {
            float hit = keeper.SightDistance(transform.TransformDirection(direction), EscapeKeeper.SightRange);
            return Mathf.Max(0f, hit - .025f);
        }

        MeshRenderer AddMarker(Mesh mesh)
        {
            var marker = new GameObject(mesh.name);
            marker.transform.SetParent(transform, false);
            marker.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = marker.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        void SetTint(MeshRenderer renderer, Color color)
        {
            appearance.SetColor(Tint, color);
            renderer.SetPropertyBlock(appearance);
        }

        static Mesh MakeFan(string name, Vector3[] points, int segments, int stride)
        {
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3 + 1] = i * stride + 1;
                triangles[i * 3 + 2] = i * stride + 2;
            }
            return new Mesh { name = name, vertices = points, triangles = triangles };
        }

        void OnDestroy()
        {
            Destroy(cone);
            Destroy(strike);
        }
    }
}
