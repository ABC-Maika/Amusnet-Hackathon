using System;
using UnityEngine;
using UnityEngine.AI;

namespace Amusnet.NPC
{
    /// <summary>Optional authored route and prop animation for a villager's calm activities.</summary>
    [DisallowMultipleComponent]
    public sealed class VillagerDailyRoutine : MonoBehaviour
    {
        public enum Chore { Rest, Sweep, Water, Carry, Feed }

        [Serializable]
        public struct Stop
        {
            public Chore chore;
            public Vector3 position;
            public Vector3 lookAt;
            [Min(1f)] public float seconds;
            public GameObject tool;
        }

        [SerializeField] private Stop[] stops = Array.Empty<Stop>();
        [SerializeField, Min(0f)] private float firstDeparture = 1f;

        public float FirstDeparture => firstDeparture;
        public bool AtStop { get; private set; }
        public Chore CurrentChore => showingTool || AtStop ? Current.chore : Chore.Rest;
        public bool IsRestStop => Current.chore == Chore.Rest;
        public float StopDuration => Current.seconds;
        public Vector3 LookAt => Current.lookAt;
        private Stop Current => stops[index];

        private NavMeshPath path;
        private int index;
        private Vector3 destination;
        private Arm left, right;
        private Transform spine;
        private Quaternion spineAtRest;
        private Tool[] tools;
        private Tool activeTool;
        private bool showingTool;
        private float poseStarted, nextParticle;

        private sealed class Tool
        {
            public GameObject model;
            public Transform leftGrip, rightGrip, emitter;
            public ParticleSystem particles;
        }

        private struct Arm
        {
            public Transform upper, forearm, hand;

            public Arm(Transform spine, string side)
            {
                upper = spine.Find("UpperArm." + side);
                forearm = upper ? upper.Find("Forearm." + side) : null;
                hand = forearm ? forearm.Find("Hand." + side) : null;
            }

            public void Reach(Vector3 point, Vector3 bend, float blend)
            {
                if (!hand) return;
                Vector3 shoulder = upper.position;
                Vector3 offset = point - shoulder;
                if (offset.sqrMagnitude < .000001f) return;
                float upperLength = Vector3.Distance(shoulder, forearm.position);
                float forearmLength = Vector3.Distance(forearm.position, hand.position);
                float reach = Mathf.Clamp(offset.magnitude, Mathf.Abs(upperLength - forearmLength) + .001f,
                    upperLength + forearmLength - .001f);
                Vector3 direction = offset.normalized;
                float along = (upperLength * upperLength + reach * reach - forearmLength * forearmLength) / (2f * reach);
                float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
                Vector3 elbow = shoulder + direction * along + Vector3.ProjectOnPlane(bend, direction).normalized * height;
                var bendUpper = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder);
                upper.rotation = Quaternion.Slerp(Quaternion.identity, bendUpper, blend) * upper.rotation;
                var bendLower = Quaternion.FromToRotation(hand.position - forearm.position,
                    shoulder + direction * reach - forearm.position);
                forearm.rotation = Quaternion.Slerp(Quaternion.identity, bendLower, blend) * forearm.rotation;
            }
        }

        internal void Bind(Animator animator)
        {
            path = new NavMeshPath();
            tools = new Tool[stops.Length];
            spine = animator.transform.Find("Skeleton/Root/Hips/Spine");
            if (!spine) return;
            spineAtRest = spine.localRotation;
            left = new Arm(spine, "L");
            right = new Arm(spine, "R");
        }

        internal bool TryDepart(NavMeshAgent agent, out float travelTime)
        {
            if (AtStop) SkipStop();
            travelTime = 0f;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            for (int attempt = 0; attempt < stops.Length; attempt++)
            {
                if (NavMesh.SamplePosition(Current.position, out var stop, 1.2f, filter) &&
                    agent.CalculatePath(stop.position, path) && path.status == NavMeshPathStatus.PathComplete &&
                    agent.SetPath(path))
                {
                    destination = stop.position;
                    float distance = 0f;
                    Vector3 previous = agent.nextPosition;
                    foreach (var corner in path.corners)
                    {
                        distance += Vector3.Distance(previous, corner);
                        previous = corner;
                    }
                    travelTime = Mathf.Max(4f, distance / agent.speed + 4f);
                    if (Current.chore == Chore.Carry) ShowTool();
                    return true;
                }
                SkipStop();
            }
            return false;
        }

        internal bool HasArrived(NavMeshAgent agent)
        {
            float radius = agent.stoppingDistance + .16f;
            return !agent.pathPending && (agent.nextPosition - destination).sqrMagnitude <= radius * radius;
        }

        internal void Arrive()
        {
            AtStop = true;
            poseStarted = Time.time;
            if (!IsRestStop) ShowTool();
        }

        internal void SkipStop()
        {
            Interrupt();
            index = (index + 1) % stops.Length;
        }

