using UnityEngine;
using Unity.Cinemachine;

namespace Amusnet.World
{
    public sealed class FountainRescue : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Walker
        {
            public Transform actor;
            public Animator animator;
            public Transform fountainSpot;
            [System.NonSerialized] public Transform[] arms;
            [System.NonSerialized] public Transform[] elbows;
            [System.NonSerialized] public Transform[] hands;
            [System.NonSerialized] public Quaternion[] armRest;
            [System.NonSerialized] public Quaternion[] elbowRest;
            [System.NonSerialized] public bool posed;
            [System.NonSerialized] public bool atRim;
            [System.NonSerialized] public Vector3 home;
            [System.NonSerialized] public Quaternion facing;
            [System.NonSerialized] public float gait;
            [System.NonSerialized] public bool dusted;
            [System.NonSerialized] public BeeHorde horde;
            [System.NonSerialized] public Vector3 jumpStart;
            [System.NonSerialized] public Vector3 poolSpot;
            [System.NonSerialized] public float jumpProgress;
        }

        private enum Phase { Ready, Following, ApproachingRim, FallingIn, CallingForHelp, LiftingOut, InFountain, FinishingChase, ClimbingOut, Returning }

        [SerializeField] private ButterflyController butterfly;
        [SerializeField] private ButterflyPollen pollen;
        [SerializeField] private BeeHorde beeHorde;
        [SerializeField] private CinemachineDeoccluder cameraObstacles;
        [SerializeField, Min(1f)] private float rescueTime = 5f;
        [SerializeField] private Terrain terrain;
        [SerializeField] private Walker girl;
        [SerializeField] private Walker[] villagers;
        [SerializeField] private Transform entryRim;
        [SerializeField] private Transform water;
        [SerializeField] private Transform exit;
        [SerializeField] private Transform fountainCenter;
        [SerializeField] private ParticleSystem splash;
        [SerializeField] private GoatLaundryPreview scenarioOne;
        [SerializeField, Tooltip("If false, repeat rescues are available until the goat has escaped through the gate.")]
        private bool replayRequiresGoatEscape;
        [SerializeField, Min(0.5f)] private float interactionRadius = 2.8f;
        [SerializeField, Min(1f)] private float lureRadius = 2.15f;

        private Phase phase;
        private float elapsed;
        private Vector3 motionStart;
        private ParticleSystem pollenTrail;
        private ParticleSystem.Particle[] pollenParticles;
        private int helpersDusted;
        private Walker rescuer;
        private Vector3 liftDestination;
        private bool childHasClimbed;
        private bool restoreCameraObstacles;
        private Texture2D rescueRing;
        private Color32[] ringPixels;
        private byte[] ringSectors;
        private int drawnProgress = -1;
        private const int RingSize = 72;
        private const int RingSteps = 64;
        public float RescueProgress { get; private set; }

        public int HelpersDusted => helpersDusted;
        public bool HelpersInFountain => phase == Phase.InFountain;

        public int RescueCount { get; private set; }
        public bool CanInteract => phase == Phase.Ready && IsReplayAvailable;
        public bool GirlInWater => phase is Phase.CallingForHelp or Phase.LiftingOut ||
            (phase == Phase.InFountain && !childHasClimbed);
        private bool IsReplayAvailable => RescueCount == 0 || replayRequiresGoatEscape == (scenarioOne && scenarioOne.GoatEscaped);

        private void Awake()
        {
            if (pollen)
            {
                pollenTrail = pollen.GetComponent<ParticleSystem>();
                pollenParticles = new ParticleSystem.Particle[pollenTrail.main.maxParticles];
            }
            RememberHome(girl);
            foreach (Walker villager in villagers)
            {
                Vector3 home = villager.actor.position;
                home.y = GroundHeight(home);
                villager.actor.position = home;
                RememberHome(villager);
                CacheArms(villager);
            }
        }

