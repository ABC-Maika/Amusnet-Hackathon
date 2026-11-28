using System.Collections.Generic;
using UnityEngine.AI;
using Amusnet.Mess;
using Butterfly.Death;
using Butterfly.UI;
using UnityEngine;

namespace Amusnet.World
{
    /// <summary>
    /// Night scenario for the "Village Night Campfire" scene (a copy of Village).
    /// A villager tends a campfire on the grassy common, a bucket beside a stool. Fly close to their face
    /// and they get angry and chase you; once you have lured them away from the fire a line of flames
    /// creeps across the ground to the nearest house. Two tasks go on the HUD task board:
    ///   1. "Lure the villager away from the campfire"
    ///   2. "Fire is spreading to the house": the cottage catches fire. The villager runs for the
    ///      bucket; keep them busy (fly in their face) until it has burned, or they put it out.
    /// Scene objects supply the camp, fire emitters, villagers and their door routes.
    /// </summary>
    public sealed class CampfireScenario : MonoBehaviour
    {
        public const string SceneName = "Village Night Campfire";

        enum Phase { Tending, Startled, Chasing, Returning, Embers, Burning, Burned, Saved }

        [Header("Villager")]
        public float faceRadius = 1.8f;
        [Min(0f)] public float sightDistance = 5f;
        [Range(0f, 180f)] public float sightAngle = 120f;
        public float walkSpeed = 2.6f;
        public float chaseSpeed = 2.6f;
        public float bucketRunSpeed = 4.2f;
        [Tooltip("Stop chasing when the butterfly is further than this.")]
        public float loseInterestDistance = 24f;

        [Header("Fire and gust")]
        [Tooltip("The fire counts as unattended when the villager is further than this from it.")]
        public float attendRadius = 6f;
        [Tooltip("Seconds the fire must be unattended before the gust arrives.")]
        public float gustDelay = 6f;
        [UnityEngine.Serialization.FormerlySerializedAs("emberFlightSeconds")]
        public float groundSpreadSeconds = 6f;

        [Header("Cottage fire")]
        public float burnSeconds = 20f;
        public int burnMessPoints = 40;
        [Tooltip("Water needed to put the fire out. The campfire tender contributes more than each helper.")]
        public float douseSeconds = 10f;
        [Tooltip("How long flying in the villager's face stalls their run to the cottage.")]
        public float stallSeconds = 1.2f;
        [Tooltip("Pause before the villager can be stalled again.")]
        public float stallCooldown = 3f;

        const string LureTask = "campfire_lure";
        const string EmberTask = "campfire_embers";

        [System.Serializable]
        sealed class Helper
        {
            public Transform t;
            public Transform interior;
            public Transform exit;
            public Transform bucketStation;
            public Animator anim;
            public float responseDelay;
            [System.NonSerialized] public int animState;
            [System.NonSerialized] public float nextThrow;
            [System.NonSerialized] public bool exited, arrived;
        }

        [Header("Scene objects")]
        [SerializeField] Transform house;
        [SerializeField] Transform camp, villager, head, bucket, flames, spit;
        [SerializeField] Transform home, bucketHome, houseApproach, ignition, roof;
        [SerializeField] Transform groundFire, houseFlameRoot;
        [SerializeField] TextMesh bubble;
        [SerializeField] Animator animator;
        [SerializeField] CampfireChair chair;
        [SerializeField] Light fireLight, houseLight;
        [SerializeField] ParticleSystem houseSmokeFx, waterFx, tearFx;
        [Header("Bucket crew")]
        [SerializeField] List<Helper> helpers = new();
        [Header("Effects and navigation")]
        [SerializeField] List<FlameSpot> groundFlames = new(), houseFlames = new();
        [SerializeField] List<BuildingFootprint> navigation = new();

        [System.Serializable]
        sealed class FlameSpot
        {
            public Transform transform;
            [Range(0f, 1f)] public float ignitionProgress;
            public ParticleSystem[] layers;
            public float[] rates;
            [System.NonSerialized] public Vector3 restScale;
        }

        [System.Serializable]
        sealed class BuildingFootprint
        {
            public Transform building;
            public NavMeshObstacle obstacle;
        }

