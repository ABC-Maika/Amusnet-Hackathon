using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class ButterflyController : MonoBehaviour
{
    private static readonly int Flapping = Animator.StringToHash("Flapping");

    [SerializeField] private ButterflyFlightSettings settings;
    [SerializeField] private Animator wings;
    [SerializeField] private Transform visual;

    private CharacterController motor;
    private Vector3 velocity;
    private float yaw;
    private float pitch;

    public float Speed => motor != null && motor.enabled ? motor.velocity.magnitude : 0f;

    private void Awake()
    {
        motor = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
        if (settings == null || visual == null)
        {
            Debug.LogError("Assign flight settings and the flight attitude transform to the butterfly.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        Vector2 steer = Vector2.zero;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            steer.x = ToAxis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            steer.y = ToAxis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
        }

        if (Gamepad.current != null)
            steer += Gamepad.current.leftStick.ReadValue();

        Simulate(Vector2.ClampMagnitude(steer, 1f), Time.deltaTime);
    }

    public void Simulate(Vector2 steer, float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        yaw += steer.x * settings.yawSpeed * deltaTime;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        bool grounded = motor.isGrounded;
        float targetPitch = Mathf.Lerp(settings.neutralDescentPitch,
            steer.y > 0f ? -settings.maximumPitch : settings.maximumPitch, Mathf.Abs(steer.y));
        if (grounded && steer.y <= 0f)
            targetPitch = 0f;

        pitch = Mathf.MoveTowards(pitch, targetPitch, settings.pitchSpeed * deltaTime);
        visual.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (grounded && (steer.y <= 0f || pitch >= 0f))
        {
            // Keep ground contact current, including when a supporting surface moves away.
            SetWingAnimation((motor.Move(Vector3.down * deltaTime) & CollisionFlags.Below) == 0);
            velocity = Vector3.zero;
            return;
        }

        Vector3 targetVelocity = visual.forward * settings.flightSpeed;
        velocity = Vector3.MoveTowards(velocity, targetVelocity, settings.acceleration * deltaTime);
        CollisionFlags contacts = motor.Move(velocity * deltaTime);
        bool landed = (contacts & CollisionFlags.Below) != 0;
        velocity = landed ? Vector3.zero : motor.velocity;
        SetWingAnimation(!landed);
    }

    private void SetWingAnimation(bool airborne)
    {
        if (wings != null)
            wings.SetBool(Flapping, airborne);
    }

    private static float ToAxis(bool positive, bool negative)
    {
        return (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
