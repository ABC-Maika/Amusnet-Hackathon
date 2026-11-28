using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Fly swatter. Standalone it hangs on a hook and swings at a butterfly that comes close;
    /// a villager can also carry one (autoHunt off) and swing it through DeathManager.Die.
    /// Charge: the swatter winds back. Strike: a fast slap down onto the butterfly.
    /// </summary>
    public class SwatterDeath : ProximityKiller
    {
        public bool buildPlaceholderArt = true;
        [Tooltip("Degrees the swatter winds back while the attack charges.")]
        public float windUpAngle = 70f;

        Transform pivot;
        Quaternion restRot;
        bool animating;

        protected override void ApplyDefaults()
        {
            displayName = "Swatter";
            warnRadius = 4.5f;
            strikeRadius = 2.2f;
            warningTime = 1.2f;
            telegraphTime = 0.6f;
            cooldown = 3f;
            killDelay = 0.12f;
            launchVelocity = new Vector3(0f, -3f, 0f);
            warningBarOffset = new Vector3(0f, 1.6f, 0f);
        }

        protected override void OnAwake()
        {
            if (!buildPlaceholderArt) return;
            Color handle = new Color(0.55f, 0.32f, 0.16f), mesh = new Color(0.85f, 0.18f, 0.15f);
            pivot = KillerArt.Pivot(transform, new Vector3(0f, 0.9f, 0f), "Swatter Pivot");
            KillerArt.Part(pivot, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.45f), new Vector3(0.05f, 0.45f, 0.05f), handle, new Vector3(90f, 0f, 0f), "Handle");
            KillerArt.Part(pivot, PrimitiveType.Cube, new Vector3(0f, 0f, 1.1f), new Vector3(0.45f, 0.03f, 0.5f), mesh, default, "Head");
            KillerArt.Part(pivot, PrimitiveType.Cube, new Vector3(0f, 0.02f, 1.1f), new Vector3(0.5f, 0.02f, 0.04f), mesh * 0.7f, default, "Rib");
            restRot = pivot.localRotation;
        }

        void LateUpdate()
        {
            if (!pivot || animating) return;
            Quaternion goal = restRot;
            if (Current == State.Warning || Current == State.Telegraph)
            {
                float jitter = Current == State.Telegraph ? Random.Range(-2f, 2f) : 0f;
                goal = Quaternion.Euler(-windUpAngle * ChargeProgress + jitter, YawTo(butterfly), 0f);
            }
            pivot.localRotation = Quaternion.RotateTowards(pivot.localRotation, goal, 240f * Time.deltaTime);
        }

        // Local yaw that points the head at the target.
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
            Quaternion back = Quaternion.Euler(-windUpAngle, yaw, 0f), slap = Quaternion.Euler(35f, yaw, 0f);
            pivot.localRotation = back;
            StartCoroutine(KillerArt.Tween(0.14f, t => pivot.localRotation = Quaternion.Slerp(back, slap, KillerArt.EaseIn(t))));
        }

        public override void OnDeathEnd(GameObject victim, float legacyGained)
        {
            if (!pivot) return;
            Quaternion from = pivot.localRotation;
            StartCoroutine(KillerArt.Tween(0.8f, t =>
            {
                pivot.localRotation = Quaternion.Slerp(from, restRot, KillerArt.EaseOut(t));
                if (t >= 1f) animating = false;
            }));
        }
    }
}
