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
    [Range(-45f, 0f)] public float hoverPitch = -22f;
    [Tooltip("Wing flap speed while hovering, 1 is full speed.")]
    [Range(0.1f, 1f)] public float hoverWingSpeed = 0.45f;
}
