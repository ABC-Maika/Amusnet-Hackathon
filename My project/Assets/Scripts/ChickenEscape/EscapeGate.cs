using UnityEngine;
using UnityEngine.AI;

namespace Amusnet.ChickenEscape
{
    public sealed class EscapeGate : MonoBehaviour
    {
        [SerializeField] Transform leaf, latch, access;
        [SerializeField] Collider blocker;
        [SerializeField] NavMeshObstacle obstacle;

        public bool IsOpen { get; private set; }
        public float OpenedAt { get; private set; }
        public Vector3 Latch => latch.position;
        public Vector3 Access => access.position;
        public Vector3 Approach => transform.position - transform.forward * .82f;

        public bool CanUnlatch(Vector3 position) =>
            !IsOpen && transform.InverseTransformPoint(position).z > .23f &&
            (Latch - position).sqrMagnitude < 1.92f * 1.92f;

        public bool InPassage(Vector3 point, float clearance)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) < 1.46f + clearance && Mathf.Abs(local.z) < .25f + clearance;
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (open) OpenedAt = Time.time;
            blocker.enabled = !open;
            obstacle.enabled = !open;
        }

        void Update()
        {
            leaf.localRotation = Quaternion.RotateTowards(leaf.localRotation,
                Quaternion.Euler(0, IsOpen ? -108f : 0f, 0), 485f * Time.deltaTime);
        }
    }
}
