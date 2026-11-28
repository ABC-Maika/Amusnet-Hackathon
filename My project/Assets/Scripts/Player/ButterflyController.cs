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
    private ButterflyLegs legs;
    private float takeoffTime;
    private float touchdownTime;
    private bool wasSupported;
    private bool hasFlightPose;
    private bool hasDirectionalFlight;

    private float spotBoost = 1f;
    private float spotBoostUntil;

    public bool IsBoosting { get; private set; }

    /// <summary>Seconds left on a speed boost from the world (e.g. a boost flower); 0 when none.</summary>
    public float SpotBoostSecondsLeft => Mathf.Max(0f, spotBoostUntil - Time.time);

    /// <summary>Multiplies flight speed by <paramref name="factor"/> for <paramref name="seconds"/>.</summary>
    public void ApplySpeedBoost(float factor, float seconds)
    {
        spotBoost = Mathf.Max(1f, factor);
        spotBoostUntil = Time.time + Mathf.Max(0f, seconds);
    }
    public float Speed => motor != null && motor.enabled ? motor.velocity.magnitude : 0f;

    private void Awake()
    {
        motor = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
        if (visual != null) visualBase = visual.localPosition;
        if (wings != null)
        {
            legs = wings.GetComponent<ButterflyLegs>();
            if (legs == null) legs = wings.gameObject.AddComponent<ButterflyLegs>();
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
        // W/S fly forward and back, A/D turn, Space rises, Ctrl descends, Shift boosts.
        Vector2 steer = Vector2.zero;
        float vertical = 0f;
        bool boost = false;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            steer.x = ToAxis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            steer.y = ToAxis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            vertical = ToAxis(keyboard.spaceKey.isPressed,
                keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed);
            boost = keyboard.leftShiftKey.isPressed;
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            steer += pad.leftStick.ReadValue();
            vertical += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            boost |= pad.leftStickButton.isPressed;
        }

        Simulate(Vector2.ClampMagnitude(steer, 1f), Mathf.Clamp(vertical, -1f, 1f), Time.deltaTime, boost);
    }

    public void Simulate(Vector2 steer, float deltaTime, bool boostHeld = false) =>
        Simulate(steer, 0f, deltaTime, boostHeld);

    /// <param name="steer">x turns left/right, y flies forward/back.</param>
    /// <param name="vertical">+1 rises, -1 descends.</param>
    public void Simulate(Vector2 steer, float vertical, float deltaTime, bool boostHeld = false)
    {
        if (deltaTime <= 0f)
            return;

        yaw += steer.x * settings.yawSpeed * deltaTime;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        bool moving = Mathf.Abs(steer.y) > 0.01f || Mathf.Abs(vertical) > 0.01f;
        bool hovering = settings.hoverWhenIdle && !moving && !boostHeld;
        bool grounded = motor.isGrounded;

        // Nose follows the climb: up when rising, down when descending, a small lean when flying forward.
        float targetPitch = hovering
            ? settings.hoverPitch
            : -vertical * settings.maximumPitch * 0.7f + Mathf.Max(0f, steer.y) * settings.forwardLean;
        if (grounded && vertical <= 0f)
            targetPitch = 0f;
        pitch = Mathf.MoveTowards(pitch, targetPitch, settings.pitchSpeed * deltaTime);
        visual.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (grounded && vertical <= 0.01f)
        {
            // Standing: a slow walk on the ground. Space takes off.
            Vector3 walk = transform.forward * (steer.y * settings.groundWalkSpeed);
            bool supported = (motor.Move((walk + Vector3.down) * deltaTime) & CollisionFlags.Below) != 0;
            velocity = Vector3.zero;
            IsBoosting = false;
            UpdateWingAnimation(supported, false, deltaTime);
            return;
        }

        // Hovering: flutter on the spot with slower wings and a tiny bob.
        wingSpeed = Mathf.MoveTowards(wingSpeed, hovering ? settings.hoverWingSpeed : 1f, 1.5f * deltaTime);
        if (wings != null) wings.speed = wingSpeed;
        bobBlend = Mathf.MoveTowards(bobBlend, hovering ? 1f : 0f, 3f * deltaTime);
        visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * settings.hoverBobSpeed) * settings.hoverBobHeight * bobBlend);

        bool boost = boostHeld && steer.y > 0.01f && settings.boostSpeed > settings.flightSpeed;
        float forwardSpeed = boost ? settings.boostSpeed : settings.flightSpeed;
        if (steer.y < 0f)
            forwardSpeed *= settings.backwardFactor;
        float climb = vertical > 0f ? settings.climbSpeed : settings.descendSpeed;
        float world = Time.time < spotBoostUntil ? spotBoost : 1f; // boost flowers and trees
        Vector3 targetVelocity = transform.forward * (steer.y * forwardSpeed * world) + Vector3.up * (vertical * climb);

        float rate = moving || boostHeld ? settings.acceleration : settings.hoverBrake;
        velocity = Vector3.MoveTowards(velocity, targetVelocity, rate * world * deltaTime);
        CollisionFlags contacts = motor.Move(velocity * deltaTime);
        bool landed = (contacts & CollisionFlags.Below) != 0 && vertical <= 0.01f;
        velocity = landed ? Vector3.zero : motor.velocity;
        IsBoosting = boost && !landed && Vector3.Dot(velocity, transform.forward) > 0.1f;
        UpdateWingAnimation(landed, grounded && vertical > 0.05f, deltaTime);
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
        if (legs != null) legs.SetSupported(supported);
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
