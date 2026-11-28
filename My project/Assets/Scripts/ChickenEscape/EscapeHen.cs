using System.Collections;
using UnityEngine;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EscapeHen : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform model;

        static readonly int Speed = Animator.StringToHash("Speed");
        CharacterController motor;
        Vector3 home, motion, burst, fleeDirection;
        float burstUntil, fleeUntil, routeUntil, vertical, idlePhase;

        public bool Saved { get; private set; }

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            home = transform.position;
            idlePhase = Mathf.Abs(home.x * .61f + home.z * .38f);
        }

        public void Step(EscapeDog dog, EscapeHen[] flock, Vector3? route)
        {
            if (Saved) return;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - dog.transform.position, Vector3.up);
            bool close = away.sqrMagnitude < 10.9f && (dog.Velocity.sqrMagnitude > .15f || away.sqrMagnitude < 1.82f);
            bool seesDog = close && !Physics.Linecast(transform.position + Vector3.up * .28f,
                dog.transform.position + Vector3.up * .18f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (seesDog && away.sqrMagnitude > .001f)
            {
                fleeDirection = away.normalized;
                fleeUntil = Time.time + .66f;
                if (route.HasValue && Vector3.Dot(fleeDirection, Toward(route.Value)) > .45f)
                    routeUntil = Mathf.Max(routeUntil, Time.time + 1.35f);
            }
            Vector3 steer;
            float speed;
            if (route.HasValue && Time.time < routeUntil)
            {
                steer = Toward(route.Value);
                speed = Time.time < burstUntil ? 4.3f : 3.1f;
                home = transform.position;
            }
            else if (Time.time < burstUntil)
            {
                steer = burst;
                speed = 4.3f;
                home = transform.position;
            }
            else if (Time.time < fleeUntil)
            {
                steer = fleeDirection;
                speed = Mathf.Lerp(3.85f, 2.18f, Mathf.Clamp01(away.magnitude / 3.4f));
                home = transform.position;
            }
            else
            {
                float t = Time.time * .66f + idlePhase;
                Vector3 wandering = home + new Vector3(Mathf.Sin(t) * .48f,0f,Mathf.Cos(t * .93f) * .32f);
                steer = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(wandering - transform.position, Vector3.up), 1f);
                speed = Mathf.Sin(t * .71f + .6f) > -.1f ? 1.2f : 0f;
            }

            foreach (var other in flock)
            {
                if (other == this || other.Saved) continue;
                Vector3 separation = Vector3.ProjectOnPlane(transform.position-other.transform.position, Vector3.up);
                float distance = separation.magnitude;
                if (distance is > .015f and < .64f)
                    steer += separation / distance * (.64f - distance) * 1.65f;
            }

            if (steer.sqrMagnitude > .02f)
            {
                Vector3 forward = steer.normalized;
                if (Physics.SphereCast(transform.position + Vector3.up * .27f, .14f, forward, out var wall,
                    .48f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Vector3 across = Vector3.ProjectOnPlane(wall.normal, Vector3.up);
                    Vector3 side = Vector3.Cross(Vector3.up, across).normalized;
                    if (Vector3.Dot(side, forward + motion * .2f) < 0f) side = -side;
                    steer = side * .8f + across * .22f;
                }
            }

            motion = Vector3.MoveTowards(motion, Vector3.ClampMagnitude(steer, 1f)*speed, 18f*Time.deltaTime);
            vertical = motor.isGrounded ? -1.5f : vertical - 19f * Time.deltaTime;
            motor.Move((motion + Vector3.up * vertical)*Time.deltaTime);
            if (motion.sqrMagnitude > .025f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(motion), 790f * Time.deltaTime);
            animator.SetFloat(Speed, Mathf.Min(2f, Vector3.ProjectOnPlane(motor.velocity,Vector3.up).magnitude/1.62f),
                .09f, Time.deltaTime);
            float peck = speed < 1.3f ? Mathf.Max(0f, Mathf.Sin(Time.time * 5.8f + idlePhase)) : 0f;
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(peck * peck * 14f,0f,0f),
                1f - Mathf.Exp(-13f*Time.deltaTime));
        }

        public void Startle(Vector3 origin, Vector3 facing, Vector3? route)
        {
            if (Saved) return;
            Vector3 away = Vector3.ProjectOnPlane(transform.position-origin, Vector3.up);
            if (away.sqrMagnitude is < .0001f or > 36f || Physics.Linecast(origin + Vector3.up * .24f,
                transform.position + Vector3.up *.28f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            float angle = Vector3.Dot(facing, away.normalized);
            if (angle < .18f && away.sqrMagnitude > 2.2f * 2.2f) return;
            Vector3 direction = angle > .18f ? Vector3.Lerp(away.normalized, facing, .73f) : away.normalized;
            if (route.HasValue && Vector3.Dot(direction, Toward(route.Value)) > -.03f)
                routeUntil = Time.time + 2.35f;
            else
                routeUntil = 0f;
            burst = Quaternion.Euler(0f, Mathf.Sin(idlePhase) * 12f, 0f) * direction.normalized;
            burstUntil = Time.time + 1.27f;
            home = transform.position;
        }

        Vector3 Toward(Vector3 point) => Vector3.ProjectOnPlane(point - transform.position, Vector3.up).normalized;

        public void Rescue(Vector3 exitDirection)
        {
            Saved = true;
            motor.enabled = false;
            StartCoroutine(RunToSafety(exitDirection));
        }

        IEnumerator RunToSafety(Vector3 direction)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.SetFloat(Speed, 2f);
            float left = 1.2f;
            while (left > 0f)
            {
                float dt = Time.unscaledDeltaTime;
                left -= dt;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 760f * dt);
                transform.position += transform.forward * (3.05f * dt);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