        private static void RememberHome(Walker walker)
        {
            walker.home = walker.actor.position;
            walker.facing = walker.actor.rotation;
            if (walker.animator)
            {
                walker.animator.applyRootMotion = false;
                walker.animator.speed = 0f;
            }
        }

        // In the game the map's preview butterfly is switched off: follow the player's butterfly and pollen.
        private void UsePlayer()
        {
            if (!butterfly || !butterfly.gameObject.activeInHierarchy)
            {
                var live = FindAnyObjectByType<ButterflyController>();
                if (live) butterfly = live;
            }
            if (!cameraObstacles || !cameraObstacles.gameObject.activeInHierarchy)
            {
                var live = FindAnyObjectByType<CinemachineDeoccluder>();
                if (live) cameraObstacles = live;
            }
            if ((!pollen || !pollen.gameObject.activeInHierarchy) && ButterflyPollen.Active)
            {
                pollen = ButterflyPollen.Active;
                pollenTrail = pollen.GetComponent<ParticleSystem>();
                pollenParticles = new ParticleSystem.Particle[pollenTrail.main.maxParticles];
            }
        }

        private void Update()
        {
            UsePlayer();
            if (!pollen) return;
            elapsed += Time.deltaTime;
            girl.gait = 0f;
            foreach (Walker villager in villagers)
                villager.gait = 0f;

            switch (phase)
            {
                case Phase.Ready:
                    if (CanInteract && ButterflyIsNearGirl() && pollen.IsEmitting)
                        SetPhase(Phase.Following);
                    break;
                case Phase.Following:
                    FollowButterfly();
                    break;
                case Phase.ApproachingRim:
                    if (WalkTo(girl, entryRim.position, 2.9f))
                        StartArc(Phase.FallingIn);
                    break;
                case Phase.FallingIn:
                    if (MoveGirlAcross(water.position, .6f, .55f))
                    {
                        if (splash)
                        {
                            splash.Play();
                            splash.Emit(34);
                        }
                        SetPhase(Phase.CallingForHelp);
                    }
                    break;
                case Phase.CallingForHelp:
                    UpdateRescue();
                    break;
                case Phase.LiftingOut:
                    UpdateChasedHelpers();
                    if (MoveGirlAcross(liftDestination, .8f, .8f))
                    {
                        RescueCount++;
                        SetPhase(Phase.FinishingChase);
                    }
                    break;
                case Phase.FinishingChase:
                    ReturnHome(girl);
                    if (UpdateChasedHelpers())
                        SetPhase(Phase.ClimbingOut);
                    break;
                case Phase.InFountain:
                    if (!childHasClimbed)
                    {
                        childHasClimbed = MoveGirlAcross(exit.position, 1.15f, .85f);
                        if (childHasClimbed)
                            RescueCount++;
                    }
                    else
                        ReturnHome(girl);

                    bool beesGone = true;
                    foreach (Walker villager in villagers)
                        beesGone &= !villager.horde || !villager.horde.IsAttacking;
                    if (childHasClimbed && beesGone)
                        SetPhase(Phase.ClimbingOut);
                    break;
                case Phase.ClimbingOut:
                    ReturnHome(girl);
                    float climb = Mathf.Clamp01(elapsed / .85f);
                    foreach (Walker villager in villagers)
                    {
                        if (villager.jumpProgress >= 1f)
                            MoveAcrossRim(villager, villager.poolSpot, villager.jumpStart, climb);
                    }
                    if (climb >= 1f)
                        SetPhase(Phase.Returning);
                    break;
                case Phase.Returning:
                    bool returned = ReturnHome(girl);
                    foreach (Walker villager in villagers)
                        returned &= ReturnHome(villager, true);
                    if (returned)
                    {
                        foreach (Walker villager in villagers)
                        {
                            villager.dusted = false;
                            villager.atRim = false;
                            villager.jumpProgress = 0f;
                            villager.horde = null;
                        }
                        rescuer = null;
                        helpersDusted = 0;
                        childHasClimbed = false;
                        RescueProgress = 0f;
                        SetPhase(Phase.Ready);
                    }
                    break;
            }

            UpdateWalkPlayback(girl);
            foreach (Walker villager in villagers)
                UpdateWalkPlayback(villager);
        }

