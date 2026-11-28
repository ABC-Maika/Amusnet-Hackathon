using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EscapeHen : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform model;

        static readonly int Speed = Animator.StringToHash("Speed");
        readonly Vector3[] corners = new Vector3[16];
        CharacterController motor;
        NavMeshPath path;
        Vector3 home, motion, scatterTo;
        float nextPathAt, calledAt, scatterUntil, vertical, idlePhase;
        int corner, cornerCount;

        public bool Saved { get; private set; }
        public bool Caught { get; private set; }
        public bool Following { get; private set; }
        public bool InHands { get; private set; }
        public Vector3 Velocity => motion;

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            path = new NavMeshPath();
            home = transform.position;
            idlePhase = Mathf.Abs(home.x * .61f + home.z * .38f);
        }

        public void Step(Vector3 target, EscapeHen[] flock, bool pathOpen, bool enterExit)
        {
            if (Saved || InHands) return;
            Vector3 steer = Vector3.zero;
            float speed = 0f;
            if (Time.time < scatterUntil)
            {
                steer = FollowPath(scatterTo);
                speed = 4.4f;
                home = transform.position;
            }
            else if (Following)
            {
                float distance = Flat(target - transform.position).magnitude;
                if (pathOpen && distance > (enterExit ? .08f : 1.22f))
                {
                    steer = FollowPath(target);
                    speed = Mathf.Lerp(2.2f, 5.8f, Mathf.InverseLerp(.7f, 4.8f, distance));
                }
                else cornerCount = 0;
            }
            else
            {
                float t = Time.time * .66f + idlePhase;
                Vector3 wandering = home + new Vector3(Mathf.Sin(t) * .48f,0f,Mathf.Cos(t * .93f) * .32f);
                steer = Vector3.ClampMagnitude(Flat(wandering - transform.position), 1f);
                speed = Mathf.Sin(t * .71f + .6f) > -.1f ? 1.2f : 0f;
            }

            foreach (var other in flock)
            {
                if (other == this || other.Saved || other.InHands || other.Caught != Caught) continue;
                Vector3 separation = Flat(transform.position - other.transform.position);
                float distance = separation.magnitude;
                if (distance is > .015f and < .64f)
                    steer += separation / distance * (.64f - distance) * 1.05f;
            }

            if (steer.sqrMagnitude > .02f)
            {
                Vector3 forward = steer.normalized;
                if (Physics.SphereCast(transform.position + Vector3.up * .27f, .14f, forward, out var wall,
                    .48f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Vector3 across = Flat(wall.normal);
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
            float peck = !Following && speed < 1.3f ? Mathf.Max(0f, Mathf.Sin(Time.time * 5.8f + idlePhase)) : 0f;
            float hop = Following ? Mathf.Sin(Mathf.Clamp01((Time.time - calledAt) / .35f) * Mathf.PI) : 0f;
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(peck * peck * 14f - hop * 19f, 0f, 0f),
                1f - Mathf.Exp(-13f*Time.deltaTime));
        }

        Vector3 FollowPath(Vector3 target)
        {
            if (Time.time >= nextPathAt)
            {
                nextPathAt = Time.time + .18f + idlePhase % .1f;
                corner = cornerCount = 0;
                if (NavMesh.SamplePosition(transform.position, out var start, 1.15f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(target, out var end, 1.05f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete)
                    cornerCount = path.GetCornersNonAlloc(corners);
            }
            while (corner < cornerCount && Flat(corners[corner] - transform.position).sqrMagnitude < .16f)
                corner++;
            return corner < cornerCount ? Flat(corners[corner] - transform.position).normalized : Vector3.zero;
        }

        public bool HearBark()
        {
            if (Saved || Caught || Following) return false;
            Following = true;
            scatterUntil = 0f;
            calledAt = Time.time;
            return true;
        }

        public void ScatterFrom(Vector3 keeper)
        {
            if (!Following) return;
            Following = false;
            scatterTo = transform.position + Flat(transform.position - keeper).normalized * 2.5f;
            scatterUntil = Time.time + 1.05f;
            nextPathAt = 0f;
        }

        public void Lift()
        {
            Caught = true;
            Following = false;
            InHands = true;
            motor.enabled = false;
            motion = Vector3.zero;
            vertical = 0f;
        }

        public void CarryAt(Vector3 position, Vector3 facing, float struggle)
        {
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            animator.SetFloat(Speed, .22f + struggle * 1.72f, .06f, Time.deltaTime);
            model.localRotation = Quaternion.Euler(-8f, 0f, Mathf.Sin(Time.time * 23f) * 24f * struggle);
        }

        public void ReleaseInPen(Vector3 point)
        {
            home = point;
            transform.position = point;
            model.localRotation = Quaternion.identity;
            InHands = false;
            motor.enabled = true;
        }

        static Vector3 Flat(Vector3 vector) => Vector3.ProjectOnPlane(vector, Vector3.up);

        public void Rescue(Vector3 exitDirection)
        {
            Saved = true;
            Following = false;
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
