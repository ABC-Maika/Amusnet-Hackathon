using UnityEngine;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EscapeButterfly : MonoBehaviour
    {
        [SerializeField] Animator wings;
        [SerializeField] Transform model;
        [SerializeField] ParticleSystem gust;

        static readonly int Flapping = Animator.StringToHash("Flapping");
        static readonly int FlightPose = Animator.StringToHash("FlightPose");
        static readonly int LegPose = Animator.StringToHash("FlightLegPose");
        static readonly int FlapRate = Animator.StringToHash("FlightFlapRate");
        CharacterController motor;
        Vector3 motion;
        float takeoff;

        void Awake() => motor = GetComponent<CharacterController>();

        public void Board()
        {
            takeoff = Time.time + .3f;
            wings.Play("Base Layer.Takeoff", 0, 0f);
        }

        public void Step(Vector2 input, float climb, bool controlled)
        {
            float floor = .58f;
            if (Physics.Raycast(transform.position + Vector3.up * .28f, Vector3.down, out var ground, 5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                floor += ground.point.y;
            float lift = controlled ? climb * 2.85f : Mathf.Clamp((floor - transform.position.y) * 5f, -2.8f, 2.4f);
            float ceiling = 3.65f;
            float height = Mathf.Clamp(transform.position.y + lift * Time.deltaTime, floor - .13f, Mathf.Max(floor, ceiling));
            Vector3 desired = new Vector3(input.x * 5.8f, 0f, input.y * 5.8f);
            motion = Vector3.MoveTowards(motion, desired, (controlled ? 25f : 15f) * Time.deltaTime);
            motor.Move(motion * Time.deltaTime + Vector3.up * (height - transform.position.y));
            if (motion.sqrMagnitude > .015f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(motion), 960f * Time.deltaTime);

            bool flying = controlled || Mathf.Abs(height - floor) > .12f;
            wings.SetBool(Flapping, flying);
            wings.SetInteger(FlightPose, controlled && Time.time < takeoff ? 1 : flying ? 2 : 0);
            wings.SetFloat(LegPose, Mathf.InverseLerp(-2.7f, 2.7f, lift), .12f, Time.deltaTime);
            wings.SetFloat(FlapRate, flying ? 1.13f : .85f, .12f, Time.deltaTime);
            float bank = Vector3.Dot(new Vector3(input.x,0f,input.y), transform.right) * -22f;
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(-lift * 8f,0f,bank),
                1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        public void GustAt(Vector3 point)
        {
            if (!gust) return;
            var direction = point - transform.position;
            if (direction.sqrMagnitude > .01f) gust.transform.rotation = Quaternion.LookRotation(direction);
            gust.Emit(24);
        }
    }
}