        private void DustHelpers()
        {
            if (helpersDusted == villagers.Length)
                return;

            int count = pollenTrail.GetParticles(pollenParticles);
            bool caught = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = pollenParticles[i].position;
                if (pollenParticles[i].remainingLifetime <= 0f)
                    continue;

                Walker nearest = null;
                float nearestDistance = .95f * .95f;
                foreach (Walker villager in villagers)
                {
                    float distance = (villager.actor.position + Vector3.up * .9f - point).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearest = villager;
                        nearestDistance = distance;
                    }
                }
                if (nearest == null)
                    continue;

                pollenParticles[i].remainingLifetime = -1f;
                caught = true;
                if (!nearest.dusted)
                {
                    nearest.dusted = true;
                    helpersDusted++;
                    SendBeesAfterHelper(nearest);
                }
            }
            if (caught)
                pollenTrail.SetParticles(pollenParticles, count);
        }

        private void UpdateRescue()
        {
            DustHelpers();
            bool everyDustedHelperWet = UpdateChasedHelpers();
            Walker nearest = null;
            float closest = float.PositiveInfinity;
            foreach (Walker villager in villagers)
            {
                if (villager.dusted)
                    continue;

                villager.atRim = WalkOnGround(villager, villager.fountainSpot.position, 3f);
                if (!villager.atRim)
                    continue;

                Face(villager.actor, HorizontalOffset(girl.actor.position, villager.actor.position));
                float distance = HorizontalOffset(villager.actor.position, girl.actor.position).sqrMagnitude;
                if (distance + .03f < closest)
                {
                    nearest = villager;
                    closest = distance;
                }
            }

            if (helpersDusted == villagers.Length && everyDustedHelperWet)
            {
                StartArc(Phase.InFountain);
                return;
            }

            if (rescuer != nearest)
            {
                rescuer = nearest;
                RescueProgress = 0f;
            }
            if (rescuer == null)
                return;

            Vector3 side = HorizontalOffset(rescuer.fountainSpot.position, fountainCenter.position).normalized;
            Vector3 reachPoint = fountainCenter.position + side * 2.75f;
            reachPoint.y = water.position.y;
            if (!WalkTo(girl, reachPoint, 2.6f))
                return;

            RescueProgress = Mathf.Min(1f, RescueProgress + Time.deltaTime / rescueTime);
            if (RescueProgress >= 1f)
            {
                liftDestination = rescuer.fountainSpot.position;
                StartArc(Phase.LiftingOut);
            }
        }

        private bool UpdateChasedHelpers()
        {
            bool allWet = true;
            foreach (Walker villager in villagers)
            {
                if (!villager.dusted)
                    continue;

                if (!villager.atRim)
                {
                    villager.atRim = WalkOnGround(villager, villager.fountainSpot.position, 4.8f);
                    allWet = false;
                    continue;
                }
                allWet &= JumpIntoFountain(villager);
            }
            return allWet;
        }

        private void SendBeesAfterHelper(Walker villager)
        {
            Vector3 side = HorizontalOffset(villager.fountainSpot.position, fountainCenter.position).normalized;
            Vector3 arrival = villager.actor.position + side * 1.7f + Vector3.up * 1.25f;
            villager.horde = Instantiate(beeHorde, arrival, Quaternion.identity, transform);
            villager.horde.Deploy(arrival, villager.actor);
            villager.jumpStart = villager.fountainSpot.position;
            villager.poolSpot = fountainCenter.position + side * 1.9f;
            villager.poolSpot.y = water.position.y - .18f;
        }

        private bool JumpIntoFountain(Walker villager)
        {
            if (villager.jumpProgress >= 1f)
                return true;
            if (villager.jumpProgress == 0f && villager.horde && !villager.horde.IsAttacking && !villager.horde.IsReturning)
                return false;

            villager.jumpProgress = Mathf.Min(1f, villager.jumpProgress + Time.deltaTime / .95f);
            float t = villager.jumpProgress;
            MoveAcrossRim(villager, villager.jumpStart, villager.poolSpot, t);
            if (t < 1f)
                return false;

            if (splash)
                splash.Emit(new ParticleSystem.EmitParams { position = villager.poolSpot + Vector3.up * .3f }, 24);
            return true;
        }

        private static void MoveAcrossRim(Walker villager, Vector3 from, Vector3 to, float t)
        {
            villager.actor.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t))
                + Vector3.up * (3.4f * t * (1f - t));
            Face(villager.actor, HorizontalOffset(to, from));
            villager.gait = t < 1f ? 1.4f : 0f;
        }

        private void FollowButterfly()
        {
            if (!butterfly || !butterfly.isActiveAndEnabled || (girl.actor.position - butterfly.transform.position).sqrMagnitude > 10f * 10f)
            {
                SetPhase(Phase.Returning);
                return;
            }

            Vector3 player = butterfly.transform.position;
            if (HorizontalOffset(player, fountainCenter.position).sqrMagnitude <= lureRadius * lureRadius &&
                HorizontalOffset(girl.actor.position, entryRim.position).sqrMagnitude <= 4f * 4f)
            {
                SetPhase(Phase.ApproachingRim);
                return;
            }

            Vector3 offset = HorizontalOffset(player, girl.actor.position);
            if (offset.sqrMagnitude <= 1.05f * 1.05f)
                return;
            Vector3 destination = player - offset.normalized * 1.05f;
            Vector3 fromFountain = HorizontalOffset(destination, fountainCenter.position);
            float entryDistance = HorizontalOffset(entryRim.position, fountainCenter.position).magnitude;
            if (fromFountain.sqrMagnitude < entryDistance * entryDistance)
                destination = entryRim.position;

            destination.y = terrain.SampleHeight(destination) + terrain.transform.position.y;
            WalkTo(girl, destination, 2.8f);
        }

        private static void CacheArms(Walker walker)
        {
            walker.arms = new Transform[2];
            walker.elbows = new Transform[2];
            walker.hands = new Transform[2];
            walker.armRest = new Quaternion[2];
            walker.elbowRest = new Quaternion[2];
            foreach (Transform bone in walker.actor.GetComponentsInChildren<Transform>(true))
            {
                int side = bone.name.EndsWith(".L") ? 0 : 1;
                switch (bone.name)
                {
                    case "UpperArm.L": case "UpperArm.R": walker.arms[side] = bone; break;
                    case "Forearm.L": case "Forearm.R": walker.elbows[side] = bone; break;
                    case "Hand.L": case "Hand.R": walker.hands[side] = bone; break;
                }
            }
        }

        private void LateUpdate()
        {
            foreach (Walker villager in villagers)
            {
                bool reaching = !villager.dusted && villager.atRim &&
                    (phase == Phase.CallingForHelp || (phase == Phase.LiftingOut && villager == rescuer));

                if (villager.posed)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (villager.arms[i]) villager.arms[i].localRotation = villager.armRest[i];
                        if (villager.elbows[i]) villager.elbows[i].localRotation = villager.elbowRest[i];
                    }
                }
                if (reaching)
                {
                    Vector3 towardPool = HorizontalOffset(fountainCenter.position, villager.actor.position);
                    villager.actor.rotation = Quaternion.LookRotation(towardPool) * Quaternion.Euler(24f, 0f, 0f);
                    Vector3 towardGirl = HorizontalOffset(girl.actor.position, villager.actor.position).normalized;
                    Vector3 reachDirection = (towardGirl + towardPool.normalized).normalized;
                    for (int i = 0; i < 2; i++)
                    {
                        Transform arm = villager.arms[i], elbow = villager.elbows[i], hand = villager.hands[i];
                        if (!arm || !elbow || !hand)
                            continue;
                        villager.armRest[i] = arm.localRotation;
                        villager.elbowRest[i] = elbow.localRotation;
                        Vector3 goal = arm.position + reachDirection * .85f - Vector3.up * .28f;
                        ReachWithArm(arm, elbow, hand, goal);
                    }
                }
                villager.posed = reaching;
            }
        }

        private static void ReachWithArm(Transform arm, Transform elbow, Transform hand, Vector3 goal)
        {
            float upperLength = Vector3.Distance(arm.position, elbow.position);
            float lowerLength = Vector3.Distance(elbow.position, hand.position);
            if (upperLength < .01f || lowerLength < .01f)
                return;

            Vector3 offset = goal - arm.position;
            Vector3 direction = offset.normalized;
            float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(upperLength - lowerLength) + .001f,
                upperLength + lowerLength - .001f);
            float elbowDepth = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float elbowDrop = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - elbowDepth * elbowDepth));
            Vector3 elbowBend = Vector3.ProjectOnPlane(Vector3.down, direction).normalized;
            Vector3 elbowTarget = arm.position + direction * elbowDepth + elbowBend * elbowDrop;

            AimBone(arm, elbow.position, elbowTarget);
            AimBone(elbow, hand.position, arm.position + direction * distance);
        }

        private static void AimBone(Transform bone, Vector3 child, Vector3 target)
        {
            bone.rotation = Quaternion.FromToRotation(child - bone.position, target - bone.position) * bone.rotation;
        }

        private float GroundHeight(Vector3 point) => terrain.SampleHeight(point) + terrain.transform.position.y + .015f;

        private bool WalkOnGround(Walker walker, Vector3 destination, float speed)
        {
            Vector3 anchored = destination;
            anchored.y = walker.actor.position.y;
            bool arrived = WalkTo(walker, anchored, speed);
            Vector3 position = walker.actor.position;
            position.y = arrived ? destination.y : GroundHeight(position);
            walker.actor.position = position;
            return arrived;
        }

        private void StartArc(Phase next)
        {
            motionStart = girl.actor.position;
            SetPhase(next);
        }

        private bool MoveGirlAcross(Vector3 destination, float duration, float hopHeight)
        {
            float time = Mathf.Clamp01(elapsed / duration);
            float travel = Mathf.SmoothStep(0f, 1f, time);
            girl.actor.position = Vector3.Lerp(motionStart, destination, travel) + Vector3.up * (4f * time * (1f - time) * hopHeight);
            Face(girl.actor, HorizontalOffset(destination, motionStart));
            girl.gait = 1.15f * Mathf.Max(0f, Mathf.Sin(Mathf.PI * time));
            return time >= 1f;
        }

        private static bool WalkTo(Walker walker, Vector3 destination, float speed)
        {
            Vector3 difference = destination - walker.actor.position;
            Face(walker.actor, HorizontalOffset(destination, walker.actor.position));
            walker.actor.position = Vector3.MoveTowards(walker.actor.position, destination, speed * Time.deltaTime);
            walker.gait = Mathf.Min(1.8f, difference.magnitude * 4f);
            return (walker.actor.position - destination).sqrMagnitude < .001f;
        }

        private bool ReturnHome(Walker walker, bool ground = false)
        {
            if (!(ground ? WalkOnGround(walker, walker.home, 3.8f) : WalkTo(walker, walker.home, 2.6f)))
                return false;
            walker.actor.rotation = Quaternion.RotateTowards(walker.actor.rotation, walker.facing, 540f * Time.deltaTime);
            return Quaternion.Angle(walker.actor.rotation, walker.facing) < 1f;
        }

        private static void Face(Transform actor, Vector3 horizontal)
        {
            if (horizontal.sqrMagnitude > .001f)
                actor.rotation = Quaternion.RotateTowards(actor.rotation, Quaternion.LookRotation(horizontal), 540f * Time.deltaTime);
        }

        private static Vector3 HorizontalOffset(Vector3 target, Vector3 from) =>
            Vector3.ProjectOnPlane(target - from, Vector3.up);

        private static void UpdateWalkPlayback(Walker walker)
        {
            if (walker.animator)
                walker.animator.speed = Mathf.MoveTowards(walker.animator.speed, walker.gait, 10f * Time.deltaTime);
        }

        private bool ButterflyIsNearGirl() => butterfly && butterfly.isActiveAndEnabled &&
            (butterfly.transform.position - girl.actor.position - Vector3.up * .7f).sqrMagnitude <= interactionRadius * interactionRadius;

        private void SetPhase(Phase next)
        {
            if (cameraObstacles)
            {
                if (next == Phase.CallingForHelp)
                {
                    restoreCameraObstacles = cameraObstacles.enabled;
                    cameraObstacles.enabled = false;
                }
                else if (next == Phase.Ready || next == Phase.Returning)
                    RestoreCamera();
            }
            phase = next;
            elapsed = 0f;
        }

        private void RestoreCamera()
        {
            if (restoreCameraObstacles && cameraObstacles)
                cameraObstacles.enabled = true;
            restoreCameraObstacles = false;
        }

        private void OnDisable() => RestoreCamera();

        private void OnDestroy()
        {
            if (rescueRing)
                Destroy(rescueRing);
        }

        private void DrawRescueRing()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (!rescueRing)
            {
                rescueRing = new Texture2D(RingSize, RingSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                ringPixels = new Color32[RingSize * RingSize];
                ringSectors = new byte[ringPixels.Length];
                for (int y = 0; y < RingSize; y++)
                    for (int x = 0; x < RingSize; x++)
                    {
                        Vector2 offset = new Vector2(x + .5f, y + .5f) - Vector2.one * (RingSize * .5f);
                        float radius = offset.magnitude;
                        ringSectors[y * RingSize + x] = radius >= 28f && radius <= 34f
                            ? (byte)(1 + Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(offset.x, offset.y), 2f * Mathf.PI) * RingSteps / (2f * Mathf.PI)))
                            : (byte)0;
                    }
            }

            int progress = Mathf.FloorToInt(RescueProgress * RingSteps);
            if (drawnProgress != progress)
            {
                Color32 filled = new(255, 193, 70, 255);
                Color32 empty = new(26, 35, 42, 220);
                for (int i = 0; i < ringPixels.Length; i++)
                    ringPixels[i] = ringSectors[i] == 0 ? default : ringSectors[i] <= progress ? filled : empty;
                rescueRing.SetPixels32(ringPixels);
                rescueRing.Apply(false);
                drawnProgress = progress;
            }
            GUI.DrawTexture(new Rect((Screen.width - RingSize) * .5f, 22f, RingSize, RingSize), rescueRing);
        }

        private void OnGUI()
        {
            if (phase == Phase.CallingForHelp)
                DrawRescueRing();

            string hint = phase switch
            {
                Phase.Ready when ButterflyIsNearGirl() && CanInteract => "Hold Space near the girl to lure her with pollen.",
                Phase.Ready when ButterflyIsNearGirl() && replayRequiresGoatEscape => "Finish the goat’s gate escape to play again.",
                Phase.Ready when ButterflyIsNearGirl() => "The fountain rescue is complete.",
                Phase.Following or Phase.ApproachingRim => "Lead the girl to the fountain.",
                Phase.CallingForHelp => $"Hold Space to stop every helper ({helpersDusted}/{villagers.Length}).",
                Phase.LiftingOut => "A helper reached the girl and is pulling her out.",
                Phase.InFountain => "All helpers stopped. The girl is climbing out.",
                _ => null
            };
            if (hint != null)
                GUI.Box(new Rect(16, Screen.height - 52, Mathf.Min(550, Screen.width - 32), 36), hint);
        }
    }
}
