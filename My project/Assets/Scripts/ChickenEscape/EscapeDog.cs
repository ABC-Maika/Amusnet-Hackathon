using UnityEngine;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EscapeDog : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform model;

        static readonly int Speed = Animator.StringToHash("Speed");
        CharacterController motor;
        Vector3 velocity;
        float vertical;
        float barkAt;
        float heldUntil;

        public Vector3 Velocity => velocity;
        public float BarkCharge => Mathf.Clamp01(1f - (barkAt - Time.time) / 1.65f);
        public bool IsStopped => Time.time < heldUntil;

        void Awake() => motor = GetComponent<CharacterController>();

        public void Step(Vector2 input, bool sprint, bool controlled, bool dragging)
        {
            if (IsStopped) input = Vector2.zero;
            Vector3 direction = new(input.x, 0f, input.y);
            float pace = dragging ? 3.9f : sprint ? 7.6f : 5.25f;
            float response = controlled ? direction.sqrMagnitude > .01f ? 27f : 32f : 5.8f;
            velocity = Vector3.MoveTowards(velocity, direction * pace, response * Time.deltaTime);
            if (velocity.sqrMagnitude > .04f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(velocity), 840f * Time.deltaTime);

            vertical = motor.isGrounded ? -2f : vertical - 19f * Time.deltaTime;
            motor.Move((velocity + Vector3.up * vertical) * Time.deltaTime);
            float actual = Vector3.ProjectOnPlane(motor.velocity, Vector3.up).magnitude;
            animator.SetFloat(Speed, Mathf.Min(2f, actual / 3.8f), .095f, Time.deltaTime);
            float bank = Vector3.Dot(direction, transform.right) * -11f * Mathf.Clamp01(actual / 5f);
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(0f,0f,bank),
                1f - Mathf.Exp(-13f * Time.deltaTime));
        }

        public bool Bark()
        {
            if (Time.time < barkAt || IsStopped) return false;
            barkAt = Time.time + 1.65f;
            return true;
        }

        public void Hesitate()
        {
            if (IsStopped) return;
            heldUntil = Time.time + .75f;
            velocity *= .18f;
        }
    }
}
