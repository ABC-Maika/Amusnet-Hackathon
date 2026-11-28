using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Death cutscene camera, added to the main camera by DeathManager (which switches the CinemachineBrain off).
    /// It orbits behind and above whatever it follows (the falling butterfly, the predator, the poop),
    /// keeping its current side so cuts between targets glide instead of jumping.
    /// </summary>
    public class DeathCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 4f;
        public float height = 1.5f;
        public float sharpness = 3f;
        Vector3 side;

        /// <param name="fromSide">Optional flat direction to view from (e.g. away from a predator); default keeps the camera's side.</param>
        public void Follow(Transform newTarget, float newDistance, float newHeight, Vector3 fromSide = default)
        {
            target = newTarget;
            distance = newDistance;
            height = newHeight;
            side = new Vector3(fromSide.x, 0f, fromSide.z);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime; // stops while the game is paused
            Vector3 around = side.sqrMagnitude > 0.0001f ? side : transform.position - target.position;
            around.y = 0f;
            if (around.sqrMagnitude < 0.01f) around = -target.forward;
            Vector3 desired = target.position + around.normalized * distance + Vector3.up * height;
            // Keep a clear view: if a wall, roof or tree is between the target and the camera, move in front of it.
            Vector3 toCamera = desired - target.position;
            if (Physics.SphereCast(target.position, 0.2f, toCamera.normalized, out var hit, toCamera.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                desired = target.position + toCamera.normalized * Mathf.Max(0.4f, hit.distance - 0.1f);
            float k = 1f - Mathf.Exp(-sharpness * dt);
            transform.position = Vector3.Lerp(transform.position, desired, k);
            Vector3 look = target.position - transform.position;
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, Vector3.up), k * 1.5f);
        }
    }
}
