using UnityEngine;
using UnityEngine.InputSystem;

namespace Butterfly.Death
{
    /// <summary>Lets the player steer the falling butterfly for a short window (WASD / arrows / left stick).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MartyrSteering : MonoBehaviour
    {
        public float steerSpeed = 8f;
        public float steerResponse = 4f;

        Rigidbody rb;
        Vector2 input;

        void Awake() => rb = GetComponent<Rigidbody>();

        void Update()
        {
            input = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 s = pad.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.04f) input = s;
            }
            input = Vector2.ClampMagnitude(input, 1f);
        }

        void FixedUpdate()
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
            Vector3 target = (right * input.x + fwd * input.y) * steerSpeed;

            Vector3 v = rb.linearVelocity;
            // fixedDeltaTime shrinks with timeScale, so scale the response to keep steering responsive in slow-mo
            float k = 1f - Mathf.Exp(-steerResponse * Time.fixedDeltaTime / Mathf.Max(Time.timeScale, 0.05f));
            Vector3 horiz = Vector3.Lerp(new Vector3(v.x, 0f, v.z), target, k);
            rb.linearVelocity = new Vector3(horiz.x, v.y, horiz.z);
        }
    }
}
