using UnityEngine;
using UnityEngine.AI;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EscapeKeeper : MonoBehaviour
    {
        [SerializeField] Animator animator;

        NavMeshAgent agent;
        Vector3 attention;
        float distractedUntil, nextPath, pursuesAt;
        int state;

        public bool IsDistracted => Time.time < distractedUntil;
        public bool IsClosing { get; private set; }

        void Awake() => agent = GetComponent<NavMeshAgent>();

        public void StartChase() => pursuesAt = Time.time + 3.4f;

        public void Distract(Vector3 position)
        {
            if ((transform.position-position).sqrMagnitude > 12f * 12f) return;
            attention = position;
            distractedUntil = Time.time + 3.25f;
            nextPath = 0f;
        }

        public void Step(EscapeDog dog, EscapeGate gate, bool chase, bool hensInPen, bool blocked,
            bool steeringDog)
        {
            IsClosing = false;
            if (!chase || !agent.isOnNavMesh || Time.time < pursuesAt)
            {
                Animate("Watch");
                return;
            }
            IsClosing = gate.IsOpen && hensInPen && !IsDistracted;
            Vector3 target = IsDistracted ? attention : IsClosing ? gate.Approach : dog.transform.position;
            agent.speed = IsDistracted ? 3.3f : IsClosing ? 3.05f : 4.15f;
            if (Time.time >= nextPath)
            {
                if (NavMesh.SamplePosition(target, out var location, 1.6f, agent.areaMask))
                    agent.SetDestination(location.position);
                nextPath = Time.time + .22f;
            }

            if (IsClosing && !blocked && Time.time - gate.OpenedAt > 8.5f &&
                (transform.position - gate.Approach).sqrMagnitude < .60f)
            {
                gate.SetOpen(false);
                Animate("Protest");
            }
            else if (!IsClosing && !IsDistracted && steeringDog && !dog.IsStopped &&
                     (transform.position-dog.transform.position).sqrMagnitude < .90f)
            {
                dog.Hesitate();
                distractedUntil = Time.time + .9f;
                attention = transform.position;
                Animate("Protest");
            }
            else Animate(agent.velocity.sqrMagnitude > .09f ? "Walk" : "Watch");
        }

        void Animate(string pose)
        {
            int next = Animator.StringToHash("Base Layer." + pose);
            if (next != state)
            {
                animator.CrossFadeInFixedTime(next, .13f);
                state = next;
            }
            animator.speed = pose == "Walk" ? Mathf.Clamp(agent.velocity.magnitude/2.6f, .4f, 1.5f) : 1f;
        }
    }
}
