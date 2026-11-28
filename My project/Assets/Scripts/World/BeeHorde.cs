using UnityEngine;

namespace Amusnet.World
{
    public sealed class BeeHorde : MonoBehaviour
    {
        private enum Phase { Approaching, Attacking, Returning, Settling }

        [SerializeField] private Transform[] bees;
        [SerializeField, Min(.1f)] private float attackDuration = 5f;
        [SerializeField, Min(.1f)] private float travelSpeed = 6f;

        private Phase phase;
        private Vector3 flowers;
        private float phaseTime;
        private float flightTime;
        private float approachSpeed;
        private bool deployed;
        private Quaternion originalRotation;
        private Quaternion reactionRotation;
        private bool reacting;

        public Transform Target { get; private set; }
        public bool IsAttacking => deployed && phase == Phase.Attacking;
        public bool IsReturning => deployed && phase == Phase.Returning;

        public void Deploy(Vector3 flowerPosition, Transform villager)
        {
            ReleaseTarget();
            flowers = flowerPosition;
            transform.position = flowers;
            Target = villager;
            approachSpeed = villager ? Mathf.Max(travelSpeed, Vector3.Distance(flowers, AimPoint()) / 2.5f) : travelSpeed;
            deployed = true;
            SetPhase(villager ? Phase.Approaching : Phase.Settling);
        }

        private void LateUpdate()
        {
            if (!deployed)
                return;

            float step = Time.deltaTime;
            phaseTime += step;
            flightTime += step;
            switch (phase)
            {
                case Phase.Approaching:
                    if (!Target || !Target.gameObject.activeInHierarchy)
                        BeginReturn();
                    else if (FlyTo(AimPoint(), step))
                        SetPhase(Phase.Attacking);
                    break;
                case Phase.Attacking:
                    if (!Target || !Target.gameObject.activeInHierarchy || phaseTime >= attackDuration)
                        BeginReturn();
                    else
                    {
                        FlyTo(AimPoint(), step);
                        ReactToStings();
                    }
                    break;
                case Phase.Returning:
                    if (FlyTo(flowers, step))
                        SetPhase(Phase.Settling);
                    break;
                case Phase.Settling:
                    if (phaseTime >= 1.25f)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    break;
            }

            AnimateBees();
        }

        private Vector3 AimPoint() => Target.position + Vector3.up * 1.3f;

        private bool FlyTo(Vector3 destination, float step)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, approachSpeed * step);
            return (transform.position - destination).sqrMagnitude <= .06f;
        }

        private void BeginReturn()
        {
            ReleaseTarget();
            SetPhase(Phase.Returning);
        }

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        private void ReactToStings()
        {
            RestoreRotation();
            originalRotation = Target.localRotation;
            float flinch = Mathf.Sin(phaseTime * 13f);
            reactionRotation = originalRotation * Quaternion.Euler(7f + 5f * flinch, 0f, 13f * flinch);
            Target.localRotation = reactionRotation;
            reacting = true;
        }

        private void RestoreRotation()
        {
            if (reacting && Target && Quaternion.Angle(Target.localRotation, reactionRotation) < .1f)
                Target.localRotation = originalRotation;
            reacting = false;
        }

        private void ReleaseTarget()
        {
            RestoreRotation();
            Target = null;
        }

        private void OnDisable() => ReleaseTarget();

        private void AnimateBees()
        {
            float scale = Mathf.SmoothStep(.1f, 1f, flightTime / .45f);
            if (phase == Phase.Settling)
                scale *= 1f - Mathf.SmoothStep(0f, 1f, phaseTime / 1.25f);

            for (int i = 0; i < bees.Length; i++)
            {
                Transform bee = bees[i];
                float angle = i * 2.39996f + flightTime * (4.4f + i % 4 * .45f);
                float radius = (.4f + .13f * (i % 5)) * (.83f + .17f * Mathf.Sin(angle * 2f));
                float height = .34f * Mathf.Sin(angle * 1.7f + i);
                bee.localPosition = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius) * scale;
                bee.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)));
                bee.localScale = Vector3.one * scale;
                float flap = 20f + Mathf.Sin(flightTime * 95f + i) * 34f;
                bee.GetChild(4).localRotation = Quaternion.Euler(0f, 0f, -flap);
                bee.GetChild(5).localRotation = Quaternion.Euler(0f, 0f, flap);
            }
        }
    }
}

