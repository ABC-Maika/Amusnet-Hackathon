using System.Collections.Generic;
using UnityEngine;

namespace Amusnet.World
{
    public sealed class FlowerBeeSwarm : MonoBehaviour
    {
        [SerializeField] private ParticleSystem pollen;
        [SerializeField] private Renderer[] flowers;
        [SerializeField] private BeeHorde hordePrefab;
        [SerializeField] private Transform[] villagers;
        [SerializeField, Min(0f), Tooltip("Zero allows the horde to seek villagers anywhere in the scene.")] private float attackRange;

        private const float RevisitDelay = 15f;
        private const int MaxHordes = 6;

        private sealed class Flower
        {
            public Bounds Target;
            public float ReadyAt;
        }

        private readonly List<Flower> targets = new();
        private readonly List<BeeHorde> hordes = new();
        private ParticleSystem.Particle[] particles;

        private void Awake()
        {
            particles = new ParticleSystem.Particle[pollen.main.maxParticles];
            foreach (Renderer flower in flowers)
            {
                if (!flower || !flower.gameObject.activeInHierarchy)
                    continue;

                Bounds bounds = flower.bounds;
                bounds.Expand(new Vector3(.85f, .9f, .85f));
                targets.Add(new Flower { Target = bounds });
            }
        }

        private void LateUpdate()
        {
            hordes.RemoveAll(horde => !horde);
            if (hordes.Count < MaxHordes)
                CheckPollen();
        }

        private void CheckPollen()
        {
            int count = pollen.GetParticles(particles);
            if (count == 0)
                return;

            var main = pollen.main;
            Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Custom
                ? main.customSimulationSpace
                : pollen.transform;
            float now = Time.time;

            bool caught = false;
            for (int i = 0; i < count && hordes.Count < MaxHordes; i++)
            {
                if (particles[i].remainingLifetime <= 0f)
                    continue;

                Vector3 point = particles[i].position;
                if (main.simulationSpace != ParticleSystemSimulationSpace.World && space)
                    point = space.TransformPoint(point);

                Flower nearest = null;
                float nearestDistance = float.PositiveInfinity;
                foreach (Flower flower in targets)
                {
                    if (flower.ReadyAt > now || !flower.Target.Contains(point))
                        continue;
                    float distance = (flower.Target.center - point).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearest = flower;
                        nearestDistance = distance;
                    }
                }

                if (nearest == null)
                    continue;

                // Treat the overlapping flowers in a bed as one swarm.
                Vector3 center = nearest.Target.center;
                foreach (Flower flower in targets)
                {
                    if ((flower.Target.center - center).sqrMagnitude < 1.1f * 1.1f)
                        flower.ReadyAt = now + RevisitDelay;
                }
                Vector3 launchPoint = new Vector3(center.x, nearest.Target.max.y - .25f, center.z);
                Transform target = FindVillager(launchPoint);
                BeeHorde horde = Instantiate(hordePrefab, launchPoint, Quaternion.identity, transform);
                horde.Deploy(launchPoint, target);
                hordes.Add(horde);
                particles[i].remainingLifetime = -1f;
                caught = true;
            }
            if (caught)
                pollen.SetParticles(particles, count);
        }

        private Transform FindVillager(Vector3 center)
        {
            Transform nearest = null;
            float rangeSquared = attackRange > 0f ? attackRange * attackRange : float.PositiveInfinity;
            foreach (Transform villager in villagers)
            {
                if (!villager || !villager.gameObject.activeInHierarchy)
                    continue;

                float distance = (villager.position - center).sqrMagnitude;
                if (distance >= rangeSquared || hordes.Exists(horde => horde.Target == villager))
                    continue;

                nearest = villager;
                rangeSquared = distance;
            }
            return nearest;
        }
    }
}