        readonly List<(Transform building, Bounds footprint)> buildings = new();
        Renderer[] houseRenderers;
        MaterialPropertyBlock charBlock;
        Transform butterfly;
        Camera cam;
        Phase phase;
        float campGrow;
        float nextStallAt, unattended, phaseTime, douse, burned, bubbleUntil, stunUntil;
        bool carryingBucket, lureDone, grieving;
        int animState;
        Vector3 campFlameScale;
        Quaternion campFlameRotation;
        float campLightRange, campLightIntensity, houseLightIntensity, smokeRate;
        const float NpcPad = 0.9f;
        string fallbackLine = "";

        Vector3 firePos => camp.position;
        Vector3 ignitionPoint => ignition.position;
        Vector3 houseTop => roof.position;
        Vector3 houseDoor => houseApproach.position;
        Vector3 bucketSpot => bucketHome.position;

        void Start()
        {
            cam = Camera.main;
            foreach (var entry in navigation)
            {
                if (!entry.building || !entry.obstacle || !entry.obstacle.isActiveAndEnabled) continue;
                var obstacle = entry.obstacle;
                var centre = obstacle.transform.TransformPoint(obstacle.center);
                var size = Vector3.Scale(obstacle.size, obstacle.transform.lossyScale);
                buildings.Add((entry.building, new Bounds(new Vector3(centre.x, 0f, centre.z),
                    new Vector3(size.x + 2f * NpcPad, 1f, size.z + 2f * NpcPad))));
            }
            houseRenderers = house.GetComponentsInChildren<Renderer>();
            charBlock = new MaterialPropertyBlock();
            campFlameScale = flames.localScale;
            campFlameRotation = flames.rotation;
            campLightRange = fireLight.range;
            campLightIntensity = fireLight.intensity;
            houseLightIntensity = houseLight.intensity;
            smokeRate = houseSmokeFx.emission.rateOverTime.constant;
            foreach (var slot in groundFlames) slot.restScale = slot.transform.localScale;
            foreach (var slot in houseFlames) slot.restScale = slot.transform.localScale;
            groundFire.gameObject.SetActive(false);
            houseFlameRoot.gameObject.SetActive(false);
            houseSmokeFx.transform.parent.gameObject.SetActive(false);
            houseLight.gameObject.SetActive(false);
            tearFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var helper in helpers)
            {
                helper.t.position = helper.interior.position;
                HelperPlay(helper, "Watch");
            }
            phase = Phase.Tending;
            chair.Sit();
            PostLureTask();
        }

        // ---------- loop ----------

