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
        readonly Collider[] nearby = new Collider[16];
        readonly Queue<(string label, int points, float expiresAt)> recentMess = new();
        Interactable[][] districtProps;
        bool showMap;
        Vector3 drift;
        void Awake()
        {
            mover = GetComponent<CharacterController>();
            shelter = GetComponent<ShelterState>();
            mess = GetComponent<MessManager>();
            districtProps = new Interactable[districts.Length][];
            for (int i = 0; i < districts.Length; i++)
                districtProps[i] = districts[i] ? districts[i].GetComponentsInChildren<Interactable>() : Array.Empty<Interactable>();
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

        void ShowAward(Interactable source, int points)
        {
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
            var k = Keyboard.current;
            if (k == null) return;
            if (districts.Length > 0 && k.tabKey.wasPressedThisFrame)
                showMap = !showMap;
            if (mess && k.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(gameObject.scene.path);
                return;
            }
            float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
            float z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            float rise = (k.spaceKey.isPressed ? 4f * weather.LiftMultiplier : 0f) - (k.leftShiftKey.isPressed ? 4f : 0f);
            if (mess && (x != 0f || z != 0f || rise != 0f))
                mess.BeginRun();
            drift = shelter.IsHidden ? Vector3.zero : wind.Sample(transform.position, Time.time);
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
            CheckInteractionVolumes();
            view.transform.position = transform.position + new Vector3(0,4,-7);
            view.transform.LookAt(transform.position + Vector3.forward * 1.8f);
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

        private void DrawDistrictMap()
        {
            float size = Mathf.Min(Screen.width - 28f, Screen.height - 64f, 475f);
            var frame = new Rect(Screen.width - size - 14f, 16f, size, size);
            GUI.Box(frame, "Village districts");
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
                GUI.Box(new Rect(point.x - 49f, point.y - 14f, 98f, 39f), $"{district.name}\n{sum} / {total}");
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
            return closest ? closest.name : "Village flight";
        }
    }
}
