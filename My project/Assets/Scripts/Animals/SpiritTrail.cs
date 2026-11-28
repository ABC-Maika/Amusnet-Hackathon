using System.Collections.Generic;
using Butterfly.Death;
using UnityEngine;

namespace Butterfly.Animals
{
    /// <summary>
    /// The martyr's successor: at run start a glowing spirit orb leads the new butterfly toward the places
    /// previous butterflies died (where the biggest chain reactions happened). The more Rebel Legacy,
    /// the more spots it knows (one per 25 legacy, max 4).
    /// Put one in the scene; it finds the butterfly by name ("Butterfly...") or via the Player field.
    /// </summary>
    public class SpiritTrail : MonoBehaviour
    {
        public Transform player;
        public float legacyPerSpot = 25f;
        public int maxSpots = 4;
        public float orbSpeed = 6f;
        public float orbHeight = 2.5f;
        [Tooltip("Orb waits for the player when further than this.")]
        public float waitDistance = 12f;
        public Color color = new Color(0.6f, 0.9f, 1f);

        Transform orb;
        List<Vector3> spots = new List<Vector3>();
        int index;

        public int SpotCount => spots.Count;

        void Start()
        {
            ResolvePlayer();
            int allowed = Mathf.Min(maxSpots, Mathf.FloorToInt(LegacyBank.Total / legacyPerSpot));
            var saved = LegacyBank.GetDeathPoints();
            // newest points first
            for (int i = saved.Count - 1; i >= 0 && spots.Count < allowed; i--) spots.Add(saved[i]);

            if (spots.Count == 0) { enabled = false; return; }
            BuildOrb();
        }

        void ResolvePlayer()
        {
            if (player != null) return;
            player = Butterfly.Death.ButterflyLocator.Find();
        }

        void BuildOrb()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpiritOrb";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.5f;
            go.GetComponent<Renderer>().material.color = color;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 1.2f;
            trail.startWidth = 0.35f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default")) { color = color };
            orb = go.transform;
            if (player != null) orb.position = player.position + Vector3.up * orbHeight;
        }

        void Update()
        {
            if (orb == null) return;
            ResolvePlayer();
            if (player == null) return;

            Vector3 target = spots[index] + Vector3.up * orbHeight;
            bool playerFar = (player.position - orb.position).sqrMagnitude > waitDistance * waitDistance;
            if (!playerFar)
                orb.position = Vector3.MoveTowards(orb.position, target, orbSpeed * Time.deltaTime);
            orb.position += Vector3.up * Mathf.Sin(Time.time * 3f) * 0.01f;

            if ((orb.position - target).sqrMagnitude < 1f)
            {
                index++;
                if (index >= spots.Count) { Destroy(orb.gameObject); orb = null; enabled = false; }
            }
        }
    }
}
