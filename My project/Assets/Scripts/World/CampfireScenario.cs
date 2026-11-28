using Amusnet.Mess;
using Butterfly.Death;
using Butterfly.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Amusnet.World
{
    /// <summary>
    /// Night scenario for the "Village Night Campfire" scene (a copy of Village).
    /// A villager tends a campfire on the grassy common, a bucket beside a stool. Fly close to their face
    /// and they get angry and chase you; once you have lured them away from the fire a scripted gust
    /// carries glowing embers to the nearest cottage. Two tasks go on the HUD task board:
    ///   1. "Lure the villager away from the campfire"
    ///   2. "Embers are blowing toward the cottage": the cottage catches fire. The villager runs for the
    ///      bucket; keep them busy (fly in their face) until it has burned, or they put it out.
    /// Burning the cottage down awards mess. Everything is built from primitives and particles at Start, and
    /// the component installs itself into the scene named <see cref="SceneName"/>, so there is nothing to place.
    /// </summary>
    public sealed class CampfireScenario : MonoBehaviour
    {
        public const string SceneName = "Village Night Campfire";

        enum Phase { Tending, Chasing, Returning, Embers, Burning, Burned }

        [Header("Villager")]
        public float faceRadius = 1.8f;
        [Tooltip("Seconds in the villager's face before they lose their temper.")]
        public float annoySeconds = 1.5f;
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
        public float emberFlightSeconds = 5f;
        public float gustSeconds = 2.2f;

        [Header("Cottage fire")]
        public float burnSeconds = 20f;
        public int burnMessPoints = 40;
        [Tooltip("Total bucket-throwing seconds (summed over all villagers at the cottage) needed to put the fire out.")]
        public float douseSeconds = 10f;
        [Tooltip("Villagers who run in with buckets once the cottage is burning.")]
        public int helperCount = 3;
        [Tooltip("How long flying in the villager's face stalls their run to the cottage.")]
        public float stallSeconds = 1.2f;
        [Tooltip("Pause before the villager can be stalled again.")]
        public float stallCooldown = 3f;

        const string LureTask = "campfire_lure";
        const string EmberTask = "campfire_embers";

        sealed class Helper
        {
            public Transform t, bucket;
            public Animator anim;
            public int animState;
            public Vector3 spot;
            public float nextThrow;
            public bool arrived;
        }

        readonly System.Collections.Generic.List<Helper> helpers = new System.Collections.Generic.List<Helper>();
        ParticleSystem waterFx;
        float campGrow;
        Phase phase;
        Transform butterfly, villager, head, bucket, stool, flames, bubbleT;
        Animator animator;
        TextMesh bubble;
        Light fireLight, houseLight;
        ParticleSystem flameFx, emberFx, houseFlameFx, houseSmokeFx;
        Transform house;
        Vector3 firePos, houseTop, stoolSpot, bucketSpot, houseDoor;
        float headHeight = 1.7f;
        float nextStallAt, emberDebt, annoy, unattended, phaseTime, douse, burned, bubbleUntil, stunUntil, gustLeft;
        bool carryingBucket, lureDone;
        int animState;
        Material glowMat;
        Camera cam;
        string fallbackLine = "";

        // ---------- install into the copied scene ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(SceneManager.GetActiveScene());
            for (int i = 0; i < SceneManager.sceneCount; i++) TryInstall(SceneManager.GetSceneAt(i));
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TryInstall(s);

        static void TryInstall(Scene s)
        {
            if (!s.isLoaded || s.name != SceneName || FindFirstObjectByType<CampfireScenario>() != null) return;
            var go = new GameObject("Night Campfire");
            SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<CampfireScenario>();
        }

        // ---------- setup ----------

        void Start()
        {
            cam = Camera.main;
            FreezeAtNight();

            var common = GameObject.Find("Grassy common");
            Vector3 centre = common ? common.transform.position : (ButterflyLocator.Find() ? ButterflyLocator.Find().position : Vector3.zero);
            firePos = Snap(centre);
            transform.position = firePos;

            house = FindNearestCottage(firePos);
            if (house)
            {
                var b = BoundsOf(house);
                houseTop = new Vector3(b.center.x, b.max.y, b.center.z);
                Vector3 toFire = firePos - b.center; toFire.y = 0f;
                houseDoor = Snap(b.center + toFire.normalized * (Mathf.Max(b.extents.x, b.extents.z) + 1.2f));
            }
            else
            {
                // No cottage found: fall back to a spot 25 m away so the scenario still runs.
                houseTop = firePos + new Vector3(25f, 4f, 0f);
                houseDoor = Snap(firePos + new Vector3(23f, 0f, 0f));
            }

            glowMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftTexture() };
            BuildCamp();
            BuildVillager();
            phase = Phase.Tending;
            PostLureTask();
        }

        // The weather cycles through day and rain: hold it at a dry night for this scenario.
        void FreezeAtNight()
        {
            var weather = FindFirstObjectByType<WeatherSystem>();
            if (!weather) return;
            weather.startDay = 0.75f;
            weather.dayLength = 1000000f;
            weather.weatherPeriod = 1000000f;
        }

        void BuildCamp()
        {
            var camp = new GameObject("Campfire").transform;
            camp.SetParent(transform, false);
            camp.position = firePos;

            Color stone = new Color(0.42f, 0.42f, 0.44f), wood = new Color(0.32f, 0.2f, 0.1f);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                var s = Part(camp, PrimitiveType.Sphere, firePos + new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * 0.75f, new Vector3(0.38f, 0.26f, 0.34f), stone);
                s.rotation = Quaternion.Euler(0f, a * 57f, 0f);
            }
            for (int i = 0; i < 3; i++)
            {
                var log = Part(camp, PrimitiveType.Cylinder, firePos + Vector3.up * 0.18f, new Vector3(0.14f, 0.55f, 0.14f), wood);
                log.rotation = Quaternion.Euler(78f, i * 120f, 0f);
            }

            flames = new GameObject("Flames").transform;
            flames.SetParent(camp, false);
            flames.position = firePos + Vector3.up * 0.25f;
            flameFx = MakeFlames(flames, 1f);
            emberFx = MakeEmbers(flames);

            fireLight = new GameObject("Fire Light").AddComponent<Light>();
            fireLight.transform.SetParent(camp, false);
            fireLight.transform.position = firePos + Vector3.up * 1.1f;
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.58f, 0.22f);
            fireLight.range = 16f;
            fireLight.intensity = 6f;

            // Stool and bucket beside it.
            stoolSpot = Snap(firePos + new Vector3(2.1f, 0f, -0.4f));
            stool = Part(camp, PrimitiveType.Cylinder, stoolSpot + Vector3.up * 0.28f, new Vector3(0.55f, 0.28f, 0.55f), new Color(0.5f, 0.33f, 0.18f));
            bucketSpot = Snap(stoolSpot + new Vector3(0.85f, 0f, 0.35f));
            bucket = Part(camp, PrimitiveType.Cylinder, bucketSpot + Vector3.up * 0.22f, new Vector3(0.36f, 0.22f, 0.36f), new Color(0.45f, 0.5f, 0.58f));
        }

        GameObject MakeModel(string modelName)
        {
            GameObject model = null;
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Prefabs/Red Shirt.prefab");
            if (prefab) model = Instantiate(prefab);
#endif
            if (model == null)
            {
                model = new GameObject("Villager");
                Part(model.transform, PrimitiveType.Capsule, Vector3.up * 0.85f, new Vector3(0.6f, 0.85f, 0.6f), new Color(0.75f, 0.2f, 0.2f)).localPosition = Vector3.up * 0.85f;
                Part(model.transform, PrimitiveType.Sphere, Vector3.up * 1.75f, Vector3.one * 0.42f, new Color(0.95f, 0.78f, 0.62f)).localPosition = Vector3.up * 1.75f;
            }
            model.name = modelName;
            model.transform.SetParent(transform, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            return model;
        }

        void BuildVillager()
        {
            GameObject model = MakeModel("Campfire Villager");
            model.transform.SetParent(transform, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            villager = model.transform;
            villager.position = stoolSpot + new Vector3(-0.2f, 0f, -0.9f);
            animator = model.GetComponentInChildren<Animator>();

            var b = BoundsOf(villager);
            if (b.size.y > 0.5f) headHeight = b.max.y - villager.position.y;
            head = new GameObject("Head").transform;
            head.SetParent(villager, false);
            head.position = villager.position + Vector3.up * headHeight * 0.95f;

            bubbleT = new GameObject("Bubble").transform;
            bubbleT.SetParent(villager, false);
            bubbleT.position = villager.position + Vector3.up * (headHeight + 0.45f);
            bubble = bubbleT.gameObject.AddComponent<TextMesh>();
            bubble.anchor = TextAnchor.MiddleCenter;
            bubble.characterSize = 0.12f;
            bubble.fontSize = 48;
            bubble.text = "";
            FaceTarget(villager, firePos);
            Play("Watch");
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
            FlickerFire();
            UpdateBubble();

            bool annoyed = butterfly && Vector3.Distance(butterfly.position, head.position) < faceRadius;
            if (phase == Phase.Burning) { TickHouseFire(dt); TickHelpers(dt); } // the fire burns whatever the villager is doing
            if (Time.time < stunUntil) { Play("Watch"); return; }

            switch (phase)
            {
                case Phase.Tending: UpdateTending(dt, annoyed); break;
                case Phase.Chasing: UpdateChasing(dt); break;
                case Phase.Returning: UpdateReturning(dt); break;
                case Phase.Embers: UpdateEmbers(dt); break;
                case Phase.Burning: UpdateBurning(dt, annoyed); break;
            }
            Ground(villager);
        }

        void UpdateTending(float dt, bool annoyed)
        {
            FaceTarget(villager, firePos);
            Play("Watch");
            annoy = annoyed ? annoy + dt : Mathf.Max(0f, annoy - dt * 0.6f);
            if (annoyed && annoy > annoySeconds * 0.4f && Time.time > bubbleUntil) Say("Hmm?", Color.white, 1f);
            if (annoy >= annoySeconds) BeginChase();
        }

        void BeginChase()
        {
            phase = Phase.Chasing; phaseTime = 0f; annoy = 0f; unattended = 0f;
            Say("Shoo, bug!", new Color(1f, 0.35f, 0.25f), 2f);
        }

        void UpdateChasing(float dt)
        {
            if (!butterfly) { phase = Phase.Returning; return; }
            var flat = new Vector3(butterfly.position.x, villager.position.y, butterfly.position.z);
            float far = Vector3.Distance(villager.position, butterfly.position);
            MoveToward(flat, chaseSpeed, dt);

            bool hidden = HidingZone.IsHidden(butterfly.position);
            if (far > loseInterestDistance || hidden || phaseTime > 25f)
            {
                Say(hidden ? "Where did it go?" : "Bah!", Color.white, 1.6f);
                phase = Phase.Returning; phaseTime = 0f;
            }
            TrackUnattended(dt);
        }

        void UpdateReturning(float dt)
        {
            Vector3 home = stoolSpot + new Vector3(-0.2f, 0f, -0.9f);
            bool arrived = MoveToward(home, walkSpeed, dt);
            if (arrived) { phase = Phase.Tending; phaseTime = 0f; unattended = 0f; return; }
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

        // The unattended campfire swells until it throws embers at the nearest cottage.
        void GrowCampfire(float dt)
        {
            float target = phase >= Phase.Embers ? 1f : Mathf.Clamp01(unattended / Mathf.Max(0.1f, gustDelay));
            campGrow = Mathf.MoveTowards(campGrow, target, dt * (target > campGrow ? 0.5f : 0.3f));
            if (flames && phase != Phase.Embers) flames.localScale = Vector3.one * (1f + 1.6f * campGrow);
            else if (flames) flames.localScale = Vector3.one * 2.6f;
            if (fireLight) fireLight.range = 16f + 10f * campGrow;
        }

        void BeginGust()
        {
            phase = Phase.Embers; phaseTime = 0f; gustLeft = gustSeconds;
            string name = house ? PrettyName(house.name) : "cottage";
            Say("The fire!", new Color(1f, 0.8f, 0.3f), 2f);
            Post(EmberTask, $"The campfire is spreading to the {name}!", 0, Mathf.CeilToInt(burnSeconds));
        }

        void UpdateEmbers(float dt)
        {
            // Villager keeps chasing the butterfly (or stands by the fire) while the gust blows.
            Vector3 dir = houseTop - firePos; dir.y = 0f;
            flames.rotation = Quaternion.Slerp(flames.rotation, Quaternion.FromToRotation(Vector3.up, Vector3.up + dir.normalized * 0.9f), 4f * dt);

            if (gustLeft > 0f)
            {
                gustLeft -= dt;
                EmitEmbers(dt);
            }

            if (phaseTime >= gustSeconds + emberFlightSeconds)
            {
                flames.rotation = Quaternion.identity;
                StartHouseFire();
            }
        }

        void EmitEmbers(float dt)
        {
            float T = emberFlightSeconds;
            float g = 9.81f * 0.15f;
            Vector3 flat = houseTop - (firePos + Vector3.up * 0.5f);
            float dy = flat.y; flat.y = 0f;
            emberDebt += 28f * dt;
            int count = Mathf.FloorToInt(emberDebt);
            emberDebt -= count;
            for (int i = 0; i < count; i++)
            {
                float t = T * Random.Range(0.92f, 1.08f);
                Vector3 v = flat / t;
                v += new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                v.y = (dy + 0.5f * g * t * t) / t;
                var p = new ParticleSystem.EmitParams
                {
                    position = firePos + Vector3.up * 0.6f + Random.insideUnitSphere * 0.25f,
                    velocity = v,
                    startLifetime = t,
                    startSize = Random.Range(0.1f, 0.22f),
                    startColor = Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.35f, 0.1f), Random.value)
                };
                emberFx.Emit(p, 1);
            }
        }

        void StartHouseFire()
        {
            phase = Phase.Burning; phaseTime = 0f; burned = 0f; douse = 0f; carryingBucket = false; nextStallAt = 0f;
            houseFlameFx = MakeFlames(new GameObject("Cottage Flames").transform, 1.6f);
            houseFlameFx.transform.position = houseTop + Vector3.down * 0.6f;
            houseSmokeFx = MakeSmoke(new GameObject("Cottage Smoke").transform);
            houseSmokeFx.transform.position = houseTop;
            houseLight = new GameObject("Cottage Fire Light").AddComponent<Light>();
            houseLight.transform.position = houseTop + Vector3.up * 1.2f;
            houseLight.type = LightType.Point;
            houseLight.color = new Color(1f, 0.5f, 0.2f);
            houseLight.range = 20f;
            houseLight.intensity = 8f;
            Say("My cottage!", new Color(1f, 0.35f, 0.25f), 2.5f);
            Post(EmberTask, "The cottage is on fire! Villagers are bringing water", 0, Mathf.CeilToInt(burnSeconds));
            SpawnHelpers();
        }

        // ---------- bucket brigade ----------

        void SpawnHelpers()
        {
            if (waterFx == null) waterFx = MakeWater();
            Vector3 away = firePos - houseDoor; away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.right;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            Vector3 toHouse = houseTop - houseDoor; toHouse.y = 0f;
            for (int i = 0; i < helperCount; i++)
            {
                float k = i - (helperCount - 1) * 0.5f;
                var h = new Helper();
                var model = MakeModel("Bucket Villager " + (i + 1));
                h.t = model.transform;
                h.anim = model.GetComponentInChildren<Animator>();
                h.t.position = Snap(firePos + away * (6f + i * 1.2f) + side * (k * 2.5f));
                h.spot = Snap(houseDoor + side * (k * 1.8f) + away * Random.Range(0f, 1.5f));
                h.nextThrow = Random.Range(0.2f, 0.9f);
                h.bucket = Part(h.t, PrimitiveType.Cylinder, h.t.position + Vector3.up, new Vector3(0.3f, 0.18f, 0.3f), new Color(0.45f, 0.5f, 0.58f));
                HelperPlay(h, "Walk");
                helpers.Add(h);
            }
        }

        void TickHelpers(float dt)
        {
            float rate = 0f;
            foreach (var h in helpers)
            {
                if (!h.t) continue;
                if (!h.arrived)
                {
                    Vector3 to = h.spot - h.t.position; to.y = 0f;
                    if (to.magnitude < 0.3f) h.arrived = true;
                    else
                    {
                        FaceTarget(h.t, h.spot);
                        h.t.position += to.normalized * (bucketRunSpeed * dt);
                        HelperPlay(h, "Walk");
                    }
                }
                else
                {
                    FaceTarget(h.t, houseTop);
                    HelperPlay(h, "Watch");
                    rate += 1f;
                    h.nextThrow -= dt;
                    if (h.nextThrow <= 0f) { Throw(h); h.nextThrow = Random.Range(0.9f, 1.4f); }
                }
                Ground(h.t);
                if (h.bucket) h.bucket.position = h.t.position + h.t.forward * 0.4f + h.t.right * 0.25f + Vector3.up * 0.9f;
            }
            if (carryingBucket && Vector3.Distance(villager.position, houseDoor) < 0.6f) rate += 1f;
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
            if (state == h.animState || !h.anim.HasState(0, state)) return;
            h.anim.CrossFadeInFixedTime(state, 0.15f);
            h.animState = state;
        }

        void ClearHelpers()
        {
            foreach (var h in helpers) if (h.t) Destroy(h.t.gameObject, 2.5f);
            helpers.Clear();
        }

        ParticleSystem MakeWater()
        {
            var go = new GameObject("Water");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 0.8f;
            main.startSize = 0.15f;
            main.gravityModifier = 1f;
            main.maxParticles = 800;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.enabled = false;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;
            return ps;
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

        // The cottage fire: grows and counts down on its own clock, independent of the villager and the butterfly.
        void TickHouseFire(float dt)
        {
            burned += dt;
            var intensity = Mathf.Clamp01(burned / burnSeconds);
            houseFlameFx.transform.localScale = Vector3.one * ((1f + intensity) * (1f - 0.7f * Mathf.Clamp01(douse / douseSeconds)));
            houseLight.intensity = 8f + 6f * intensity + Random.Range(-1.2f, 1.2f);
            TaskPanel.Progress(EmberTask, Mathf.FloorToInt(burned));
            fallbackLine = $"The cottage is burning {Mathf.FloorToInt(burned)}/{Mathf.CeilToInt(burnSeconds)}";
            if (burned >= burnSeconds) Burn();
        }

        void PutOut()
        {
            Say("Phew!", Color.white, 2f);
            TaskPanel.Remove(EmberTask);
            ClearHelpers();
            Destroy(houseFlameFx.gameObject); Destroy(houseSmokeFx.gameObject); Destroy(houseLight.gameObject);
            if (carryingBucket) { bucket.position = bucketSpot + Vector3.up * 0.22f; carryingBucket = false; }
            // The scenario resets: back to the fire, ready for another gust.
            lureDone = false; unattended = 0f;
            phase = Phase.Returning; phaseTime = 0f;
            PostLureTask();
        }

        void Burn()
        {
            phase = Phase.Burned;
            Say("Noooo!", new Color(1f, 0.35f, 0.25f), 4f);
            TaskPanel.Complete(EmberTask);
            fallbackLine = "";
            var mess = FindFirstObjectByType<MessManager>();
            if (mess) mess.AddMess(burnMessPoints);
            if (house)
                foreach (var r in house.GetComponentsInChildren<Renderer>())
                {
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.09f));
                    block.SetColor("_Color", new Color(0.12f, 0.1f, 0.09f));
                    r.SetPropertyBlock(block);
                }
            carryingBucket = false;
            StartCoroutine(StareAtRuins());
        }

        System.Collections.IEnumerator StareAtRuins()
        {
            // Villager stands and stares at the ruined cottage.
            float t = 0f;
            while (t < 5f) { t += Time.deltaTime; FaceTarget(villager, houseTop); yield return null; }
        }

        // ---------- villager helpers ----------

        bool MoveToward(Vector3 target, float speed, float dt)
        {
            Vector3 to = target - villager.position; to.y = 0f;
            if (to.magnitude < 0.25f) { Play("Watch"); return true; }
            FaceTarget(villager, target);
            villager.position += to.normalized * (speed * dt);
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
            if (cam) bubbleT.rotation = cam.transform.rotation;
        }

        void FlickerFire()
        {
            if (fireLight) fireLight.intensity = 6f + 6f * campGrow + Mathf.PerlinNoise(Time.time * 9f, 0f) * 3f - 1.5f;
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

        static Transform FindNearestCottage(Vector3 from)
        {
            Transform best = null; float bestD = float.MaxValue;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.Contains("Cottage") || t.name == "Cottages") continue;
                if (t.parent && t.parent.name.Contains("Cottage") && t.parent.name != "Cottages") continue; // top-most only
                var b = BoundsOf(t);
                if (b.size.sqrMagnitude < 1f) continue;
                float d = (b.center - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Vector3 Snap(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t) p.y = t.SampleHeight(p) + t.transform.position.y;
            else if (Physics.Raycast(p + Vector3.up * 100f, Vector3.down, out var hit, 300f)) p.y = hit.point.y;
            return p;
        }

        static void Ground(Transform t) => t.position = Snap(t.position);

        static string PrettyName(string n) => n.Replace("(Clone)", "").Trim().ToLowerInvariant();

        // ---------- placeholder art ----------

        static Transform Part(Transform parent, PrimitiveType type, Vector3 worldPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = color };
            r.sharedMaterial.SetColor("_BaseColor", color);
            return go.transform;
        }

        ParticleSystem MakeFlames(Transform at, float scale)
        {
            at.SetParent(transform, true);
            var ps = at.gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.7f * scale);
            main.gravityModifier = -0.2f;
            main.maxParticles = 200;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime = 60f * scale;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.28f * scale;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var color = ps.colorOverLifetime; color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.5f), new GradientColorKey(new Color(0.3f, 0.08f, 0.05f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;
            return ps;
        }

        // World-space sparks that only appear on the gust (and the odd ambient one).
        ParticleSystem MakeEmbers(Transform at)
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(at, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 2f;
            main.startSpeed = 0f;
            main.startSize = 0.12f;
            main.gravityModifier = 0.15f;
            main.maxParticles = 600;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.enabled = false;
            var color = ps.colorOverLifetime; color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;
            // A few drifting sparks around the fire in normal play.
            var sparks = ps.emission; sparks.rateOverTime = 0f;
            StartCoroutine(AmbientSparks(ps));
            return ps;
        }

        System.Collections.IEnumerator AmbientSparks(ParticleSystem ps)
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0.25f, 0.7f));
                if (phase == Phase.Embers) continue;
                ps.Emit(new ParticleSystem.EmitParams
                {
                    position = firePos + Vector3.up * 0.6f + Random.insideUnitSphere * 0.2f,
                    velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(1f, 2f), Random.Range(-0.4f, 0.4f)),
                    startLifetime = Random.Range(1f, 2f),
                    startSize = Random.Range(0.05f, 0.1f),
                    startColor = new Color(1f, 0.7f, 0.25f)
                }, 1);
            }
        }

        ParticleSystem MakeSmoke(Transform at)
        {
            at.SetParent(transform, true);
            var ps = at.gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startColor = new Color(0.15f, 0.15f, 0.17f, 0.5f);
            main.gravityModifier = -0.05f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime = 14f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 20f; shape.radius = 0.8f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var color = ps.colorOverLifetime; color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;
            return ps;
        }

        static Texture2D SoftTexture()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return tex;
        }
    }
}
