using System.Collections.Generic;
using UnityEngine;

namespace Amusnet.Mess
{
    // Villagers who live by a mooring. When the boat is cut loose they run along the bank after it, and when someone ends up
    // in the water they wade in as far as they dare and haul the swimmer out.
    public sealed class RescueCrew : MonoBehaviour
    {
        private sealed class Member
        {
            public Transform t;
            public Animator anim;
            public Vector3 home;
            public int slot;
            public float grab;
            public float lastMove;
        }

        [SerializeField] private BoatDrift boat;
        [SerializeField] private GameObject[] prefabs;
        [SerializeField] private Vector3[] homes;
        [SerializeField] private float runSpeed = 4.6f;
        [SerializeField] private float wadeDepth = 1.3f;   // they will not go in deeper than this
        [SerializeField] private float waterY = -0.71f;

        private readonly List<Member> crew = new List<Member>();
        private Terrain terrain;
        private bool alerted, calledOff;

        private void Start()
        {
            terrain = Terrain.activeTerrain;
            for (int i = 0; i < homes.Length; i++)
            {
                var go = Instantiate(prefabs[i % prefabs.Length], homes[i], Quaternion.identity, transform);
                go.name = "Rescuer " + (i + 1);
                foreach (var ag in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>()) Destroy(ag);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>()) if (mb && !(mb is RescueCrew)) Destroy(mb);
                var m = new Member { t = go.transform, anim = go.GetComponentInChildren<Animator>(), home = homes[i], slot = i };
                m.t.position = Ground(homes[i]);
                m.t.rotation = Quaternion.Euler(0f, 160f, 0f);
                if (m.anim) m.anim.speed = 0f;
                crew.Add(m);
            }
        }

        private float GroundY(Vector3 p) => terrain ? terrain.SampleHeight(p) + terrain.transform.position.y : 0f;
        private Vector3 Ground(Vector3 p) { p.y = GroundY(p); return p; }

        // First dry-ish point on the village bank at this x (the village is on the +z side of the creek).
        private float BankZ(float x)
        {
            float z = RiverLane.ZAt(x, -19f);
            for (int i = 0; i < 80 && z < -4f; i++, z += 0.25f)
                if (GroundY(new Vector3(x, 0f, z)) > waterY + 0.25f) break;
            return z + 0.3f;
        }

        private BoatPassenger Victim(Member m)
        {
            BoatPassenger best = null;
            float bestD = float.MaxValue;
            foreach (var p in boat.Passengers)
            {
                if (!p || !p.gameObject.activeInHierarchy || !p.NeedsHelp) continue;
                float d = (p.transform.position - m.t.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        private void Update()
        {
            if (!boat) return;
            if (boat.IsDrifting && !alerted)
            {
                alerted = true;
                HudNotice.Show("Villagers run after the boat!");
            }
            foreach (var m in crew)
            {
                var victim = alerted ? Victim(m) : null;
                Vector3 goal;
                float speed = runSpeed;
                bool toVictim = victim != null;
                if (toVictim) goal = victim.transform.position;
                else if (alerted && !boat.HasArrived) goal = new Vector3(boat.Boat.position.x - 1.5f - m.slot * 2.2f, 0f, BankZ(boat.Boat.position.x) + m.slot * 0.9f);
                else { goal = m.home; speed = 2.2f; }

                Vector3 to = goal - m.t.position; to.y = 0f;
                float dist = to.magnitude;
                bool arrived = toVictim ? dist < 1.6f : dist < 0.6f;
                bool moving = false;
                if (!arrived && dist > 0.01f)
                {
                    Vector3 dir = to / dist;
                    Vector3 next = m.t.position + dir * (speed * Time.deltaTime);
                    // They wade, but won't go beyond waist depth.
                    bool tooDeep = GroundY(next) < waterY - wadeDepth && GroundY(next) < GroundY(m.t.position);
                    if (!tooDeep)
                    {
                        next.y = GroundY(next);
                        m.t.position = next;
                        moving = true;
                    }
                    m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                }
                if (m.anim) m.anim.speed = Mathf.MoveTowards(m.anim.speed, moving ? speed / 2.4f : 0f, Time.deltaTime * 10f);

                if (toVictim && dist < 1.8f)
                {
                    m.grab += Time.deltaTime;
                    if (m.grab > 1.2f)
                    {
                        m.grab = 0f;
                        Vector3 bank = new Vector3(0f, 0f, 1f);
                        victim.Rescue(bank);
                        HudNotice.Show("Rescued!");
                    }
                }
                else m.grab = 0f;
            }
        }
    }
}
