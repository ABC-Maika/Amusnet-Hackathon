using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class ButterflyController : MonoBehaviour
{
    private static readonly int Flapping = Animator.StringToHash("Flapping");
    private static readonly int FlightPose = Animator.StringToHash("FlightPose");
    private static readonly int FlightLegPose = Animator.StringToHash("FlightLegPose");
    private static readonly int FlightFlapRate = Animator.StringToHash("FlightFlapRate");

    private enum WingPose { Idle, Takeoff, Flight, LandingApproach, Touchdown }

    [SerializeField] private ButterflyFlightSettings settings;
    [SerializeField] private Animator wings;
    [SerializeField] private Transform visual;

    private CharacterController motor;
    private Vector3 velocity;
    private float yaw;
    private float pitch;
    private Vector3 visualBase;
    private float bobBlend;
    private float wingSpeed = 1f;
    private float takeoffTime;
    private float touchdownTime;
    private bool wasSupported;
    private bool hasFlightPose;
    private bool hasDirectionalFlight;

    public bool IsBoosting { get; private set; }
    public float Speed => motor != null && motor.enabled ? motor.velocity.magnitude : 0f;

    private void Awake()
    {
        motor = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
        if (visual != null) visualBase = visual.localPosition;
        if (wings != null)
        {
            foreach (AnimatorControllerParameter parameter in wings.parameters)
            {
                if (parameter.nameHash == FlightPose)
                    hasFlightPose = true;
                if (parameter.nameHash == FlightLegPose)
                    hasDirectionalFlight = true;
            }
        }
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

        Simulate(Vector2.ClampMagnitude(steer, 1f), Time.deltaTime, keyboard?.leftShiftKey.isPressed == true);
    }

    public void Simulate(Vector2 steer, float deltaTime, bool boostHeld = false)
    {
        if (deltaTime <= 0f)
            return;

        yaw += steer.x * settings.yawSpeed * deltaTime;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        bool idle = settings.hoverWhenIdle && steer.sqrMagnitude < 0.0001f && !boostHeld;
        bool grounded = motor.isGrounded;
        float targetPitch = Mathf.Lerp(settings.neutralDescentPitch,
            steer.y > 0f ? -settings.maximumPitch : settings.maximumPitch, Mathf.Abs(steer.y));
        if (idle && !grounded)
            targetPitch = settings.hoverPitch; // nose slightly up while fluttering on the spot
        else if (grounded && steer.y <= 0f)
            targetPitch = 0f;

        pitch = Mathf.MoveTowards(pitch, targetPitch, settings.pitchSpeed * deltaTime);
        visual.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (grounded && (steer.y <= 0f || pitch >= 0f))
        {
            // Keep ground contact current, including when a supporting surface moves away.
            bool supported = (motor.Move(Vector3.down * deltaTime) & CollisionFlags.Below) != 0;
            velocity = Vector3.zero;
            IsBoosting = false;
            UpdateWingAnimation(supported, supported && steer.y > 0.05f, deltaTime);
            return;
        }

        // No input: stop and flutter on the spot (wings keep flapping, a tiny bob keeps it alive).
        wingSpeed = Mathf.MoveTowards(wingSpeed, idle ? settings.hoverWingSpeed : 1f, 1.5f * deltaTime);
        if (wings != null) wings.speed = wingSpeed;
        bobBlend = Mathf.MoveTowards(bobBlend, idle ? 1f : 0f, 3f * deltaTime);
        visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * settings.hoverBobSpeed) * settings.hoverBobHeight * bobBlend);
        if (idle)
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, settings.hoverBrake * deltaTime);
            motor.Move(velocity * deltaTime);
            velocity = motor.velocity;
            IsBoosting = false;
            UpdateWingAnimation(false, false, deltaTime);
            return;
        }

        bool boost = boostHeld && settings.boostSpeed > settings.flightSpeed;
        float speed = boost ? settings.boostSpeed : settings.flightSpeed;
        Vector3 targetVelocity = visual.forward * speed;
        velocity = Vector3.MoveTowards(velocity, targetVelocity, settings.acceleration * deltaTime);
        CollisionFlags contacts = motor.Move(velocity * deltaTime);
        bool landed = (contacts & CollisionFlags.Below) != 0;
        velocity = landed ? Vector3.zero : motor.velocity;
        IsBoosting = boost && !landed && Vector3.Dot(velocity, visual.forward) > 0.1f;
        UpdateWingAnimation(landed, grounded && steer.y > 0.05f, deltaTime);
    }

    private void OnDisable()
    {
        IsBoosting = false;
    }

    private void UpdateWingAnimation(bool supported, bool liftingOff, float deltaTime)
    {
        if (wings == null)
            return;

        wings.SetBool(Flapping, !supported);
        if (!hasFlightPose)
            return;

        takeoffTime = liftingOff ? 0.34f : Mathf.Max(0f, takeoffTime - deltaTime);
        touchdownTime = supported && !wasSupported && !liftingOff
            ? 0.34f
            : Mathf.Max(0f, touchdownTime - deltaTime);
        wasSupported = supported;

        WingPose pose;
        if (liftingOff || (!supported && takeoffTime > 0f))
            pose = WingPose.Takeoff;
        else if (supported)
            pose = touchdownTime > 0f ? WingPose.Touchdown : WingPose.Idle;
        else
            pose = IsNearLandingSurface() ? WingPose.LandingApproach : WingPose.Flight;

        wings.SetInteger(FlightPose, (int)pose);
        if (hasDirectionalFlight)
        {
            float reach = Mathf.InverseLerp(-0.8f, 1.15f, velocity.y);
            wings.SetFloat(FlightLegPose, reach, 0.13f, deltaTime);
            wings.SetFloat(FlightFlapRate, Mathf.Lerp(0.85f, 1.23f, reach), 0.13f, deltaTime);
        }
    }

    private bool IsNearLandingSurface()
    {
        if (velocity.y >= -0.12f)
            return false;

        Bounds bounds = motor.bounds;
        // Reach for the ground sooner during a fast descent.
        float anticipation = Mathf.Clamp(-velocity.y * 0.35f, 0.33f, 0.8f);
        Vector3 ahead = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up) * 0.12f, 0.3f);
        return Physics.Raycast(bounds.center + ahead, Vector3.down, out RaycastHit surface,
            bounds.extents.y + anticipation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            Vector3.Angle(surface.normal, Vector3.up) <= motor.slopeLimit;
    }

    private static float ToAxis(bool positive, bool negative)
    {
        return (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
