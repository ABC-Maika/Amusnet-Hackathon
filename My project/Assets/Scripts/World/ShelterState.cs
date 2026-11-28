using System.Collections.Generic;
using UnityEngine;

namespace Amusnet.World
{
    public sealed class ShelterState : MonoBehaviour
    {
        private readonly HashSet<(HidingSpot spot, Collider body)> contacts = new();
        public bool IsHidden
        {
            get
            {
                contacts.RemoveWhere(c => !c.spot || !c.spot.isActiveAndEnabled || !c.body || !c.body.enabled || !c.spot.StillOverlaps(c.body));
                return contacts.Count != 0;
            }
        }
        public void Enter(HidingSpot spot, Collider body) => contacts.Add((spot, body));
        public void Leave(HidingSpot spot, Collider body) => contacts.Remove((spot, body));
        void OnDisable() => contacts.Clear();
    }
}