        internal void Interrupt()
        {
            AtStop = false;
            showingTool = false;
            if (activeTool != null)
            {
                if (activeTool.particles)
                    activeTool.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                activeTool.model.SetActive(false);
                activeTool = null;
            }
        }

        private void OnDisable() => Interrupt();

        private void ShowTool()
        {
            if (!Current.tool) return;
            if (tools[index] == null)
            {
                var prop = Instantiate(Current.tool, transform);
                prop.name = Current.tool.name;
                tools[index] = new Tool
                {
                    model = prop,
                    leftGrip = prop.transform.Find("Grip L"),
                    rightGrip = prop.transform.Find("Grip R"),
                    emitter = prop.transform.Find("Emitter"),
                    particles = prop.GetComponentInChildren<ParticleSystem>(true)
                };
            }
            activeTool = tools[index];
            activeTool.model.SetActive(true);
            showingTool = true;
            poseStarted = Time.time;
            nextParticle = poseStarted;
        }

        private void LateUpdate()
        {
            if (!showingTool || !spine || Time.deltaTime <= 0f) return;
            float time = Time.time - poseStarted;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time * 4f));
            var prop = activeTool.model.transform;
            switch (Current.chore)
            {
                case Chore.Sweep: Sweep(prop, time); break;
                case Chore.Water: Water(prop, time); break;
                case Chore.Carry: Carry(prop, time); break;
                case Chore.Feed: Feed(prop, time, blend); break;
            }
            if (activeTool.leftGrip)
                left.Reach(activeTool.leftGrip.position, -transform.right - transform.up, blend);
            if (activeTool.rightGrip)
                right.Reach(activeTool.rightGrip.position, transform.right - transform.up, blend);
        }

        private void Sweep(Transform prop, float time)
        {
            float stroke = Mathf.Sin(time * 4.3f);
            Vector3 brush = transform.TransformPoint(new Vector3(.19f + stroke * .24f, .02f, .43f + stroke * .07f));
            if (Physics.Raycast(brush + Vector3.up * .25f, Vector3.down, out var floor, .48f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                brush.y = floor.point.y + .015f;
            Vector3 grip = transform.TransformPoint(new Vector3(-.06f + stroke * .06f, .32f, .13f));
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, grip - brush) * transform.rotation;
            prop.SetPositionAndRotation(brush, rotation);
            if (stroke > .52f) Scatter(brush, transform.right * .55f + Vector3.up * .13f, .30f, 2);
        }

        private void Water(Transform prop, float time)
        {
            float phase = Mathf.Repeat(time / 3.2f, 1f);
            float pour = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(phase * 5f, (1f - phase) * 4f)));
            prop.SetLocalPositionAndRotation(new Vector3(.10f, .24f + pour * .085f, .23f + pour * .04f),
                Quaternion.Euler(7f + pour * 59f, Mathf.Sin(time * 2f) * 12f, -10f));
            if (pour > .68f && activeTool.emitter)
                Scatter(activeTool.emitter.position, activeTool.emitter.forward * .78f, .041f, 3);
        }

        private void Carry(Transform prop, float time)
        {
            float dip = AtStop ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(time, StopDuration - time) * 1.6f)) : 0f;
            float bob = AtStop ? -.10f * dip : Mathf.Sin(time * 7.2f) * .021f;
            prop.SetLocalPositionAndRotation(new Vector3(0, .285f + bob, .19f + dip * .02f),
                Quaternion.Euler(dip * 14f, Mathf.Sin(time * 3.4f) * 3f, 0f));
            var lean = spineAtRest * Quaternion.Euler(24f, 0f, 0f);
            spine.localRotation = Quaternion.Slerp(spine.localRotation, lean, dip * .76f);
        }

        private void Feed(Transform prop, float time, float blend)
        {
            float phase = Mathf.Repeat(time / 2.15f, 1f);
            float toss = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(phase * 3.9f, (1f - phase) * 2.2f)));
            prop.SetLocalPositionAndRotation(new Vector3(-.15f, .20f, .16f), Quaternion.Euler(0, 0, -10f));
            var hand = transform.TransformPoint(Vector3.Lerp(new Vector3(-.07f, .23f, .19f),
                new Vector3(.23f, .42f, .31f), toss));
            right.Reach(hand, transform.right - transform.up, blend);
            if (phase is > .27f and < .43f)
                Scatter(hand, transform.forward * .76f + Vector3.up * .5f, .43f, 8);
        }

        private void Scatter(Vector3 point, Vector3 velocity, float interval, int count)
        {
            if (Time.time < nextParticle || !activeTool.particles) return;
            nextParticle = Time.time + interval;
            if (!activeTool.particles.isPlaying) activeTool.particles.Play();
            for (int i = 0; i < count; i++)
            {
                float spread = i - (count - 1) * .5f;
                var particle = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = Quaternion.AngleAxis(spread * 8f, Vector3.up) * velocity + Vector3.up * (spread * .055f)
                };
                activeTool.particles.Emit(particle, 1);
            }
        }
    }
}
