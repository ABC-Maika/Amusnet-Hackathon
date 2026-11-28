using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Spider in a web strung between two posts. Place the object at web height (about 2.5 m up).
    /// Warning: the spider creeps down its thread. Strike: it drops onto the butterfly and wraps it.
    /// Builds placeholder primitives unless buildPlaceholderArt is off (then assign a model + Animator).
    /// </summary>
    public class SpiderDeath : ProximityKiller
    {
        public bool buildPlaceholderArt = true;
        [Tooltip("How far the spider creeps down while the attack charges.")]
        public float creepDistance = 0.6f;

        Transform spider, thread;
        Vector3 rest;
        bool animating;

        protected override void ApplyDefaults()
        {
            displayName = "Spider";
            warnRadius = 5f;
            strikeRadius = 2.6f;
            warningTime = 1.5f;
            telegraphTime = 1f;
            cooldown = 4f;
            killDelay = 0.2f;
            launchVelocity = new Vector3(0f, 0.5f, 0f);
            warningBarOffset = new Vector3(0f, 1.3f, 0f);
        }

        protected override void OnAwake()
        {
            if (!buildPlaceholderArt) return;
            var root = transform;
            Color wood = new Color(0.36f, 0.25f, 0.15f), silk = new Color(0.92f, 0.92f, 0.95f), black = new Color(0.08f, 0.06f, 0.07f);

            // Two posts and the web between them.
            KillerArt.Part(root, PrimitiveType.Cylinder, new Vector3(-1.1f, -1.2f, 0f), new Vector3(0.12f, 1.9f, 0.12f), wood, default, "Post L");
            KillerArt.Part(root, PrimitiveType.Cylinder, new Vector3(1.1f, -1.2f, 0f), new Vector3(0.12f, 1.9f, 0.12f), wood, default, "Post R");
            var web = KillerArt.Pivot(root, Vector3.zero, "Web");
            for (int i = 0; i < 4; i++)
                KillerArt.Part(web, PrimitiveType.Cube, Vector3.zero, new Vector3(2.2f, 0.02f, 0.02f), silk, new Vector3(0f, 0f, i * 45f), "Strand");
            for (int r = 1; r <= 3; r++)
                KillerArt.Part(web, PrimitiveType.Cylinder, Vector3.zero, new Vector3(r * 0.6f, 0.005f, r * 0.6f), new Color(silk.r, silk.g, silk.b), new Vector3(90f, 0f, 0f), "Ring");

            // Spider: body, head and eight legs.
            spider = KillerArt.Pivot(root, new Vector3(0f, 0.15f, -0.08f), "Spider");
            KillerArt.Part(spider, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.32f, 0.3f, 0.4f), black, default, "Body");
            var head = KillerArt.Part(spider, PrimitiveType.Sphere, new Vector3(0f, -0.02f, -0.24f), Vector3.one * 0.18f, black, default, "Head");
            KillerArt.Glow(KillerArt.Part(head, PrimitiveType.Sphere, new Vector3(-0.2f, 0.15f, -0.4f), Vector3.one * 0.25f, Color.red), Color.red, 2f);
            KillerArt.Glow(KillerArt.Part(head, PrimitiveType.Sphere, new Vector3(0.2f, 0.15f, -0.4f), Vector3.one * 0.25f, Color.red), Color.red, 2f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                    KillerArt.Part(spider, PrimitiveType.Cube, new Vector3(side * 0.22f, -0.04f, -0.14f + i * 0.1f),
                        new Vector3(0.36f, 0.025f, 0.025f), black, new Vector3(0f, (i - 1.5f) * 18f * side, side * -25f), "Leg");

            thread = KillerArt.Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.012f, 0f, 0.012f), silk, default, "Thread");
            rest = spider.localPosition;
            UpdateThread();
        }

        void LateUpdate()
        {
            if (!spider || animating) return;
            Vector3 goal = rest;
            if (Current == State.Warning || Current == State.Telegraph)
            {
                goal += Vector3.down * creepDistance * ChargeProgress;
                if (Current == State.Telegraph) goal += Random.insideUnitSphere * 0.02f; // twitching
                if (butterfly)
                {
                    Vector3 look = Vector3.ProjectOnPlane(butterfly.position - spider.position, Vector3.up);
                    if (look.sqrMagnitude > 0.001f) spider.rotation = Quaternion.LookRotation(-look); // head is -z
                }
            }
            spider.localPosition = Vector3.MoveTowards(spider.localPosition, goal, 2f * Time.deltaTime);
            UpdateThread();
        }

        void UpdateThread()
        {
            if (!thread) return;
            Vector3 top = rest + Vector3.up * 0.35f, bottom = spider.localPosition;
            thread.localPosition = (top + bottom) * 0.5f;
            thread.localScale = new Vector3(0.012f, Mathf.Max(0.001f, (top - bottom).magnitude * 0.5f), 0.012f);
            thread.localRotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
        }

        public override void OnDeathBegin(GameObject victim)
        {
            if (!spider) return;
            animating = true;
            Vector3 from = spider.localPosition;
            Vector3 to = transform.InverseTransformPoint(victim.transform.position + Vector3.up * 0.1f);
            StartCoroutine(KillerArt.Tween(0.22f, t =>
            {
                spider.localPosition = Vector3.Lerp(from, to, KillerArt.EaseIn(t));
                UpdateThread();
            }));
        }

        public override void OnDeathEnd(GameObject victim, float legacyGained)
        {
            if (!spider) return;
            Vector3 from = spider.localPosition;
            StartCoroutine(KillerArt.Tween(1.2f, t =>
            {
                spider.localPosition = Vector3.Lerp(from, rest, KillerArt.EaseOut(t));
                UpdateThread();
                if (t >= 1f) animating = false;
            }));
        }
    }
}