        void Update()
        {
            if (!villager) return;
            if (GameFlow.Instance != null && !GameFlow.Instance.IsRunning) return;
            if (!butterfly) butterfly = ButterflyLocator.Find();

            float dt = Time.deltaTime;
            phaseTime += dt;
            GrowCampfire(dt);
            if (spit) spit.Rotate(Vector3.right, 38f * dt, Space.Self);
            FlickerFire();
            UpdateBubble();

            bool annoyed = butterfly && Vector3.Distance(butterfly.position, head.position) < faceRadius;
            if (phase == Phase.Burning) TickHouseFire(dt);
            if (phase == Phase.Burning) TickHelpers(dt);
            if (Time.time < stunUntil) { Play("Watch"); return; }

            switch (phase)
            {
                case Phase.Tending: UpdateTending(); break;
                case Phase.Startled: UpdateStartled(); break;
                case Phase.Chasing: UpdateChasing(dt); break;
                case Phase.Returning: UpdateReturning(dt); break;
                case Phase.Embers: UpdateEmbers(dt); break;
                case Phase.Burning: UpdateBurning(dt, annoyed); break;
                case Phase.Burned: UpdateGrieving(dt); break;
            }
            if (phase != Phase.Tending && phase != Phase.Startled) Ground(villager);
            if (phase == Phase.Burned && grieving) villager.position += Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 13f)) * 0.05f); // sobbing
        }

        // In a tree or flower hiding spot (ShelterState on the butterfly).
        bool ButterflyHidden()
        {
            var shelter = butterfly.GetComponentInParent<Amusnet.World.ShelterState>();
            return shelter != null && shelter.IsHidden;
        }

        void UpdateTending()
        {
            if (!butterfly || ButterflyHidden()) return;
            Vector3 offset = butterfly.position - head.position;
            float distance = offset.magnitude;
            if (distance > sightDistance || Vector3.Angle(villager.forward, offset) > sightAngle * 0.5f) return;
            if (distance > 0.01f && Physics.Raycast(head.position, offset / distance, out var hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(butterfly)) return;

            phase = Phase.Startled;
            phaseTime = 0f;
            chair.BeginJump(OutsideBuildings(chair.LandingPoint));
            Say("Shoo, bug!", new Color(1f, 0.35f, 0.25f), 2f);
        }

        void UpdateStartled()
        {
            if (!chair.Jump(phaseTime)) return;
            phase = Phase.Chasing; phaseTime = 0f; unattended = 0f;
            animState = 0;
        }

        void UpdateChasing(float dt)
        {
            if (!butterfly) { phase = Phase.Returning; return; }
            var flat = new Vector3(butterfly.position.x, villager.position.y, butterfly.position.z);
            float far = Vector3.Distance(villager.position, butterfly.position);
            MoveToward(flat, chaseSpeed, dt);

            bool hidden = ButterflyHidden();
            if (far > loseInterestDistance || hidden || phaseTime > 25f)
            {
                Say(hidden ? "Where did it go?" : "Bah!", Color.white, 1.6f);
                phase = Phase.Returning; phaseTime = 0f;
            }
            TrackUnattended(dt);
        }

        void UpdateReturning(float dt)
        {
            bool arrived = MoveToward(home.position, walkSpeed, dt);
            if (arrived)
            {
                chair.Sit();
                phase = Phase.Tending; phaseTime = 0f; unattended = 0f;
                return;
            }
            TrackUnattended(dt);
        }

        // The fire is unattended while the villager is away from it; after a while the gust comes.
        void TrackUnattended(float dt)
        {
            bool away = Vector3.Distance(villager.position, firePos) > attendRadius;
            unattended = away ? unattended + dt : 0f;
            if (away && !lureDone)
            {
                lureDone = true;
                TaskPanel.Complete(LureTask);
                fallbackLine = "";
            }
            if (unattended >= gustDelay) BeginGust();
        }

        // The unattended campfire swells until it throws embers at the nearest village house.
        void GrowCampfire(float dt)
        {
            float target = phase == Phase.Embers || phase == Phase.Burning ? 0.65f : Mathf.Clamp01(unattended / Mathf.Max(0.1f, gustDelay));
            campGrow = Mathf.MoveTowards(campGrow, target, dt * (target > campGrow ? 0.5f : 0.3f));
            if (flames) flames.localScale = campFlameScale * (1f + 0.50f * campGrow);
            if (fireLight) fireLight.range = campLightRange + 10f * campGrow;
        }

        void BeginGust()
        {
            phase = Phase.Embers;
            phaseTime = 0f;
            groundFire.gameObject.SetActive(true);
            Say("The fire!", new Color(1f, 0.8f, 0.3f), 2f);
            Post(EmberTask, $"Fire is spreading to the {PrettyName(house.name)}!", 0, Mathf.CeilToInt(burnSeconds));
        }

        void UpdateEmbers(float dt)
        {
            Vector3 direction = Vector3.ProjectOnPlane(ignitionPoint - firePos, Vector3.up).normalized;
            flames.rotation = Quaternion.Slerp(flames.rotation,
                Quaternion.FromToRotation(Vector3.up, Vector3.up + direction * 0.45f), dt * 3f);
            UpdateGroundFire(phaseTime);
            if (phaseTime >= Mathf.Max(0.1f, groundSpreadSeconds))
            {
                flames.rotation = campFlameRotation;
                StartHouseFire();
            }
        }

        void UpdateGroundFire(float elapsed)
        {
            foreach (var flame in groundFlames)
            {
                float age = elapsed - flame.ignitionProgress * Mathf.Max(0.1f, groundSpreadSeconds);
                float strength = Mathf.SmoothStep(0f, 1f, age / 0.55f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (age - 1.6f) / 2.8f));
                SetFlameStrength(flame, strength, strength);
            }
        }

        void StartHouseFire()
        {
            phase = Phase.Burning; phaseTime = 0f; burned = 0f; douse = 0f; carryingBucket = false; nextStallAt = 0f;
            houseFlameRoot.gameObject.SetActive(true);
            houseSmokeFx.transform.parent.gameObject.SetActive(true);
            houseLight.gameObject.SetActive(true);
            Post(EmberTask, "The house is on fire! Villagers are bringing water", 0, Mathf.CeilToInt(burnSeconds));
        }

        void TickHelpers(float dt)
        {
            float rate = 0f;
            foreach (var h in helpers)
            {
                if (!h.t || phaseTime < h.responseDelay) continue;
                if (!h.exited)
                {
                    // Only the short, authored doorway segment bypasses house avoidance.
                    Vector3 threshold = h.exit.position;
                    threshold.y = h.t.position.y;
                    FaceTarget(h.t, threshold);
                    h.t.position = Vector3.MoveTowards(h.t.position, threshold, walkSpeed * dt);
                    h.exited = Vector3.Distance(h.t.position, threshold) < 0.05f;
                    HelperPlay(h, "Walk");
                }
                else if (!h.arrived)
                {
                    Vector3 target = OutsideBuildings(h.bucketStation.position);
                    Vector3 to = Vector3.ProjectOnPlane(target - h.t.position, Vector3.up);
                    if (to.magnitude < 0.3f) h.arrived = true;
                    else
                    {
                        StepAroundBuildings(h.t, target, bucketRunSpeed * dt);
                        HelperPlay(h, "Walk");
                    }
                }
                else
                {
                    FaceTarget(h.t, houseTop);
                    HelperPlay(h, "Watch");
                    rate += 0.20f;
                    h.nextThrow -= dt;
                    if (h.nextThrow <= 0f) { Throw(h); h.nextThrow = Random.Range(0.9f, 1.4f); }
                }
                Ground(h.t);
            }
            douse += rate * dt;
            if (douse >= douseSeconds) PutOut();
        }

        void Throw(Helper h)
        {
            Vector3 from = h.t.position + Vector3.up * 1.3f + h.t.forward * 0.5f;
            Vector3 target = houseTop + Vector3.down * 0.8f + Random.insideUnitSphere * 0.8f;
            const float T = 0.8f, g = 9.81f;
            Vector3 flat = target - from; float dy = flat.y; flat.y = 0f;
            for (int i = 0; i < 22; i++)
            {
                float t = T * Random.Range(0.92f, 1.08f);
                Vector3 v = flat / t + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
                v.y = (dy + 0.5f * g * t * t) / t;
                waterFx.Emit(new ParticleSystem.EmitParams
                {
                    position = from + Random.insideUnitSphere * 0.15f,
                    velocity = v,
                    startLifetime = t,
                    startSize = Random.Range(0.1f, 0.2f),
                    startColor = new Color(0.55f, 0.75f, 1f)
                }, 1);
            }
        }

        void HelperPlay(Helper h, string pose)
        {
            if (!h.anim || h.anim.runtimeAnimatorController == null) return;
            int state = Animator.StringToHash("Base Layer." + pose);
            h.anim.speed = pose == "Walk" || h.anim.HasState(0, state) ? 1f : 0f;
            if (state == h.animState || !h.anim.HasState(0, state)) return;
            h.anim.CrossFadeInFixedTime(state, 0.15f);
            h.animState = state;
        }

        void StopBucketCrew()
        {
            foreach (var h in helpers) HelperPlay(h, "Watch");
        }

        void UpdateBurning(float dt, bool annoyed)
        {
            if (phase != Phase.Burning) return;

            // Annoying the villager stalls their run to the cottage, but only once in a while,
            // so hovering in their face cannot hold them back forever.
            if (annoyed && phaseTime > 1.5f && Time.time >= nextStallAt)
            {
                stunUntil = Time.time + stallSeconds;
                nextStallAt = stunUntil + stallCooldown;
                Say("Get off!", new Color(1f, 0.35f, 0.25f), 1.2f);
                return;
            }

            // First to the bucket, then to the cottage.
            if (phaseTime > 2f)
            {
                if (!carryingBucket)
                {
                    if (MoveToward(bucketSpot, bucketRunSpeed, dt)) carryingBucket = true;
                }
                else if (MoveToward(houseDoor, bucketRunSpeed, dt))
                {
                    douse += dt;
                    Play("Watch");
                    if (douse >= douseSeconds) { PutOut(); return; }
                }
            }
            else FaceTarget(villager, houseTop);

            if (carryingBucket) bucket.position = villager.position + villager.right * 0.35f + Vector3.up * 0.8f;
        }

        static void SetFlameStrength(FlameSpot flame, float growth, float emissionScale)
        {
            flame.transform.localScale = flame.restScale * Mathf.Lerp(0.35f, 1.36f, growth);
            for (int i = 0; i < flame.layers.Length; i++)
            {
                var emission = flame.layers[i].emission;
                emission.rateOverTime = flame.rates[i] * emissionScale;
            }
        }

        // The cottage fire: grows and counts down on its own clock, independent of the villager and the butterfly.
        void TickHouseFire(float dt)
        {
            burned += dt;
            var intensity = Mathf.Clamp01(burned / burnSeconds);
            float dousedFactor = 1f - 0.7f * Mathf.Clamp01(douse / douseSeconds);
            foreach (var flame in houseFlames)
            {
                float growth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((burned - burnSeconds * flame.ignitionProgress) / Mathf.Max(0.1f, burnSeconds * 0.13f)));
                float flicker = Mathf.Lerp(0.75f, 1.25f, Mathf.PerlinNoise(flame.ignitionProgress * 20f, burned * 5f));
                SetFlameStrength(flame, growth, growth * flicker * dousedFactor);
            }
            if (groundFire.gameObject.activeSelf)
            {
                UpdateGroundFire(Mathf.Max(0.1f, groundSpreadSeconds) + burned);
                if (burned >= 8f) ClearGroundFire();
            }
            houseSmokeFx.transform.position = Vector3.Lerp(ignitionPoint, houseTop, intensity);
            var smoke = houseSmokeFx.emission;
            smoke.rateOverTime = smokeRate * Mathf.Lerp(0.4f, 4.2f, intensity) * dousedFactor;
            houseLight.transform.position = Vector3.Lerp(ignitionPoint, houseTop, intensity) + Vector3.up;
            houseLight.intensity = (houseLightIntensity + 17f * intensity) * dousedFactor * Mathf.Lerp(0.8f, 1.2f, Mathf.PerlinNoise(burned * 9f, 1f));
            TintHouse(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.95f, intensity)));
            TaskPanel.Progress(EmberTask, Mathf.FloorToInt(burned));
            fallbackLine = $"The cottage is burning {Mathf.FloorToInt(burned)}/{Mathf.CeilToInt(burnSeconds)}";
            if (burned >= burnSeconds) Burn();
        }

        void ClearGroundFire()
        {
            groundFire.gameObject.SetActive(false);
        }

        void PutOut()
        {
            ClearGroundFire();
            Say("Phew!", Color.white, 2f);
            TaskPanel.Complete(EmberTask);
            fallbackLine = "";
            StopBucketCrew();
            houseFlameRoot.gameObject.SetActive(false);
            houseSmokeFx.transform.parent.gameObject.SetActive(false);
            houseLight.gameObject.SetActive(false);
            if (carryingBucket) { bucket.position = bucketSpot + Vector3.up * 0.22f; carryingBucket = false; }
            // The scenario ends here.
            phase = Phase.Saved; phaseTime = 0f;
        }

        // After the mission: the villager walks to his cottage, stands beside it and cries.

        void UpdateGrieving(float dt)
        {
            Vector3 side = Vector3.Cross(Vector3.up, (houseDoor - houseTop).normalized);
            Vector3 spot = Snap(houseDoor + side * 1.4f);
            if (!MoveToward(spot, 2.4f, dt)) return;
            FaceTarget(villager, houseTop);
            Play("Watch");
            if (grieving) { if (Time.time > bubbleUntil - 1f) Say("MY HOUSE", new Color(1f, 0.85f, 0.4f), 5f); return; }
            grieving = true;
            Say("MY HOUSE", new Color(1f, 0.85f, 0.4f), 5f);
            tearFx.Play();
        }

        void LateUpdate()
        {
            if (!enabled || buildings.Count == 0) return;
            if (Time.time >= nextAgentScan)
            {
                nextAgentScan = Time.time + 2f;
                agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            }
            foreach (var agent in agents)
            {
                if (!agent || agent.gameObject.scene != gameObject.scene || !agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;
                Vector3 position = agent.transform.position;
                Vector3 outside = OutsideBuildings(position);
                if ((outside - position).sqrMagnitude < 0.0001f) continue;
                if (NavMesh.SamplePosition(outside, out var hit, 0.5f, agent.areaMask) &&
                    (OutsideBuildings(hit.position) - hit.position).sqrMagnitude < 0.0001f)
                    agent.Warp(hit.position);
            }
        }

        float nextAgentScan;
        NavMeshAgent[] agents = System.Array.Empty<NavMeshAgent>();

        Vector3 OutsideBuildings(Vector3 point)
        {
            foreach (var entry in buildings)
            {
                Bounds box = entry.footprint;
                if (point.x <= box.min.x || point.x >= box.max.x || point.z <= box.min.z || point.z >= box.max.z) continue;
                float x = point.x < box.center.x ? box.min.x - 0.08f : box.max.x + 0.08f;
                float z = point.z < box.center.z ? box.min.z - 0.08f : box.max.z + 0.08f;
                if (Mathf.Abs(point.x - x) < Mathf.Abs(point.z - z)) point.x = x;
                else point.z = z;
            }
            return point;
        }

        bool SegmentClear(Vector3 from, Vector3 to)
        {
            Vector3 move = to - from;
            move.y = 0f;
            if (move.sqrMagnitude < 0.000001f) return true;
            from.y = 0f;
            var ray = new Ray(from, move);
            foreach (var entry in buildings)
                if (entry.footprint.IntersectRay(ray, out float distance) && distance < move.magnitude)
                    return false;
            return true;
        }

        readonly Dictionary<Transform, (Vector3 target, Vector3 waypoint)> detours = new();

        void StepAroundBuildings(Transform actor, Vector3 target, float step)
        {
            Vector3 from = OutsideBuildings(actor.position);
            target = OutsideBuildings(target);
            target.y = from.y;
            Vector3 waypoint = target;
            if (SegmentClear(from, target))
                detours.Remove(actor);
            else if (detours.TryGetValue(actor, out var detour) &&
                Vector3.ProjectOnPlane(detour.target - target, Vector3.up).sqrMagnitude < 2.25f &&
                Vector3.ProjectOnPlane(detour.waypoint - from, Vector3.up).sqrMagnitude > 0.0025f &&
                SegmentClear(from, detour.waypoint))
                waypoint = detour.waypoint;
            else
            {
                waypoint = FindDetour(from, target);
                detours[actor] = (target, waypoint);
            }
            waypoint.y = from.y;
            FaceTarget(actor, waypoint);
            actor.position = Vector3.MoveTowards(from, waypoint, step);
        }

        Vector3 FindDetour(Vector3 from, Vector3 target)
        {
            // Find the shortest clear route through the corners of the building footprints.
            var points = new List<Vector3> { from, target };
            foreach (var entry in buildings)
            {
                var box = entry.footprint;
                for (int i = 0; i < 4; i++)
                {
                    var corner = new Vector3(i < 2 ? box.min.x - 0.12f : box.max.x + 0.12f, from.y,
                        i % 2 == 0 ? box.min.z - 0.12f : box.max.z + 0.12f);
                    if ((OutsideBuildings(corner) - corner).sqrMagnitude < 0.0001f)
                        points.Add(corner);
                }
            }

            var distance = new float[points.Count];
            var previous = new int[points.Count];
            var visited = new bool[points.Count];
            System.Array.Fill(distance, float.PositiveInfinity);
            distance[0] = 0f;
            for (int step = 0; step < points.Count; step++)
            {
                int current = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < points.Count; i++)
                {
                    if (visited[i] || distance[i] >= best) continue;
                    current = i;
                    best = distance[i];
                }
                if (current < 0) break;
                if (current == 1)
                {
                    while (previous[current] != 0) current = previous[current];
                    return points[current];
                }
                visited[current] = true;
                for (int i = 1; i < points.Count; i++)
                {
                    if (visited[i]) continue;
                    float score = best + Vector3.Distance(points[current], points[i]);
                    if (score >= distance[i] || !SegmentClear(points[current], points[i])) continue;
                    distance[i] = score;
                    previous[i] = current;
                }
            }
            return from;
        }

        void Burn()
        {
            ClearGroundFire();
            phase = Phase.Burned;
            Say("Noooo!", new Color(1f, 0.35f, 0.25f), 4f);
            TaskPanel.Complete(EmberTask);
            fallbackLine = "";
            var mess = FindFirstObjectByType<MessManager>();
            if (mess) mess.AddMess(burnMessPoints);
            TintHouse(1f);
            carryingBucket = false;
            StopBucketCrew();
            StartCoroutine(FinishBurn());
        }

        void TintHouse(float amount)
        {
            foreach (var renderer in houseRenderers)
            {
                var material = renderer.sharedMaterial;
                if (!material) continue;
                string color = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                if (!material.HasProperty(color)) continue;
                renderer.GetPropertyBlock(charBlock);
                charBlock.SetColor(color, Color.Lerp(material.GetColor(color), new Color(0.11f, 0.08f, 0.06f), amount));
                renderer.SetPropertyBlock(charBlock);
            }
        }

        System.Collections.IEnumerator FinishBurn()
        {
            yield return new WaitForSeconds(1.1f);
            if (house)
            {
                buildings.RemoveAll(entry => entry.building == house);
                foreach (var entry in navigation)
                    if (entry.building == house && entry.obstacle) entry.obstacle.gameObject.SetActive(false);
                house.gameObject.SetActive(false);
            }

            foreach (var flame in houseFlames)
                foreach (var layer in flame.layers)
                    layer.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            houseSmokeFx.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            StartCoroutine(HideAfter(houseFlameRoot.gameObject, 4f));
            StartCoroutine(HideAfter(houseSmokeFx.transform.parent.gameObject, 7f));
            for (float elapsed = 0f; elapsed < 2f; elapsed += Time.deltaTime)
            {
                if (houseLight) houseLight.intensity *= Mathf.Exp(-2.5f * Time.deltaTime);
                yield return null;
            }
            houseLight.gameObject.SetActive(false);
        }

        static System.Collections.IEnumerator HideAfter(GameObject effect, float delay)
        {
            yield return new WaitForSeconds(delay);
            effect.SetActive(false);
        }

        // ---------- villager helpers ----------

        bool MoveToward(Vector3 target, float speed, float dt)
        {
            target = OutsideBuildings(target);
            Vector3 to = target - villager.position; to.y = 0f;
            if (to.magnitude < 0.25f) { Play("Watch"); return true; }
            StepAroundBuildings(villager, target, speed * dt);
            Play("Walk");
            return false;
        }

        static void FaceTarget(Transform t, Vector3 target)
        {
            Vector3 d = target - t.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
        }

        void Play(string pose)
        {
            if (!animator || animator.runtimeAnimatorController == null) return;
            int state = Animator.StringToHash("Base Layer." + pose);
            animator.speed = pose == "Walk" || animator.HasState(0, state) ? 1f : 0f;
            if (state == animState || !animator.HasState(0, state)) return;
            animator.CrossFadeInFixedTime(state, 0.15f);
            animState = state;
        }

        void Say(string text, Color color, float seconds)
        {
            if (!bubble) return;
            bubble.text = text; bubble.color = color;
            bubbleUntil = Time.time + seconds;
        }

        void UpdateBubble()
        {
            if (!bubble) return;
            if (Time.time > bubbleUntil) bubble.text = "";
            if (cam) bubble.transform.rotation = cam.transform.rotation;
        }

        void FlickerFire()
        {
            if (fireLight) fireLight.intensity = campLightIntensity + 6f * campGrow + Mathf.PerlinNoise(Time.time * 9f, 0f) * 3f - 1.5f;
        }

        // ---------- task board ----------

        void PostLureTask() => Post(LureTask, "Lure the villager away from the campfire", 0, 1);

        void Post(string id, string text, int progress, int target)
        {
            TaskPanel.Set(id, text, progress, target);
            fallbackLine = TaskPanel.Instance == null ? text : "";
        }

        // Standalone runs (no HUD in the scene) still show the objective.
        void OnGUI()
        {
            if (TaskPanel.Instance != null || string.IsNullOrEmpty(fallbackLine)) return;
            GUI.Box(new Rect(16f, 16f, 420f, 30f), "Task: " + fallbackLine);
        }

        // ---------- world helpers ----------

        static Vector3 Snap(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t) p.y = t.SampleHeight(p) + t.transform.position.y;
            else if (Physics.Raycast(p + Vector3.up * 100f, Vector3.down, out var hit, 300f)) p.y = hit.point.y;
            return p;
        }

        static void Ground(Transform t) => t.position = Snap(t.position);

        static string PrettyName(string n) => n.Replace("(Clone)", "").Trim().ToLowerInvariant();

    }
}
