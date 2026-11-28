using UnityEngine;

/// <summary>Trigger volume of one speed-boost tree or flower; NatureSpots creates and owns it.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class SpeedBoostSpot : MonoBehaviour
{
    NatureSpots owner;
    int index;

    public void Init(NatureSpots spots, int spotIndex)
    {
        owner = spots;
        index = spotIndex;
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner != null) owner.Touch(index, other);
    }
}
