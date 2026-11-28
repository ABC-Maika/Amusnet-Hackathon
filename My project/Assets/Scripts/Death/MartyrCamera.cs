using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>Simple follow camera used during the fall. Added to the camera by DeathManager and removed after.</summary>
    public class MartyrCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 2.5f, -5f);
        public float followSharpness = 6f;

        void LateUpdate()
        {
            if (target == null) return;
            // unscaled so the camera stays smooth during slow-mo
            float t = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target.position + offset, t);
            Vector3 dir = target.position - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), t);
        }
    }
}
