using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CinemachineCamera), typeof(CinemachineFollow))]
public sealed class ButterflyCamera : CinemachineExtension
{
    [SerializeField] private CinemachineImpulseSource impulse;
    [SerializeField, Range(5f, 65f)] private float downwardTilt = 20.6f;
    [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.18f;

    private CinemachineFollow follow;
    private Vector3 restOffset;
    private float orbitYaw;
    private float orbitTiltOffset;
    private bool capturedCursor;

    protected override void Awake()
    {
        base.Awake();
        follow = GetComponent<CinemachineFollow>();
        restOffset = follow.FollowOffset;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (Application.isPlaying)
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
        if (!Application.isPlaying || !Application.isFocused)
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
        Vector2 delta = pixels * mouseSensitivity;
        orbitYaw = Mathf.Repeat(orbitYaw + delta.x, 360f);
        orbitTiltOffset = Mathf.Clamp(downwardTilt + orbitTiltOffset - delta.y, 5f, 65f) - downwardTilt;

        Quaternion orbit = Quaternion.Euler(orbitTiltOffset, orbitYaw, 0f);
        follow.FollowOffset = orbit * restOffset;
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
            float yaw = vcam.Follow.eulerAngles.y + orbitYaw;
            state.RawOrientation = Quaternion.Euler(downwardTilt + orbitTiltOffset, yaw, 0f);
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
