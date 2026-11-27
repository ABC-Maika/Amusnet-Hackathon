using UnityEngine;
using UnityEngine.InputSystem;

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
        Amusnet.Mess.MessManager mess;
        Vector3 drift;
        void Awake() { mover = GetComponent<CharacterController>(); shelter = GetComponent<ShelterState>(); mess = GetComponent<Amusnet.Mess.MessManager>(); }
        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
            float z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            float rise = (k.spaceKey.isPressed ? 4f * weather.LiftMultiplier : 0f) - (k.leftShiftKey.isPressed ? 4f : 0f);
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
            view.transform.position = transform.position + new Vector3(0,4,-7);
            view.transform.LookAt(transform.position + Vector3.forward * 1.8f);
            view.backgroundColor = RenderSettings.fogColor;
        }
        void OnGUI()
        {
            if (mess) GUI.Box(new Rect(12,90,390,26), $"Mess: {mess.Score}     Last milestone: {mess.LastThreshold}     Brush hay bales to score");
            GUI.Box(new Rect(12,12,390,74),"Sprint 2   •   Village flight");
            GUI.Label(new Rect(24,37,375,20),"WASD move     Space climb     Left Shift descend");
            GUI.Label(new Rect(24,58,375,20),$"{(shelter.IsHidden ? "Hidden" : "Exposed")}    {(weather.Daylight < .3f ? "Night" : "Day")}    Rain {weather.Rain:P0}    Wind {drift.magnitude:F1} m/s");
        }
    }
}
