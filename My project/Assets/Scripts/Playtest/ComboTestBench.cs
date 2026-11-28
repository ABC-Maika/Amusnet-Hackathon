using System.Collections.Generic;
using Amusnet.Mess;
using Amusnet.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Amusnet.Playtest
{
    [DefaultExecutionOrder(-100)]
    public sealed class ComboTestBench : MonoBehaviour
    {
        public ComboTestSeed[] seeds;
        public EnvironmentFlightPreview pilotTemplate;
        public Camera overview;

        private ComboTestSeed fixture;
        private EnvironmentFlightPreview pilot;
        private WingGust gust;
        private MessManager score;
        private readonly HashSet<Interactable> matched = new();
        private readonly List<string> observed = new();
        private string unexpected;
        private int selected, quarterTurns;
        private float lastHitTime;
        private readonly List<Bounds> meshes = new();
        private Bounds projectedStage;
        private int framedTurn = -1;
        private string progress, missing;
        private Vector2 seedScroll;
        private GUIStyle titleStyle, labelStyle, hintStyle, buttonStyle;
        private const float Sidebar = 195f;
        private const float Footer = 112f;

        private float UiScale => Mathf.Clamp(Mathf.Min(Screen.width / 970f, Screen.height / 610f), .62f, 1.3f);
        private void Awake()
        {
            // The default seed and pilot also provide the edit-mode preview.
            foreach (var seed in seeds) seed.gameObject.SetActive(false);
            pilotTemplate.gameObject.SetActive(false);
            LoadSeed(0);
        }

        public void LoadSeed(int index)
        {
            if ((uint)index >= seeds.Length) return;
            if (score) score.MessCreated -= Observe;
            if (fixture) Retire(fixture.gameObject);
            if (pilot) Retire(pilot.gameObject);
            observed.Clear();
            matched.Clear();
            unexpected = null;
            progress = string.Empty;
            missing = string.Empty;
            framedTurn = -1;
            quarterTurns = 0;
            selected = index;

            fixture = Instantiate(seeds[index], transform);
            fixture.Prepare();
            fixture.gameObject.SetActive(true);
            pilot = Instantiate(pilotTemplate, fixture.arrival.position, fixture.arrival.rotation, transform);
            pilot.gameObject.SetActive(true);
            gust = pilot.GetComponent<WingGust>();
            score = pilot.GetComponent<MessManager>();
            score.MessCreated += Observe;
            Physics.SyncTransforms();

            meshes.Clear();
            foreach (var prop in fixture.GetComponentsInChildren<Interactable>())
                foreach (var mesh in prop.GetComponentsInChildren<Renderer>())
                    if (mesh.enabled && (mesh is MeshRenderer || mesh is SkinnedMeshRenderer))
                        meshes.Add(mesh.bounds);
            FrameStage();
        }

        private static void Retire(GameObject old)
        {
            // Disable before deferred destruction to cancel running chains and release colliders this frame.
            old.SetActive(false);
            Destroy(old);
        }

        private void OnDestroy()
        {
            if (score) score.MessCreated -= Observe;
        }

        private void Observe(Interactable prop, int points)
        {
            observed.Add(prop.name);
            lastHitTime = Time.time;
            if (prop.ChainRoot == fixture.expected[0] && System.Array.IndexOf(fixture.expected, prop) >= 0)
                matched.Add(prop);
            else
                unexpected ??= prop.name;
            progress = string.Join("  ›  ", observed);
            var pending = new List<string>();
            foreach (var expected in fixture.expected)
                if (!matched.Contains(expected)) pending.Add(expected.name);
            missing = string.Join(", ", pending);
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.rKey.wasPressedThisFrame) LoadSeed(selected);
            else if (keys.rightBracketKey.wasPressedThisFrame) LoadSeed((selected + 1) % seeds.Length);
            else if (keys.leftBracketKey.wasPressedThisFrame) LoadSeed((selected + seeds.Length - 1) % seeds.Length);
            if (keys.qKey.wasPressedThisFrame) quarterTurns = (quarterTurns + 1) % 4;
        }

        private void LateUpdate() => FrameStage();

        private void FrameStage()
        {
            float scale = UiScale;
            float left = Mathf.Min(.37f, Sidebar * scale / Screen.width);
            float bottom = Mathf.Min(.32f, Footer * scale / Screen.height);
            Rect region = Rect.MinMaxRect(left + .015f, bottom + .015f, .985f, .96f);
            var rotation = Quaternion.Euler(37f, -57f + quarterTurns * 90f, 0);
            var intoView = Quaternion.Inverse(rotation);
            if (framedTurn != quarterTurns)
            {
                framedTurn = quarterTurns;
                projectedStage = new Bounds(intoView * fixture.arrival.position, Vector3.one * .7f);
                foreach (var mesh in meshes)
                {
                    var e = mesh.extents;
                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        projectedStage.Encapsulate(intoView * (mesh.center + new Vector3(e.x * x, e.y * y, e.z * z)));
                }
            }
            var frame = projectedStage;
            frame.Encapsulate(intoView * pilot.transform.position);
            float height = Mathf.Max((frame.extents.y + .62f) / region.height,
                (frame.extents.x + .62f) / (overview.aspect * region.width));
            overview.orthographicSize = Mathf.Max(2.5f, height);
            var offset = new Vector3((region.center.x - .5f) * 2 * overview.orthographicSize * overview.aspect,
                (region.center.y - .5f) * 2 * overview.orthographicSize, 0f);
            var focus = rotation * (frame.center - offset);
            var forward = rotation * Vector3.forward;
            overview.transform.SetPositionAndRotation(focus - forward * 35f, rotation);
        }

        private static readonly Color Ink = new(.10f, .15f, .15f, .97f);
        private static readonly Color Paper = new(.98f, .94f, .84f);
        private static readonly Color Gold = new(.99f, .78f, .44f);

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnGUI()
        {
            if (!fixture) return;
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15 };
            hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 5, 2, 2) };
            titleStyle.normal.textColor = labelStyle.normal.textColor = Paper;
            hintStyle.normal.textColor = Gold;
            float scale = UiScale;
            float width = Screen.width / scale, height = Screen.height / scale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, Sidebar, height), Ink);
            GUI.Label(new Rect(14, 12, Sidebar - 20, 34), "Combo tests", titleStyle);
            float listHeight = Mathf.Min(height - 108f, seeds.Length * 36f);
            seedScroll = GUI.BeginScrollView(new Rect(8, 60, Sidebar - 16, listHeight),
                seedScroll, new Rect(0, 0, Sidebar - 35, seeds.Length * 36f));
            for (int i = 0; i < seeds.Length; i++)
            {
                float y = i * 36f;
                if (GUI.Button(new Rect(2, y, Sidebar - 38, 32), seeds[i].title, buttonStyle)) LoadSeed(i);
                if (i == selected) Fill(new Rect(2, y + 3, 3, 26), Gold);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(14, height - 40, Sidebar - 20, 32), "[  /  ]    Change setup", hintStyle);

            float x = Sidebar + 16, yBase = height - Footer;
            Fill(new Rect(Sidebar, yBase, width - Sidebar, Footer), Ink);
            string status = unexpected != null ? "Unexpected hit · " + unexpected
                : matched.Count == fixture.expected.Length ? "Passed"
                : observed.Count == 0 ? "Ready"
                : Time.time - lastHitTime > 5f ? "Incomplete" : "Running";
            GUI.Label(new Rect(x, yBase + 6, width - x - 190, 28),
                $"{status}   ·   {matched.Count}/{fixture.expected.Length} hits   ·   {score.Score} pts", labelStyle);
            if (GUI.Button(new Rect(width - 182, yBase + 7, 80, 26), "E   Gust")) gust.Pulse();
            if (GUI.Button(new Rect(width - 96, yBase + 7, 84, 26), "R   Reseed")) LoadSeed(selected);
            GUI.Label(new Rect(x, yBase + 36, width - x - 10, 39),
                observed.Count == 0 ? fixture.instruction :
                    status == "Incomplete" ? $"Still waiting for {missing}" : progress, hintStyle);
            GUI.Label(new Rect(x, yBase + 81, width - x - 8, 26),
                $"{gust.Prompt}     ·     Q rotate view     ·     WASD fly     ·     Space / Shift height", hintStyle);

            if (observed.Count == 0)
            {
                var box = fixture.expected[0].GetComponent<Collider>().bounds;
                var pos = overview.WorldToScreenPoint(box.center + Vector3.up * (box.extents.y + .3f));
                string name = fixture.expected[0].name;
                float tag = hintStyle.CalcSize(new GUIContent(name)).x + 16f;
                var rect = new Rect(pos.x / scale - tag * .5f, height - pos.y / scale - 24, tag, 24);
                Fill(rect, Ink);
                GUI.Label(new Rect(rect.x + 6, rect.y + 1, tag - 8, 22), name, hintStyle);
            }
            GUI.matrix = previous;
        }
    }
}
