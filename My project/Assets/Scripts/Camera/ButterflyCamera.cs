using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CinemachineCamera), typeof(CinemachineFollow))]
public sealed class ButterflyCamera : CinemachineExtension
{
    [SerializeField] private CinemachineImpulseSource impulse;
    [SerializeField, Range(5f, 65f)] private float downwardTilt = 20.6f;
    [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.18f;

    [SerializeField, Range(0f, 15f)] private float maximumTurnLean = 7f;
    [SerializeField, Min(0f)] private float boostPullback = 1.15f;
    [SerializeField, Min(0.1f)] private float motionResponse = 5f;
    [SerializeField, Range(0.3f, 10f)] private float turnCatchUp = 1.6f; // lower = camera swings slower after A/D
    [SerializeField, Range(0f, 0.8f)] private float pitchFollow = 0.4f;   // camera tilts up/down with the climb
    [SerializeField, Range(0f, 30f)] private float boostFovKick = 14f;
    [SerializeField, Range(0f, 20f)] private float speedFovKick = 5f;
    private float fovKick;

    private CinemachineFollow follow;
    private Transform trackedTarget;
    private ButterflyController player;
    private float previousYaw;
    private float cameraLean;
    private float currentPullback;
    private Vector3 restOffset;
    private float orbitYaw;
    private float turnLag; // camera trails behind A/D turns, then catches up
    private float orbitTiltOffset;
    private bool capturedCursor;
    private bool inputEnabled = true;

    /// <summary>
    /// When false the camera ignores the mouse and frees the cursor (e.g. on menus and pause);
    /// setting it back to true captures the cursor again. Set by the game's GameplayGate.
    /// </summary>
    public bool InputEnabled
    {
        get => inputEnabled;
        set
        {
            if (inputEnabled == value) return;
            inputEnabled = value;
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (value) CaptureCursor();
            else ReleaseCursor();
        }
    }

    /// <summary>Multiplier on mouse sensitivity (the player's setting).</summary>
    public float SensitivityScale { get; set; } = 1f;

    protected override void Awake()
    {
        base.Awake();
        follow = GetComponent<CinemachineFollow>();
        restOffset = follow.FollowOffset;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (Application.isPlaying && inputEnabled)
            CaptureCursor();
    }

    private void OnDisable()
    {
        if (follow != null)
            follow.FollowOffset = restOffset;
        ReleaseCursor();
    }

    private void Update()
    {
        if (!Application.isPlaying || !Application.isFocused || !inputEnabled)
            return;

        // The pause menu owns the cursor while open (clicks there must not recapture it).
        if (Butterfly.UI.PauseMenu.IsPaused)
            return;

        var mouse = Mouse.current;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            ReleaseCursor();
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (mouse?.leftButton.wasPressedThisFrame == true)
                CaptureCursor();
            return;
        }

        if (mouse == null)
            return;

        ApplyMouseDelta(mouse.delta.ReadValue());
    }

    private void ApplyMouseDelta(Vector2 pixels)
    {
        // Mouse X orbits the camera. Mouse Y pitches the butterfly's flight (ButterflyController), and the camera follows that.
        orbitYaw = Mathf.Repeat(orbitYaw + pixels.x * (mouseSensitivity * SensitivityScale), 360f);
    }

    public override void PrePipelineMutateCameraStateCallback(
        CinemachineVirtualCameraBase vcam, ref CameraState state, float deltaTime)
    {
        if (!Application.isPlaying || follow == null || vcam.Follow == null)
            return;

        Transform target = vcam.Follow;
        float yaw = target.eulerAngles.y;
        bool targetChanged = trackedTarget != target;
        if (targetChanged)
        {
            trackedTarget = target;
            player = target.GetComponent<ButterflyController>();
        }

        float pullback = player != null && player.IsBoosting ? boostPullback : 0f;
        if (targetChanged || deltaTime < 0f)
        {
            cameraLean = 0f;
            currentPullback = pullback;
        }
        else if (deltaTime > 0f)
        {
            float dYaw = Mathf.DeltaAngle(previousYaw, yaw);
            turnLag = Mathf.Clamp(turnLag - dYaw, -40f, 40f);
            turnLag = Mathf.Lerp(turnLag, 0f, 1f - Mathf.Exp(-turnCatchUp * deltaTime));
            float turnRate = dYaw / deltaTime;
            float desiredLean = Mathf.Clamp(turnRate / 105f, -1f, 1f) * maximumTurnLean;
            float response = 1f - Mathf.Exp(-motionResponse * deltaTime);
            cameraLean = Mathf.Lerp(cameraLean, desiredLean, response);
            currentPullback = Mathf.Lerp(currentPullback, pullback, response);
        }

        if (player != null)
        {
            float response = deltaTime > 0f ? 1f - Mathf.Exp(-motionResponse * deltaTime) : 1f;
            float wantTilt = Mathf.Clamp(downwardTilt - player.FlightPitch * pitchFollow, 5f, 65f) - downwardTilt;
            orbitTiltOffset = Mathf.Lerp(orbitTiltOffset, wantTilt, response);
            float wantFov = (player.IsBoosting ? boostFovKick : 0f) + Mathf.Clamp01(player.Speed01 - 0.5f) * speedFovKick;
            fovKick = Mathf.Lerp(fovKick, wantFov, response);
        }

        previousYaw = yaw;
        Quaternion orbit = Quaternion.Euler(orbitTiltOffset, orbitYaw + turnLag, 0f);
        follow.FollowOffset = orbit * (restOffset + restOffset.normalized * currentPullback);
    }

    public void Shake(float strength)
    {
        if (impulse != null && strength > 0f)
            impulse.GenerateImpulseWithForce(strength);
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Aim && vcam.Follow != null)
        {
            float yaw = vcam.Follow.eulerAngles.y + orbitYaw + turnLag;
            state.RawOrientation = Quaternion.Euler(downwardTilt + orbitTiltOffset, yaw, cameraLean);
            state.Lens.FieldOfView += fovKick;
        }
    }

    private void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        capturedCursor = true;
    }

    private void ReleaseCursor()
    {
        if (!capturedCursor)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        capturedCursor = false;
    }
}
