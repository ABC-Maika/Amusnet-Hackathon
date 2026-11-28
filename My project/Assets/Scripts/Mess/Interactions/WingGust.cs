using UnityEngine;
using UnityEngine.InputSystem;

namespace Amusnet.Mess
{
    [RequireComponent(typeof(MessManager))]
    public sealed class WingGust : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Animator wings;
        [SerializeField] private ParticleSystem gust;
        [SerializeField] private AudioSource sound;
        [SerializeField] private float reach = 5.8f;
        private MessManager mess;
        private CharacterController motor;
        private Interactable lastImpact, target;
        private RollingCask movingCask;
        private float linkedUntil, readyAt, stroke, roll, previousYaw;
        private Quaternion rest;
        private readonly Collider[] candidates = new Collider[96];
        private float nextScan, shooedUntil;
        public bool IsShooed => Time.time < shooedUntil;
        public void ArriveAtDistrict()
        {
            lastImpact = null;
            movingCask = null;
            target = null;
            readyAt = shooedUntil = linkedUntil = nextScan = stroke = roll = 0f;
            previousYaw = transform.eulerAngles.y;
        }
        public void Shoo()
        {
            linkedUntil = 0f;
            shooedUntil = Time.time + 1.5f;
            readyAt = Mathf.Max(readyAt, shooedUntil);
        }
        private Transform Candidate => movingCask ? movingCask.Drum : target ? target.transform : null;
        public string Prompt => IsShooed ? "Spotted!  Fly up or find cover" : movingCask ? "E   Flick the rolling cask" :
            Time.time < readyAt ? "Gust recovering" : target ? $"E   Gust · {target.name}" : "E   Wing gust";
        public float Charge => Mathf.Clamp01(1f - (readyAt - Time.time) / 1.15f);

        private void Awake()
        {
            mess = GetComponent<MessManager>();
            motor = GetComponent<CharacterController>();
            rest = visual.localRotation;
            previousYaw = transform.eulerAngles.y;
        }
        private void OnEnable() => mess.MessCreated += Observe;
        private void OnDisable() => mess.MessCreated -= Observe;

        private void Observe(Interactable source, int points)
        {
            lastImpact = source;
            linkedUntil = Time.time + 2.45f;
        }

        private void Update()
        {
            if (Time.time >= nextScan)
            {
                SelectTarget();
                nextScan = Time.time + .065f;
            }
            if (Keyboard.current?.eKey.wasPressedThisFrame == true ||
                Gamepad.current?.rightShoulder.wasPressedThisFrame == true)
                Pulse();
        }

        private void SelectTarget()
        {
            movingCask = null;
            target = null;
            Vector3 origin = transform.position;
            float best = float.MaxValue;
            foreach (var cask in RollingCask.InMotion)
            {
                if (!cask.CanFlick || (cask.Drum.position - origin).sqrMagnitude > 81f) continue;
                float distance = (cask.Drum.position - origin).sqrMagnitude;
                if (distance < best) { best = distance; movingCask = cask; }
            }
            if (movingCask) return;
            best = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(origin, reach, candidates, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider collider = candidates[i];
                if (!collider.isTrigger || !collider.TryGetComponent<Interactable>(out var item) || item.HasInteracted) continue;
                Vector3 offset = collider.bounds.center - origin;
                float distance = offset.magnitude;
                if (distance > reach || Vector3.Dot(transform.forward, offset.normalized) < .43f) continue;
                if (Physics.Linecast(origin, collider.bounds.center, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.transform != transform && !hit.transform.IsChildOf(item.transform)) continue;
                float weight = distance + 2f * (1f - Vector3.Dot(transform.forward, offset.normalized));
                if (weight < best) { best = weight; target = item; }
            }
        }

        public bool Pulse()
        {
            if (Time.time < readyAt) return false;
            SelectTarget();
            readyAt = Time.time + 1.15f;
            stroke = 1f;
            Vector3 direction = Candidate ? Candidate.position - transform.position : transform.forward;
            if (direction.sqrMagnitude < .01f) direction = transform.forward;
            gust.transform.rotation = Quaternion.LookRotation(direction);
            gust.Emit(30);
            sound.Play();
            if (movingCask) return movingCask.Flick();
            if (!target) return false;
            bool connect = lastImpact && Time.time <= linkedUntil &&
                (lastImpact.transform.position - target.transform.position).sqrMagnitude < 100f;
            return target.TryInteract(mess, connect ? lastImpact : null);
        }

        private void LateUpdate()
        {
            float yaw = transform.eulerAngles.y;
            float turn = Mathf.Clamp(Mathf.DeltaAngle(previousYaw, yaw), -35f, 35f);
            previousYaw = yaw;
            roll = Mathf.Lerp(roll, 0f, 1f - Mathf.Exp(-4f * Time.deltaTime)) - turn * .7f;
            roll = Mathf.Clamp(roll, -36f, 36f);
            stroke = Mathf.Max(0f, stroke - Time.deltaTime * 2.25f);
            float beat = Mathf.Sin((1f - stroke) * Mathf.PI * 2f) * stroke;
            float pitch = -motor.velocity.y * 3f + beat * 24f;
            visual.localRotation = rest * Quaternion.Euler(pitch, 0f, roll + beat * 16f);
            wings.speed = 1.0f + stroke * 2.1f;
        }
    }
}
