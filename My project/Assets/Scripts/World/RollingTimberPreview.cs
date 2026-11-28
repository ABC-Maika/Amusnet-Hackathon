using UnityEngine;

namespace Amusnet.World
{
    public sealed class RollingTimberPreview : MonoBehaviour
    {
        [SerializeField] private Transform backUpPoint;
        [SerializeField] private BoxCollider impactZone;
        [SerializeField] private Rigidbody[] logs;

        public Transform BackUpPoint => backUpPoint;
        public bool Released { get; private set; }

        public bool TryRelease(Vector3 contact)
        {
            if (Released || !impactZone.bounds.Contains(contact))
                return false;

            Released = true;
            foreach (Rigidbody log in logs)
            {
                log.isKinematic = false;
                log.AddForce(-transform.right * 1.3f, ForceMode.VelocityChange);
            }
            return true;
        }

        private void OnGUI()
        {
            if (Released)
                GUI.Box(new Rect(16, 94, Mathf.Min(460, Screen.width - 32), 28), "The goat knocked the timber loose!");
        }
    }
}
