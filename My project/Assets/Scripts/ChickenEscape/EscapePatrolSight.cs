using UnityEngine;
using UnityEngine.Rendering;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(EscapeKeeper))]
    public sealed class EscapePatrolSight : MonoBehaviour
    {
        const int Segments = 24;
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
        readonly Vector3[] conePoints = new Vector3[Segments + 2];
        float nextScan;

        void Awake()
        {
            keeper = GetComponent<EscapeKeeper>();
            appearance = new MaterialPropertyBlock();

            cone = MakeFan("Keeper sight", conePoints);
            cone.MarkDynamic();
            coneRenderer = AddMarker(cone);
            var disk = new Vector3[Segments + 2];
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * 2f * Mathf.PI / Segments;
                disk[i + 1] = new Vector3(Mathf.Sin(angle) * 1.15f, 0f, Mathf.Cos(angle) * 1.15f);
            }
            strike = MakeFan("Keeper lunge indicator", disk);
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
                if (Time.time >= nextScan)
                {
                    ProjectCone();
                    nextScan = Time.time + .055f;
                }
            }
        }

        void ProjectCone()
        {
            conePoints[0] = Vector3.up * .075f;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = Mathf.Lerp(-EscapeKeeper.SightAngle, EscapeKeeper.SightAngle, (float)i / Segments) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                float distance = Physics.Raycast(keeper.Eye, transform.TransformDirection(direction), out var hit,
                    EscapeKeeper.SightRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    ? Mathf.Max(.05f, hit.distance - .055f) : EscapeKeeper.SightRange;
                conePoints[i + 1] = direction * distance + Vector3.up * .075f;
            }
            cone.vertices = conePoints;
            cone.RecalculateBounds();
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

        static Mesh MakeFan(string name, Vector3[] points)
        {
            var triangles = new int[Segments * 3];
            for (int i = 0; i < Segments; i++)
            {
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
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
