using Amusnet.Mess;
using UnityEngine;
using UnityEngine.AI;

namespace Amusnet.Animals
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class FarmAnimal : MonoBehaviour
    {
        [SerializeField] private MessManager mess;
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform tail;
        [SerializeField] private Transform[] legs;
        [SerializeField] private Vector3 strollSpot;
        [SerializeField] private float walkSpeed = 1.2f;
        [SerializeField] private float panicSpeed = 3.8f;

        public bool IsAlarmed { get; private set; }
        private NavMeshAgent agent;
        private Vector3 home;
        private Vector3 bodyRest;
        private Quaternion headRest;
        private Quaternion tailRest;
        private float nextDeparture;
        private float gait;
        private bool returning;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            home = transform.position;
            bodyRest = body.localPosition;
            headRest = head.localRotation;
            tailRest = tail.localRotation;
            nextDeparture = Time.time + 2f + transform.position.x % 1.5f;
        }

        private void Start()
        {
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(home, out var nearest, 2f, agent.areaMask))
                agent.Warp(nearest.position);
        }

        private void OnEnable()
        {
            if (mess) mess.MessCreated += React;
        }

        private void OnDisable()
        {
            if (mess) mess.MessCreated -= React;
        }

        private void React(Interactable source, int amount)
        {
            if (!agent.isOnNavMesh || IsAlarmed) return;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - source.transform.position, Vector3.up);
            if (away.sqrMagnitude > 42f || away.sqrMagnitude < .01f) return;
            if (!NavMesh.SamplePosition(transform.position + away.normalized * 4f, out var escape, 2f, agent.areaMask))
                return;
            IsAlarmed = agent.SetDestination(escape.position);
            if (IsAlarmed)
            {
                agent.speed = panicSpeed;
                nextDeparture = Time.time + 3.4f;
            }
        }

        private void Update()
        {
            if (!agent.isOnNavMesh) return;
            if (Time.time >= nextDeparture)
            {
                IsAlarmed = false;
                agent.speed = walkSpeed;
                returning = !returning;
                Vector3 destination = returning ? strollSpot : home;
                if (NavMesh.SamplePosition(destination, out var hit, 1.4f, agent.areaMask))
                    agent.SetDestination(hit.position);
                nextDeparture = Time.time + 5f;
            }

            float speed = agent.velocity.magnitude;
            gait += Time.deltaTime * (4f + speed * 5f);
            float stride = Mathf.Sin(gait);
            float walking = Mathf.Clamp01(speed / Mathf.Max(.1f, walkSpeed));
            float swing = walking * (IsAlarmed ? 36f : 24f);
            for (int i = 0; i < legs.Length; i++)
                legs[i].localRotation = Quaternion.Euler(stride * swing * (i is 0 or 3 ? 1f : -1f), 0f, 0f);
            body.localPosition = bodyRest + Vector3.up * (.015f + Mathf.Abs(stride) * walking * .065f);
            head.localRotation = headRest * Quaternion.Euler(IsAlarmed ? -12f : Mathf.Sin(Time.time * 2f) * 5f, 0f, 0f);
            tail.localRotation = tailRest * Quaternion.Euler(0f, Mathf.Sin(Time.time * (IsAlarmed ? 12f : 5f)) * 19f, 0f);
        }
    }
}
