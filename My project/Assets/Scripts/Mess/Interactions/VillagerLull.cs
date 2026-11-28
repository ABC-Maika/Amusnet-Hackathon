using UnityEngine;

namespace Amusnet.Mess
{
    // After a boat escapes the map the village is empty for a while: villagers and rescuers are hidden and anger stops
    // rising. Created on demand by BoatDrift; removes itself when the time is up.
    public sealed class VillagerLull : MonoBehaviour
    {
        private static VillagerLull active;
        private float left;
        private GameObject[] hidden;

        public static void Begin(float seconds)
        {
            if (active) { active.left = Mathf.Max(active.left, seconds); return; }
            active = new GameObject("Villager Lull").AddComponent<VillagerLull>();
            active.left = seconds;
        }

        private void Start()
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var v in FindObjectsByType<Amusnet.NPC.VillagerAI>(FindObjectsSortMode.None)) list.Add(v.gameObject);
            foreach (var r in FindObjectsByType<RescueCrew>(FindObjectsSortMode.None)) list.Add(r.gameObject);
            foreach (var g in list) g.SetActive(false);
            hidden = list.ToArray();
            if (Butterfly.NPC.AngerLevels.Instance) Butterfly.NPC.AngerLevels.Instance.Frozen = true;
        }

        private void Update()
        {
            left -= Time.deltaTime;
            if (left > 0f) return;
            foreach (var g in hidden) if (g) g.SetActive(true);
            if (Butterfly.NPC.AngerLevels.Instance) Butterfly.NPC.AngerLevels.Instance.Frozen = false;
            HudNotice.Show("The villagers are back!");
            active = null;
            Destroy(gameObject);
        }
    }
}
