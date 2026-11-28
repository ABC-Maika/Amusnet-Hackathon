using System.Collections;
using System.Collections.Generic;
using Butterfly.NPC;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Butterfly reproduction ("find a mate"). Wild butterflies, pink with sparkles, wander the village.
/// Fly up to one to pair: after a short dance it leaves you a brood of babies that follow you as a swarm.
/// Each baby raises the mess score multiplier (read by GameIntegration) and the village's anger.
/// Lives on the player.
/// </summary>
public sealed class ButterflySwarm : MonoBehaviour
{
    public static ButterflySwarm Instance { get; private set; }

    [Tooltip("The butterfly model (visual only) used for wild butterflies and babies.")]
    [SerializeField] GameObject butterflyModel;
    [Tooltip("One wild butterfly wanders around each of these points.")]
    [SerializeField] Transform[] mateHomes;
    [SerializeField] float wanderRadius = 10f;
    [SerializeField] float pairRadius = 2.5f;
    [SerializeField] float pairSeconds = 1.6f;
    [SerializeField] int babiesPerMate = 2;
    [SerializeField] float multiplierPerBaby = 0.1f;
    [Tooltip("Anger added per baby, as a share of the anger bar (0..1).")]
    [SerializeField] float angerPerBaby = 0.03f;
    [SerializeField] Color mateTint = new Color(1f, 0.55f, 0.75f);
    [SerializeField] Color babyTint = new Color(1f, 0.7f, 0.85f);
    [SerializeField] float babyScale = 0.3f;
    [SerializeField] Material sparkleMaterial;

    readonly List<WildButterfly> mates = new List<WildButterfly>();
    readonly List<Transform> babies = new List<Transform>();

    public int Babies => babies.Count;
    public int MatesLeft => mates.FindAll(m => m != null && !m.Taken).Count;
    public float Multiplier => 1f + babies.Count * multiplierPerBaby;

    void Awake() => Instance = this;

    void Start() => MapLoader.WhenReady(SpawnMates);

    void OnDestroy()
    {
        MapLoader.MapReady -= SpawnMates;
        if (Instance == this) Instance = null;
        foreach (var b in babies) if (b != null) Destroy(b.gameObject);
    }

    void SpawnMates(Scene map)
    {
        MapLoader.MapReady -= SpawnMates;
        if (butterflyModel == null || mateHomes == null) return;
        foreach (var home in mateHomes)
        {
            if (home == null) continue;
            var mate = MakeButterfly("Wild butterfly", mateTint, 1f);
            AddSparkles(mate.transform);
            var wild = mate.AddComponent<WildButterfly>();
            wild.Init(home.position, wanderRadius);
            mates.Add(wild);
        }
    }

    void Update()
    {
        if (GameFlow.Instance != null && !GameFlow.Instance.IsRunning) return;
        foreach (var mate in mates)
        {
            if (mate == null || mate.Taken) continue;
            if ((mate.transform.position - transform.position).sqrMagnitude <= pairRadius * pairRadius)
                StartCoroutine(Pair(mate));
        }
    }

    IEnumerator Pair(WildButterfly mate)
    {
        mate.Pair(transform);
        if (GameHUD.Instance != null) GameHUD.Instance.Toast("You found a mate!", pairSeconds);
        yield return new WaitForSeconds(pairSeconds);
        if (mate == null) yield break;

        for (int i = 0; i < babiesPerMate; i++)
        {
            var baby = MakeButterfly("Baby butterfly", babyTint, babyScale);
            baby.transform.position = mate.transform.position + Random.insideUnitSphere * 0.4f;
            babies.Add(baby.transform);
            if (AngerLevels.Instance != null) AngerLevels.Instance.AddAgitation(angerPerBaby);
        }
        mate.Leave();
        if (GameHUD.Instance != null)
            GameHUD.Instance.Toast($"+{babiesPerMate} baby butterflies! The villagers are getting nervous", 3.5f);
    }

    void LateUpdate()
    {
        // The brood circles the player a little above it, clear of the camera behind.
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        for (int i = 0; i < babies.Count; i++)
        {
            var b = babies[i];
            if (b == null) continue;
            float a = i * 137.5f + Time.time * 50f;
            Vector3 ring = Quaternion.Euler(0f, a, 0f) * Vector3.forward * (1.3f + 0.2f * (i % 3));
            Vector3 goal = transform.position + ring + Vector3.up * (0.6f + 0.25f * Mathf.Sin(Time.time * 3f + i));
            Vector3 next = Vector3.Lerp(b.position, goal, 1f - Mathf.Exp(-4f * dt));
            Vector3 flat = new Vector3(next.x - b.position.x, 0f, next.z - b.position.z);
            if (flat.sqrMagnitude > 1e-6f)
                b.rotation = Quaternion.Slerp(b.rotation, Quaternion.LookRotation(flat), 8f * dt);
            b.position = next;
        }
    }

    GameObject MakeButterfly(string name, Color tint, float scale)
    {
        var go = Instantiate(butterflyModel);
        go.name = name;
        go.transform.localScale *= scale;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (var anim in go.GetComponentsInChildren<Animator>()) anim.SetBool("Flapping", true);
        var block = new MaterialPropertyBlock();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            r.SetPropertyBlock(block);
        }
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        return go;
    }

    void AddSparkles(Transform parent)
    {
        var go = new GameObject("Mate sparkles");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startColor = new Color(1f, 0.5f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;
        var emission = ps.emission;
        emission.rateOverTime = 12f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = fade;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkleMaterial;
        ps.Play();
    }
}
