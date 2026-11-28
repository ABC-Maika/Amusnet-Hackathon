using UnityEngine;
using UnityEngine.InputSystem;

namespace Butterfly.Death
{
    // Temporary stand-ins used by the death test scene (see Editor/DeathSceneBuilder.cs).
    // Delete once the real flight controller, camera, village and messables exist.

    /// <summary>Stand-in flight: A/D turn, W/S move, Space up, Shift down. Disabled by DeathManager on death.</summary>
    public class SimFlyer : MonoBehaviour
    {
        public float speed = 9f;
        public float climb = 5f;
        public float turnSpeed = 90f;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            float turn = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float fwd = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            float up = (kb.spaceKey.isPressed ? 1f : 0f) - (kb.leftShiftKey.isPressed ? 1f : 0f);

            transform.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f);
            transform.position += (transform.forward * fwd * speed + Vector3.up * up * climb) * Time.deltaTime;
        }
    }

    /// <summary>Plain chase camera for flying. Steps aside while the death sequence runs.</summary>
    public class SimChaseCamera : MonoBehaviour
    {
        public Transform target;

        void LateUpdate()
        {
            if (target == null || (DeathManager.Instance != null && DeathManager.Instance.IsDying)) return;
            Vector3 want = target.position - target.forward * 7f + Vector3.up * 3f;
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-6f * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        }
    }

    /// <summary>Simulated messable object so the chain reaction has something to hit.</summary>
    public class SimMessProp : MonoBehaviour, IChainReactionTarget
    {
        public float messValue = 1f;

        public float TriggerChain(Vector3 origin, float power)
        {
            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = Color.red;
            return messValue;
        }
    }

    /// <summary>
    /// Obsolete: the old IMGUI controls/legacy text. Controls now live in the pause menu (PauseMenu)
    /// and legacy in DeathHud. Kept as an empty class so scenes that still reference it load cleanly;
    /// Death > Add Anger and Predator UI removes it from the open scene.
    /// </summary>
    public class DeathTestHud : MonoBehaviour { }
}
