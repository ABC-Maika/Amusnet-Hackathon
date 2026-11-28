using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Butterfly net on a pole. Standalone it waits by a flower bed; a villager can carry one
    /// (autoHunt off) and sweep it through DeathManager.Die.
    /// Charge: the net lifts high. Strike: it sweeps down in an arc and scoops the butterfly.
    /// </summary>
    public class NetDeath : ProximityKiller
    {
        public bool buildPlaceholderArt = true;
        public float liftAngle = 60f;

        Transform pivot, bag;
        Quaternion restRot;
        bool animating;

        protected override void ApplyDefaults()
        {
            displayName = "Net";
            warnRadius = 5.5f;
            strikeRadius = 2.8f;
            warningTime = 1.4f;
            telegraphTime = 0.8f;
            cooldown = 3.5f;
            killDelay = 0.25f;
            launchVelocity = new Vector3(0f, 0.3f, 0f);
            warningBarOffset = new Vector3(0f, 1.8f, 0f);
        }

        protected override void OnAwake()
        {
            if (!buildPlaceholderArt) return;
            Color pole = new Color(0.6f, 0.45f, 0.25f), rim = new Color(0.25f, 0.25f, 0.28f), netCol = new Color(0.95f, 0.95f, 0.85f);
            pivot = KillerArt.Pivot(transform, new Vector3(0f, 0.9f, 0f), "Net Pivot");
            KillerArt.Part(pivot, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.8f), new Vector3(0.05f, 0.8f, 0.05f), pole, new Vector3(90f, 0f, 0f), "Pole");
            var ring = KillerArt.Pivot(pivot, new Vector3(0f, 0f, 1.9f), "Ring");
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                KillerArt.Part(ring, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.32f, Mathf.Sin(a) * 0.32f, 0f),
                    new Vector3(0.03f, 0.21f, 0.03f), rim, new Vector3(0f, 0f, i * 36f), "Rim");
            }
            bag = KillerArt.Part(ring, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.3f), new Vector3(0.6f, 0.6f, 0.75f), netCol, default, "Bag");
            restRot = pivot.localRotation;
        }

        void LateUpdate()
        {
            if (!pivot || animating) return;
            Quaternion goal = restRot;
            if (Current == State.Warning || Current == State.Telegraph)
            {
                float yaw = YawTo(butterfly);
                goal = Quaternion.Euler(-liftAngle * ChargeProgress, yaw, 0f);
                if (Current == State.Telegraph) goal *= Quaternion.Euler(0f, Mathf.Sin(Time.time * 18f) * 3f, 0f);
            }
            pivot.localRotation = Quaternion.RotateTowards(pivot.localRotation, goal, 200f * Time.deltaTime);
        }

        float YawTo(Transform target)
        {
            if (!target) return pivot.localEulerAngles.y;
            Vector3 d = transform.InverseTransformDirection(target.position - pivot.position);
            d.y = 0f;
            return d.sqrMagnitude < 0.001f ? pivot.localEulerAngles.y : Quaternion.LookRotation(d).eulerAngles.y;
        }

        public override void OnDeathBegin(GameObject victim)
        {
            if (!pivot) return;
            animating = true;
            float yaw = YawTo(victim.transform);
            Quaternion up = Quaternion.Euler(-liftAngle, yaw - 40f, 0f), down = Quaternion.Euler(30f, yaw + 25f, 0f);
            Vector3 bagScale = bag.localScale;
            StartCoroutine(KillerArt.Tween(0.3f, t =>
            {
                pivot.localRotation = Quaternion.Slerp(up, down, KillerArt.EaseIn(t));
                bag.localScale = Vector3.Lerp(bagScale, new Vector3(0.5f, 0.5f, 0.95f), t); // the bag swings full
            }));
        }

        public override void OnDeathEnd(GameObject victim, float legacyGained)
        {
            if (!pivot) return;
            Quaternion from = pivot.localRotation;
            Vector3 bagFrom = bag.localScale;
            StartCoroutine(KillerArt.Tween(1f, t =>
            {
                pivot.localRotation = Quaternion.Slerp(from, restRot, KillerArt.EaseOut(t));
                bag.localScale = Vector3.Lerp(bagFrom, new Vector3(0.6f, 0.6f, 0.75f), t);
                if (t >= 1f) animating = false;
            }));
        }
    }
}
