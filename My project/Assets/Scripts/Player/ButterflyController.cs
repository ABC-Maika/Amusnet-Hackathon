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
    private Terrain groundTerrain;
    internal Vector3? LastTerrainPosition { get; private set; }
    private Vector3 velocity;
    private float yaw;
    private float pitch;       // visual nose pitch (degrees, positive = nose down)
    private float flightPitch; // where the mouse points the flight (degrees, positive = climb)
    private float speed;       // forward speed along the flight direction; it only ever builds up or bleeds off
    private float roll;
    private float throttle;
    private ButterflyBoostFx fx;
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

    /// <summary>Current flight pitch in degrees (positive = climbing). Driven by the mouse.</summary>
    public float FlightPitch => flightPitch;
    /// <summary>Forward speed as a fraction of normal flight speed (above 1 while boosting).</summary>
    public float Speed01 => settings != null ? speed / settings.flightSpeed : 0f;
    public float Throttle => throttle;

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
        fx = GetComponent<ButterflyBoostFx>();
        if (fx == null) fx = gameObject.AddComponent<ButterflyBoostFx>();
        fx.Init(this, visual);
        if (settings == null || visual == null)
        {
            Debug.LogError("Assign flight settings and the flight attitude transform to the butterfly.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // W speeds up, S brakes (never reverses), A/D turn, the mouse pitches the flight up/down, Shift boosts.
        Vector2 steer = Vector2.zero;
        float vertical = 0f;
        float mousePitch = 0f;
        bool boost = false;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            steer.x = ToAxis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            steer.y = ToAxis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            boost = keyboard.leftShiftKey.isPressed;
        }

        // Mouse up = nose up. Only while the camera owns the cursor (not in menus / pause).
        var mouse = Mouse.current;
        if (mouse != null && Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !Butterfly.UI.PauseMenu.IsPaused)
            mousePitch = mouse.delta.ReadValue().y * settings.mouseSensitivity;

        var pad = Gamepad.current;
        if (pad != null)
        {
            steer += pad.leftStick.ReadValue();
            vertical += pad.rightStick.ReadValue().y;
            boost |= pad.leftStickButton.isPressed;
        }

        Simulate(Vector2.ClampMagnitude(steer, 1f), Mathf.Clamp(vertical, -1f, 1f), Time.deltaTime, boost, mousePitch);
    }

    public void Simulate(Vector2 steer, float deltaTime, bool boostHeld = false) =>
        Simulate(steer, 0f, deltaTime, boostHeld);

    /// <param name="steer">x turns left/right, y is the throttle (only forward; negative brakes).</param>
    /// <param name="vertical">Pitch rate input, +1 pitches up (gamepad). The mouse feeds <paramref name="pitchDelta"/>.</param>
    /// <param name="pitchDelta">Degrees to add to the flight pitch this frame (mouse up = positive).</param>
    public void Simulate(Vector2 steer, float vertical, float deltaTime, bool boostHeld = false, float pitchDelta = 0f)
    {
        if (deltaTime <= 0f || !motor.enabled)
            return;

        KeepAboveTerrain();

        float yawRate = steer.x * settings.yawSpeed;
        yaw += yawRate * deltaTime;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        float maxPitch = settings.maximumPitch;
        flightPitch = Mathf.Clamp(flightPitch + pitchDelta + vertical * settings.pitchSpeed * deltaTime, -maxPitch, maxPitch);

        bool grounded = motor.isGrounded;
        float world = Time.time < spotBoostUntil ? spotBoost : 1f; // boost flowers and trees

        // Throttle: W builds speed, S brakes (no reverse), letting go coasts to a stop.
        throttle = Mathf.MoveTowards(throttle, Mathf.Max(0f, steer.y), settings.throttleResponse * deltaTime);
        bool boost = boostHeld && steer.y > 0.01f && settings.boostSpeed > settings.flightSpeed;
        float cap = (boost ? settings.boostSpeed : settings.flightSpeed) * world;
        if (steer.y > 0.01f)
        {
            float accel = (boost ? settings.boostAcceleration : settings.acceleration) * world * steer.y;
            speed = speed < cap
                ? Mathf.MoveTowards(speed, cap, accel * deltaTime)
                : Mathf.MoveTowards(speed, cap, settings.brakeDeceleration * 0.4f * deltaTime); // ease down after a boost
        }
        else if (steer.y < -0.01f)
            speed = Mathf.MoveTowards(speed, 0f, settings.brakeDeceleration * -steer.y * deltaTime);
        else
            speed = Mathf.MoveTowards(speed, 0f, settings.coastDeceleration * deltaTime);

        bool hovering = settings.hoverWhenIdle && speed < 0.25f && steer.y <= 0.01f && !boostHeld;

        if (grounded && flightPitch > 6f)
            speed = Mathf.Max(speed, settings.climbSpeed); // mouse up on the ground = take off

        if (grounded && flightPitch <= 6f)
        {
            // Standing: walk forward slowly. No walking backwards. Mouse up takes off.
            Vector3 walk = transform.forward * (Mathf.Max(0f, steer.y) * settings.groundWalkSpeed);
            bool supported = (motor.Move((walk + Vector3.down) * deltaTime) & CollisionFlags.Below) != 0;
            velocity = Vector3.zero;
            speed = 0f;
            IsBoosting = false;
            AnimateBody(0f, false, deltaTime, yawRate, true);
            UpdateWingAnimation(supported, false, deltaTime);
            KeepAboveTerrain();
            return;
        }

        // Fly along the pitched heading; the velocity lags a little behind it so turns carve instead of snap.
        Vector3 heading = Quaternion.Euler(-flightPitch, yaw, 0f) * Vector3.forward;
        Vector3 target = heading * speed;
        velocity = Vector3.Lerp(velocity, target, 1f - Mathf.Exp(-settings.turnGrip * deltaTime));
        CollisionFlags contacts = motor.Move(velocity * deltaTime);
        bool landed = (contacts & CollisionFlags.Below) != 0 && heading.y <= 0.02f;
        if (landed) speed = 0f;
        velocity = landed ? Vector3.zero : motor.velocity;
        IsBoosting = boost && !landed && speed > settings.flightSpeed * 0.95f;

        AnimateBody(speed, hovering, deltaTime, yawRate, false);
        UpdateWingAnimation(landed, false, deltaTime);
        KeepAboveTerrain();
    }

    private void KeepAboveTerrain()
    {
        if (!groundTerrain || !groundTerrain.isActiveAndEnabled)
            groundTerrain = Terrain.activeTerrain;
        if (!groundTerrain)
            return;

        Vector3 position = transform.position;
        float footOffset = motor.bounds.min.y - position.y;
        if (TryGetTerrainFloor(groundTerrain, position, out float floor))
        {
            if (position.y + footOffset >= floor - .08f)
            {
                LastTerrainPosition = position;
                return;
            }
        }
        else
        {
            if (position.y >= groundTerrain.transform.position.y - 1f || !LastTerrainPosition.HasValue)
                return;
            position = LastTerrainPosition.Value;
            if (!TryGetTerrainFloor(groundTerrain, position, out floor))
                return;
        }

        position.y = floor - footOffset + .025f;
        motor.enabled = false;
        transform.position = position;
        motor.enabled = true;
        LastTerrainPosition = position;
        velocity = Vector3.zero;
        speed = 0f;
        IsBoosting = false;
    }

    internal static bool TryGetTerrainFloor(Terrain terrain, Vector3 position, out float height)
    {
        height = 0f;
        if (!terrain || !terrain.isActiveAndEnabled || !terrain.terrainData)
            return false;

        Vector3 origin = terrain.transform.position;
        Vector3 offset = position - origin;
        Vector3 size = terrain.terrainData.size;
        if (offset.x < 0f || offset.z < 0f || offset.x >= size.x || offset.z >= size.z)
            return false;

        height = origin.y + terrain.SampleHeight(position);
        return true;
    }

    // Body motion: nose follows the climb, banks into turns, wings beat faster with speed, flutter bob.
    private void AnimateBody(float fwdSpeed, bool hovering, float deltaTime, float yawRate, bool supportedOnGround)
    {
        float speedNorm = Mathf.Clamp01(fwdSpeed / settings.flightSpeed);
        float targetPitch = supportedOnGround ? 0f : hovering
            ? settings.hoverPitch
            : -flightPitch * 0.85f + speedNorm * settings.forwardLean;
        pitch = Mathf.MoveTowards(pitch, targetPitch, settings.pitchSpeed * deltaTime);

        float turn = settings.yawSpeed > 0f ? Mathf.Clamp(yawRate / settings.yawSpeed, -1f, 1f) : 0f;
        float targetRoll = supportedOnGround || hovering ? 0f : -turn * settings.bankAngle * Mathf.Lerp(0.5f, 1.2f, speedNorm);
        roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-settings.bankResponse * deltaTime));
        visual.localRotation = Quaternion.Euler(pitch, 0f, roll);

        if (supportedOnGround)
        {
            visual.localPosition = visualBase;
            return;
        }

        float targetWing = hovering ? settings.hoverWingSpeed
            : Mathf.Lerp(0.95f, 1.35f, speedNorm) * (IsBoosting ? 1.2f : 1f);
        wingSpeed = Mathf.MoveTowards(wingSpeed, targetWing, 2f * deltaTime);
        if (wings != null) wings.speed = wingSpeed;

        bobBlend = Mathf.MoveTowards(bobBlend, 1f, 3f * deltaTime);
        float flutter = hovering ? settings.hoverBobHeight : settings.flutterHeight * (0.4f + speedNorm);
        float rate = hovering ? settings.hoverBobSpeed : settings.flutterSpeed;
        visual.localPosition = visualBase + new Vector3(
            Mathf.Sin(Time.time * rate * 0.6f) * flutter * 0.5f * bobBlend,
            Mathf.Sin(Time.time * rate) * flutter * bobBlend, 0f);
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
