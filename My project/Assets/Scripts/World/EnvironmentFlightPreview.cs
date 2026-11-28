using System;
using System.Collections.Generic;
using Amusnet.Mess;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Amusnet.World
{
    [RequireComponent(typeof(CharacterController), typeof(ShelterState))]
    public sealed class EnvironmentFlightPreview : MonoBehaviour
    {
        public WindSystem wind;
        public WeatherSystem weather;
        public Camera view;
        public Transform leftWing, rightWing;
        [SerializeField] private Vector2 xLimits = new(-30f, 30f);
        [SerializeField] private Vector2 yLimits = new(.55f, 13f);
        [SerializeField] private Vector2 zLimits = new(-27f, 37f);
        [SerializeField, Min(.1f)] private float horizontalSpeed = 6f;
        [SerializeField] private Transform[] districts = Array.Empty<Transform>();
        [SerializeField] private string openingHint = "Try the laundry or chicken on the flower trail";
        CharacterController mover;
        ShelterState shelter;
        MessManager mess;
        GameFlow flow;
        Transform cameraPose;
        readonly Collider[] nearby = new Collider[16];
        readonly Queue<(string label, int points, float expiresAt)> recentMess = new();
        Interactable[][] districtProps;
        [SerializeField] private bool marketView;
        [SerializeField] private bool comboTest;
        private WingGust gust;
        private int bestChain;
        private readonly Dictionary<Transform, int> districtChains = new();
        private Interactable lastImpact;
        private float focusedUntil;
        private Vector3 cameraBias;
        private RollingCask rollingFocus;
        private float crane;
        private static string restartDistrict;
        private Transform visitedDistrict;
        private float clueUntil;
        private readonly Queue<(Vector3 position, int points, float started)> impactLabels = new();
        private bool launched;
        private GUIStyle districtStyle, scoreStyle, noteStyle;
        bool showMap;
        Vector3 drift;
        void Awake()
        {
            mover = GetComponent<CharacterController>();
            shelter = GetComponent<ShelterState>();
            mess = GetComponent<MessManager>();
            gust = GetComponent<WingGust>();
            districtProps = new Interactable[districts.Length][];
            for (int i = 0; i < districts.Length; i++)
                districtProps[i] = districts[i] ? districts[i].GetComponentsInChildren<Interactable>() : Array.Empty<Interactable>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRestart() => restartDistrict = null;

        private void Start()
        {
            if (!marketView || string.IsNullOrEmpty(restartDistrict)) return;
            foreach (var district in districts)
                if (district && district.name == restartDistrict) Visit(district);
            restartDistrict = null;
        }

        public void AttachToRun(GameFlow gameFlow, Camera output, Transform pose)
        {
            flow = gameFlow;
            view = output;
            cameraPose = pose;
            mess.Flow = gameFlow;
            mess.ScoreChanged += UpdateRunScore;
            flow.StateChanged += OnRunState;
            OnRunState(flow.Current);
            UpdateRunScore(mess.Score);
            UpdateCamera();
        }

        private void OnRunState(GameFlow.State state)
        {
            // Leave the capsule in place during pause so it keeps contact with nearby shelters.
            if (state == GameFlow.State.Menu) mover.enabled = false;
            if (state == GameFlow.State.Playing)
            {
                mover.enabled = true;
                mess.BeginRun();
            }
        }

        private void UpdateRunScore(int score)
        {
            if (GameHUD.Instance) GameHUD.Instance.SetMess((float)score / mess.GoalScore);
            if (mess.HasReachedGoal) flow.Win();
        }

        private void Visit(Transform district)
        {
            var destination = district.GetComponent<MarketDistrict>();
            if (!destination || !destination.approach) return;
            mover.enabled = false;
            transform.SetPositionAndRotation(destination.approach.position, destination.approach.rotation);
            mover.enabled = true;
            showMap = false;
            if (gust) gust.ArriveAtDistrict();
            launched = false;
            lastImpact = null;
            rollingFocus = null;
            cameraBias = Vector3.zero;
            crane = 0f;
            impactLabels.Clear();
            clueUntil = Time.time + 6f;
        }

        void OnEnable()
        {
            if (mess) mess.MessCreated += ShowAward;
        }

        void OnDisable()
        {
            if (mess) mess.MessCreated -= ShowAward;
            recentMess.Clear();
        }

        void OnDestroy()
        {
            if (flow) flow.StateChanged -= OnRunState;
            if (mess) mess.ScoreChanged -= UpdateRunScore;
        }

        void ShowAward(Interactable source, int points)
        {
            if (marketView)
            {
                if (impactLabels.Count >= 7) impactLabels.Dequeue();
                impactLabels.Enqueue((source.transform.position + Vector3.up * 1.35f, points, Time.time));
            }
            lastImpact = source;
            launched = true;
            if (source is RollingCask cask) rollingFocus = cask;
            bestChain = Mathf.Max(bestChain, source.ChainRoot.ChainLength);
            var area = source.transform.parent;
            if (marketView && area)
            {
                districtChains.TryGetValue(area, out int districtBest);
                districtChains[area] = Mathf.Max(districtBest, source.ChainRoot.ChainLength);
            }
            focusedUntil = Time.time + 2.8f;
            if (recentMess.Count == 3) recentMess.Dequeue();
            recentMess.Enqueue((source.name, points, Time.unscaledTime + 4f));
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // A solid object can stop the controller before it overlaps the trigger.
            if (!mess) return;
            hit.collider.GetComponentInParent<Interactable>()?.TryInteract(mess);
        }
        void Update()
        {
            if (flow && !flow.IsRunning) return;
            var k = Keyboard.current;
            if (k == null) return;
            if (!comboTest && districts.Length > 0 && k.tabKey.wasPressedThisFrame)
                showMap = !showMap;
            if (!comboTest && mess && k.rKey.wasPressedThisFrame)
            {
                if (marketView) restartDistrict = ClosestDistrict()?.name;
                if (flow) flow.Restart();
                else SceneManager.LoadScene(gameObject.scene.path);
                return;
            }
            float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
            float z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            float rise = (k.spaceKey.isPressed ? 4f * weather.LiftMultiplier : 0f) - (k.leftShiftKey.isPressed ? 4f : 0f);
            if (mess && (x != 0f || z != 0f || rise != 0f))
                mess.BeginRun();
            launched |= x != 0 || z != 0 || rise != 0f;
            drift = shelter.IsHidden || (marketView && !launched) ? Vector3.zero : wind.Sample(transform.position, Time.time);
            Vector3 velocity = Vector3.ClampMagnitude(new Vector3(x, 0, z), 1f) * horizontalSpeed + drift +
                Vector3.up * (rise - (shelter.IsHidden ? 0f : weather.RainSink));
            Vector3 next = transform.position + velocity * Time.deltaTime;
            next = new Vector3(Mathf.Clamp(next.x, xLimits.x, xLimits.y), Mathf.Clamp(next.y, yLimits.x, yLimits.y),
                Mathf.Clamp(next.z, zLimits.x, zLimits.y));
            mover.Move(next - transform.position);
            if (x != 0 || z != 0) transform.forward = new Vector3(x,0,z);
            float flap = Mathf.Sin(Time.time * 22f) * 32f;
            if (leftWing) leftWing.localRotation = Quaternion.Euler(0,0,flap);
            if (rightWing) rightWing.localRotation = Quaternion.Euler(0,0,-flap);
        }
        void LateUpdate()
        {
            if (!flow || flow.IsRunning) CheckInteractionVolumes();
            if (comboTest) return;
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            Vector3 focus = transform.position;
            if (marketView)
            {
                bool rolling = rollingFocus && rollingFocus.IsRolling &&
                    (rollingFocus.Drum.position - focus).sqrMagnitude < 196f;
                Vector3 subject = rolling ? rollingFocus.Drum.position :
                    lastImpact && Time.time < focusedUntil ? lastImpact.transform.position : focus;
                Vector3 bias = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(subject - focus, Vector3.up) * .62f, 4.7f);
                crane = Mathf.Lerp(crane, rolling ? 1f : 0f, 1f - Mathf.Exp(-3f * Time.deltaTime));
                cameraBias = Vector3.Lerp(cameraBias, bias, 1f - Mathf.Exp(-3.3f * Time.deltaTime));
                focus += cameraBias;
            }
            var pose = cameraPose ? cameraPose : view.transform;
            Vector3 offset = marketView
                ? new Vector3(1.45f + crane * 3.15f, 3.15f + 3.35f * crane, -5.8f - crane * .55f)
                : new Vector3(0, 4f, -7f);
            Vector3 position = focus + offset;
            if (marketView)
            {
                // Sweep from above the flight capsule so a hillside, wall, or tree cannot swallow the view.
                Vector3 anchor = transform.position + Vector3.up * .6f;
                Vector3 travel = position - anchor;
                Vector3 direction = travel.normalized;
                if (Physics.SphereCast(anchor, .22f, direction, out var obstruction, travel.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    position = anchor + direction * Mathf.Max(0f, obstruction.distance - .12f);
            }
            pose.position = position;
            pose.LookAt(focus + Vector3.forward * 1.8f);
            view.backgroundColor = RenderSettings.fogColor;
        }
        void CheckInteractionVolumes()
        {
            if (!mess || !mover.enabled) return;
            // Check the flight capsule after Move. A fast controller can cross a trigger
            // between physics notifications.
            Bounds bounds = mover.bounds;
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            float halfSegment = Mathf.Max(0f, bounds.extents.y - radius);
            Vector3 offset = Vector3.up * halfSegment;
            int count = Physics.OverlapCapsuleNonAlloc(bounds.center - offset, bounds.center + offset,
                radius, nearby, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var other = nearby[i];
                if (other.isTrigger)
                    other.GetComponentInParent<Interactable>()?.TryInteract(mess);
            }
        }
        void OnGUI()
        {
            if (comboTest || (flow && !flow.IsRunning)) return;
            if (marketView && mess)
            {
                DrawMarketHud();
                if (showMap) DrawDistrictMap();
                return;
            }
            GUI.Box(new Rect(12,12,390,74), NearestDistrict());
            GUI.Label(new Rect(24,37,375,20),"WASD move     Space climb     Left Shift descend");
            GUI.Label(new Rect(24,58,375,20),$"{(shelter.IsHidden ? "Hidden" : "Exposed")}    {(weather.Daylight < .3f ? "Night" : "Day")}    Rain {weather.Rain:P0}    Wind {drift.magnitude:F1} m/s");
            if (!mess) return;

            var elapsed = TimeSpan.FromSeconds(mess.ElapsedSeconds);
            string time = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
            string heading = mess.HasReachedGoal ? $"Goal reached in {time}" : $"Mess: {mess.Score} / {mess.GoalScore}     {time}";
            GUI.Box(new Rect(12,90,390,70), heading);

            float fraction = Mathf.Clamp01((float)mess.Score / mess.GoalScore);
            Color original = GUI.color;
            GUI.color = new Color(.18f,.22f,.17f);
            GUI.DrawTexture(new Rect(24,114,366,8), Texture2D.whiteTexture);
            GUI.color = new Color(.83f,.93f,.51f);
            if (fraction > 0f)
                GUI.DrawTexture(new Rect(24,114,366 * fraction,8), Texture2D.whiteTexture);
            GUI.color = original;
            string hint = mess.Score == 0 ? openingHint : $"Milestone {mess.LastThreshold}";
            GUI.Label(new Rect(24,130,280,20), hint);
            GUI.Label(new Rect(285,130,110,20), districts.Length == 0 ? "R restart" : "Tab map  ·  R reset");

            while (recentMess.Count > 0 && recentMess.Peek().expiresAt <= Time.unscaledTime)
                recentMess.Dequeue();
            int top = 166;
            foreach (var (label, points, _) in recentMess)
            {
                GUI.Box(new Rect(12,top,245,24), $"{label}  +{points}");
                top += 27;
            }
            if (showMap) DrawDistrictMap();
        }

        private static void Panel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawMarketHud()
        {
            districtStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            scoreStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            noteStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13 };
            Color ink = new Color(.12f, .16f, .15f, .94f);
            Color wheat = new Color(.96f, .77f, .48f);
            districtStyle.normal.textColor = wheat;
            scoreStyle.normal.textColor = noteStyle.normal.textColor = new Color(.98f, .95f, .86f);
            Panel(new Rect(16, 14, 220, 68), ink);
            var district = ClosestDistrict();
            if (district != visitedDistrict)
            {
                visitedDistrict = district;
                clueUntil = Time.time + 6f;
            }
            GUI.Label(new Rect(27, 19, 198, 22), district ? district.name : "Village", districtStyle);
            var time = TimeSpan.FromSeconds(mess.ElapsedSeconds);
            GUI.Label(new Rect(26, 37, 140, 34), $"{mess.Score} / {mess.GoalScore}", scoreStyle);
            GUI.Label(new Rect(179, 47, 54, 22), $"{(int)time.TotalMinutes}:{time.Seconds:00}", noteStyle);
            Panel(new Rect(16, 79, 220f * Mathf.Clamp01((float)mess.Score / mess.GoalScore), 3), wheat);

            string note = mess.HasReachedGoal ? "Village goal reached" : mess.Score == 0 ? openingHint : "";
            if (district && (!districtChains.ContainsKey(district) || Time.time < clueUntil) &&
                district.TryGetComponent<MarketDistrict>(out var market))
                note = market.clue;
            else if (note.Length == 0 && districtChains.Count < districts.Length)
                note = "Tab · Find another district";
            if (lastImpact && Time.time < focusedUntil)
            {
                int count = lastImpact.ChainRoot.ChainLength;
                note = (count > 1 ? $"{count} in chain   ·   " : "") + lastImpact.name + $"   +{lastImpact.Points}";
            }
            if (note.Length > 0)
            {
                float width = noteStyle.CalcSize(new GUIContent(note)).x + 22;
                Panel(new Rect(16, 88, width, 29), ink);
                GUI.Label(new Rect(26, 92, width, 24), note, noteStyle);
            }
            if (flow && PredatorSystem.Instance is { } predator &&
                (predator.IsHidden || predator.Percent >= 55f))
            {
                string cover = predator.IsHidden ? "In cover · Predator pressure falling" : "Take cover beneath a large flower";
                float y = note.Length > 0 ? 121f : 88f;
                float width = noteStyle.CalcSize(new GUIContent(cover)).x + 22f;
                Panel(new Rect(16, y, width, 29), ink);
                GUI.Label(new Rect(26, y + 4f, width, 24), cover, noteStyle);
            }
            while (impactLabels.Count > 0 && Time.time - impactLabels.Peek().started > 1.12f)
                impactLabels.Dequeue();
            foreach (var hit in impactLabels)
            {
                float age = Time.time - hit.started;
                Vector3 screen = view.WorldToScreenPoint(hit.position + Vector3.up * age * .8f);
                if (screen.z <= 0f) continue;
                float y = Screen.height - screen.y;
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((1.12f - age) * 4f));
                Panel(new Rect(screen.x - 14f, y, 30, 22), ink);
                GUI.Label(new Rect(screen.x - 10, y, 28, 21), $"+{hit.points}", districtStyle);
                GUI.color = previous;
            }
            if (gust)
            {
                string prompt = gust.Prompt;
                float w = Mathf.Max(140f, districtStyle.CalcSize(new GUIContent(prompt)).x + 26f);
                float x = Screen.width * .5f - w * .5f;
                Panel(new Rect(x, Screen.height - 92f, w, 32f), ink);
                GUI.Label(new Rect(x + 11, Screen.height - 87f, w, 23), prompt, districtStyle);
                Panel(new Rect(x, Screen.height - 61f, w * gust.Charge, 2f), wheat);
                if (bestChain >= 2)
                {
                    string record = bestChain >= 8 ? $"Market stunt   ★   {bestChain} in chain" : $"Best chain   {bestChain} / 8";
                    float width = noteStyle.CalcSize(new GUIContent(record)).x + 20f;
                    float y = flow ? Screen.height - 40f : 14f;
                    Panel(new Rect(Screen.width - width - 16, y, width, 27), ink);
                    GUI.Label(new Rect(Screen.width - width - 6, y + 4f, width, 22), record, noteStyle);
                }
            }
            const string controls = "WASD fly   ·   Space / Shift height   ·   Tab map   ·   R restart";
            float line = noteStyle.CalcSize(new GUIContent(controls)).x + 20f;
            float controlsY = Screen.height - (flow ? 74f : 40f);
            Panel(new Rect(16, controlsY, line, 26), ink);
            GUI.Label(new Rect(25, controlsY + 3f, line, 22), controls, noteStyle);
        }

        private void DrawDistrictMap()
        {
            float top = flow ? 125f : 16f;
            float size = Mathf.Min(Screen.width - 28f, Screen.height - top - 14f, 475f);
            var frame = new Rect(Screen.width - size - 14f, top, size, size);
            GUI.Box(frame, marketView ? "Choose a district to fly there" : "Village districts");
            var map = new Rect(frame.x + 56f, frame.y + 43f, size - 112f, size - 100f);
            for (int index = 0; index < districts.Length; index++)
            {
                var district = districts[index];
                if (!district) continue;
                var point = MapPosition(map, district.position);
                int sum = 0;
                int total = 0;
                foreach (var item in districtProps[index])
                {
                    total += item.Points;
                    if (item.HasInteracted) sum += item.Points;
                }
                if (marketView)
                {
                    districtChains.TryGetValue(district, out int chain);
                    if (GUI.Button(new Rect(point.x - 50f, point.y - 20f, 100f, 56f), $"{district.name}\n{sum} / {total}\nChain  {chain} / 8"))
                        Visit(district);
                }
                else GUI.Box(new Rect(point.x - 49f, point.y - 14f, 98f, 39f), $"{district.name}\n{sum} / {total}");
            }
            var player = MapPosition(map, transform.position);
            Color original = GUI.color;
            GUI.color = new Color(.88f, .97f, .45f);
            GUI.DrawTexture(new Rect(player.x - 4f, player.y - 4f, 8f, 8f), Texture2D.whiteTexture);
            GUI.Label(new Rect(frame.x + 18f, frame.yMax - 29f, 160f, 22f), "■ Your position");
            GUI.color = original;
        }

        private Vector2 MapPosition(Rect map, Vector3 position)
        {
            return new Vector2(
                Mathf.Lerp(map.xMin, map.xMax, Mathf.InverseLerp(xLimits.x, xLimits.y, position.x)),
                Mathf.Lerp(map.yMax, map.yMin, Mathf.InverseLerp(zLimits.x, zLimits.y, position.z)));
        }

        private string NearestDistrict()
        {
            var district = ClosestDistrict();
            return district ? district.name : "Village flight";
        }

        private Transform ClosestDistrict()
        {
            Transform closest = null;
            float best = float.PositiveInfinity;
            foreach (var district in districts)
            {
                if (!district) continue;
                Vector3 delta = district.position - transform.position;
                float distance = delta.x * delta.x + delta.z * delta.z;
                if (distance >= best) continue;
                closest = district;
                best = distance;
            }
            return closest;
        }
    }
}
