using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Butterfly/Flight Settings")]
public sealed class ButterflyFlightSettings : ScriptableObject
{
    [Header("Steering")]
    [Min(1f)] public float yawSpeed = 105f;
    [Min(1f)] public float pitchSpeed = 95f;
    [Range(20f, 80f)] public float maximumPitch = 50f;
    [Range(2f, 20f)] public float neutralDescentPitch = 12f;

    [Header("Flight")]
    [FormerlySerializedAs("glideSpeed")]
    [Min(0.1f)] public float flightSpeed = 3.25f;
    [Min(0.1f)] public float boostSpeed = 7f;
    [Min(0.1f)] public float acceleration = 16f;

    [Header("Hover")]
    [Tooltip("With no input the butterfly stops and flutters on the spot instead of gliding forward.")]
    public bool hoverWhenIdle = true;
    [Min(0f)] public float hoverBobHeight = 0.04f;
    [Min(0.1f)] public float hoverBobSpeed = 2.4f;
    [Tooltip("How quickly it slows to a stop (lower is a longer, softer stop).")]
    [Min(0.1f)] public float hoverBrake = 4f;
    [Tooltip("Nose-up tilt while hovering, in degrees (negative is up).")]
    [Range(-45f, 0f)] public float hoverPitch = -38f;
    [Tooltip("Wing flap speed while hovering, 1 is full speed.")]
    [Range(0.1f, 1f)] public float hoverWingSpeed = 0.45f;

    [Header("Momentum (W / S)")]
    [Tooltip("Build-up while boosting.")]
    [Min(0.1f)] public float boostAcceleration = 5f;
    [Tooltip("How quickly speed bleeds off with no keys held (m/s per second).")]
    [Min(0.05f)] public float coastDeceleration = 1.1f;
    [Tooltip("How hard S brakes. S never reverses.")]
    [Min(0.1f)] public float brakeDeceleration = 6f;
    [Min(0.1f)] public float throttleResponse = 4f;
    [Tooltip("How tightly the flight path follows the heading (higher = snappier, lower = more drift in turns).")]
    [Range(1f, 20f)] public float turnGrip = 5f;

    [Header("Mouse pitch")]
    [Tooltip("Degrees of flight pitch per mouse pixel; mouse up climbs.")]
    [Min(0.01f)] public float mouseSensitivity = 0.12f;

    [Header("Motion")]
    [Range(0f, 45f)] public float bankAngle = 28f;
    [Min(0.1f)] public float bankResponse = 5f;
    [Min(0f)] public float flutterHeight = 0.07f;
    [Min(0.1f)] public float flutterSpeed = 7f;

    [Header("Controls")]
    [Tooltip("Rising speed with Space (m/s).")]
    [Min(0.1f)] public float climbSpeed = 1.7f;
    [Tooltip("Descending speed with Ctrl or C (m/s).")]
    [Min(0.1f)] public float descendSpeed = 2.2f;
    [Tooltip("Reverse speed with S, as a fraction of flight speed.")]
    [Range(0.1f, 1f)] public float backwardFactor = 0.5f;
    [Tooltip("Speed when walking on the ground.")]
    [Min(0.1f)] public float groundWalkSpeed = 0.7f;
    [Tooltip("Nose-down lean while flying forward, in degrees.")]
    [Range(0f, 25f)] public float forwardLean = 8f;
}
