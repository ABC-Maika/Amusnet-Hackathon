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
    [Min(0.1f)] public float flightSpeed = 5f;
    [Min(0.1f)] public float acceleration = 16f;
}
