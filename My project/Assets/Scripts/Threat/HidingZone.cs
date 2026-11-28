using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal stand-in for the Sprint 2 [D] hiding-spot system (HidingSpot.cs, not built yet).
/// Put it on a GameObject with a trigger collider (box, sphere, capsule or convex mesh): a point
/// inside any enabled zone counts as hidden. It checks positions directly instead of using
/// OnTriggerEnter, so the player needs no Rigidbody. Replace IsHidden's callers when HidingSpot lands.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HidingZone : MonoBehaviour
{
    static readonly List<HidingZone> active = new();

    Collider zone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => active.Clear();

    /// <summary>True when the point is inside any enabled hiding zone.</summary>
    public static bool IsHidden(Vector3 position)
    {
        foreach (var z in active)
        {
            // ClosestPoint returns the point itself when it is inside the collider.
            if ((z.zone.ClosestPoint(position) - position).sqrMagnitude < 1e-6f) return true;
        }
        return false;
    }

    void Awake()
    {
        zone = GetComponent<Collider>();
        zone.isTrigger = true;
    }

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);
}
