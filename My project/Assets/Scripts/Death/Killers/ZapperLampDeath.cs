using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Bug-zapper lamp. Its glow pulls you in at night: the light pulses faster as the attack charges,
    /// then a bolt arcs to the butterfly. Furious villagers also put these down as daytime traps
    /// (nightOnly off). Hook ProximityKiller.IsNight to the day-night system.
    /// </summary>
    public class ZapperLampDeath : ProximityKiller
    {
        public bool buildPlaceholderArt = true;
        public Color glowColor = new Color(0.45f, 0.55f, 1f);
        [Tooltip("Seconds before a trap placed by a villager removes itself. 0 = stays forever.")]
        public float lifetime = 0f;

        Transform cage;
        Light lamp;
        LineRenderer bolt;
        bool animating;
        float age;

        protected override void ApplyDefaults()
        {
            displayName = "Zapper Lamp";
            nightOnly = true;
            warnRadius = 6f;
            strikeRadius = 3f;
            warningTime = 1.6f;
            telegraphTime = 0.7f;
            cooldown = 5f;
            killDelay = 0.1f;
            legacyMultiplier = 1.25f;
            launchVelocity = new Vector3(0f, 1.5f, 0f);
            warningBarOffset = new Vector3(0f, 2.6f, 0f);
        }

        protected override void OnAwake()
        {
            if (!buildPlaceholderArt) return;
            Color metal = new Color(0.2f, 0.2f, 0.22f);
            KillerArt.Part(transform, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.08f, 0.9f, 0.08f), metal, default, "Pole");
            KillerArt.Part(transform, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.03f, 0.5f), metal, default, "Base");
            cage = KillerArt.Part(transform, PrimitiveType.Cube, new Vector3(0f, 2.05f, 0f), new Vector3(0.38f, 0.5f, 0.38f), glowColor, default, "Cage");
            KillerArt.Part(transform, PrimitiveType.Cube, new Vector3(0f, 2.35f, 0f), new Vector3(0.48f, 0.08f, 0.48f), metal, default, "Cap");
            KillerArt.Glow(cage, glowColor, 1.5f);

            lamp = KillerArt.Pivot(transform, new Vector3(0f, 2.05f, 0f), "Lamp Light").gameObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = glowColor;
            lamp.range = 6f;
            lamp.intensity = 1.5f;

            bolt = KillerArt.Pivot(transform, Vector3.zero, "Bolt").gameObject.AddComponent<LineRenderer>();
            bolt.useWorldSpace = true;
            bolt.positionCount = 8;
            bolt.startWidth = 0.06f;
            bolt.endWidth = 0.02f;
            bolt.material = new Material(Shader.Find("Sprites/Default"));
            bolt.startColor = Color.white;
            bolt.endColor = glowColor;
            bolt.enabled = false;
        }

        void LateUpdate()
        {
            if (lifetime > 0f && !animating && (age += Time.deltaTime) > lifetime) { Destroy(gameObject); return; }
            if (!cage || animating) return;

            bool on = !nightOnly || IsNight();
            float charge = ChargeProgress;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(2f, 22f, charge));
            float glow = on ? Mathf.Lerp(1.2f, 4f, charge) * (0.7f + 0.3f * pulse) : 0.1f;
            KillerArt.Glow(cage, glowColor, glow);
            lamp.enabled = on;
            lamp.intensity = glow;
        }

        public override void OnDeathBegin(GameObject victim)
        {
            if (!cage) return;
            animating = true;
            StartCoroutine(KillerArt.Tween(0.6f, t =>
            {
                bool flash = t < 0.85f;
                bolt.enabled = flash;
                if (flash) DrawBolt(cage.position, victim ? victim.transform.position : cage.position);
                float glow = Mathf.Lerp(12f, 1.2f, t);
                KillerArt.Glow(cage, Color.Lerp(Color.white, glowColor, t), glow);
                lamp.color = Color.Lerp(Color.white, glowColor, t);
                lamp.intensity = glow;
                if (t >= 1f) animating = false;
            }));
        }

        void DrawBolt(Vector3 a, Vector3 b)
        {
            int n = bolt.positionCount;
            for (int i = 0; i < n; i++)
            {
                float f = i / (n - 1f);
                Vector3 jitter = (i == 0 || i == n - 1) ? Vector3.zero : Random.insideUnitSphere * 0.18f;
                bolt.SetPosition(i, Vector3.Lerp(a, b, f) + jitter);
            }
        }
    }
}
