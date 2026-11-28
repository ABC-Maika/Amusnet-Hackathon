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
        CharacterController mover;
        ShelterState shelter;
        MessManager mess;
        readonly Collider[] nearby = new Collider[16];
        readonly Queue<(string label, int points, float expiresAt)> recentMess = new();
        Vector3 drift;
        void Awake()
        {
            mover = GetComponent<CharacterController>();
            shelter = GetComponent<ShelterState>();
            mess = GetComponent<MessManager>();
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
            Vector3 velocity = Vector3.ClampMagnitude(new Vector3(x, 0, z), 1f) * 6f + drift +
                Vector3.up * (rise - (shelter.IsHidden ? 0f : weather.RainSink));
            Vector3 next = transform.position + velocity * Time.deltaTime;
            next = new Vector3(Mathf.Clamp(next.x,-30,30), Mathf.Clamp(next.y,0.55f,13f), Mathf.Clamp(next.z,-27,37));
            mover.Move(next - transform.position);
            if (x != 0 || z != 0) transform.forward = new Vector3(x,0,z);
            float flap = Mathf.Sin(Time.time * 22f) * 32f;
            leftWing.localRotation = Quaternion.Euler(0,0,flap);
            rightWing.localRotation = Quaternion.Euler(0,0,-flap);
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
            GUI.Box(new Rect(12,12,390,74),"Village flight");
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
            string hint = mess.Score == 0 ? "Try the laundry or chicken on the flower trail" : $"Milestone {mess.LastThreshold}";
            GUI.Label(new Rect(24,130,280,20), hint);
            GUI.Label(new Rect(305,130,85,20), "R restart");

            while (recentMess.Count > 0 && recentMess.Peek().expiresAt <= Time.unscaledTime)
                recentMess.Dequeue();
            int top = 166;
            foreach (var (label, points, _) in recentMess)
            {
                GUI.Box(new Rect(12,top,245,24), $"{label}  +{points}");
                top += 27;
            }
        }
    }
}
