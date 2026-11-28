using System.Collections.Generic;
using Amusnet.World;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Turns the map's trees and flowers into spots when the map loads:
///  - Hiding spot (pale blue drifting motes): fly into the leaves or petals to hide from the predator and killers.
///  - Speed boost, trees and flowers (gold rising sparkles): a short speed boost; it recharges afterwards.
/// Each tree or flower gets a fixed kind from its position, so the same plant always does the same thing.
/// Markers are pooled and shown only on the spots nearest the player. Also feeds the HUD cover pill.
/// </summary>
public sealed class NatureSpots : MonoBehaviour
{
    public enum Kind { Hide, Boost }

    [SerializeField] Transform player;

    [Header("Which map objects (name prefixes)")]
    [SerializeField] string[] treePrefixes = { "Tree_", "Pine Tree_" };
    [SerializeField] string[] flowerPrefixes = { "Flower_" };
    [SerializeField, Range(0f, 1f)] float boostShare = 0.35f;

    [Header("Speed boost")]
    [SerializeField] float boostFactor = 1.7f;
    [SerializeField] float boostSeconds = 4f;
    [SerializeField] float rechargeSeconds = 20f;

    [Header("Markers")]
    [SerializeField] Material hideMaterial;
    [SerializeField] Material boostMaterial;
    [SerializeField] Color hideColor = new Color(0.62f, 0.85f, 1f, 0.95f);
    [SerializeField] Color boostColor = new Color(1f, 0.8f, 0.3f, 1f);
    [SerializeField] int markersPerKind = 16;
    [SerializeField] float markerRange = 40f;

    class Spot { public Kind kind; public Bounds bounds; public float readyAt; public ParticleSystem marker; }

    readonly List<Spot> spots = new List<Spot>();
    readonly Dictionary<Kind, List<ParticleSystem>> markers = new Dictionary<Kind, List<ParticleSystem>>();
    readonly List<Spot> nearby = new List<Spot>();
    bool toldHide, toldBoost;
    Transform container;
    ShelterState shelter;
    ButterflyController flight;
    float nextMarkerPass;

    public int CountOf(Kind kind) => spots.FindAll(s => s.kind == kind).Count;

    void OnEnable()
    {
        if (player != null)
        {
            shelter = player.GetComponentInChildren<ShelterState>();
            flight = player.GetComponentInChildren<ButterflyController>();
        }
        var map = SceneManager.GetActiveScene();
        if (container != null && map.isLoaded && map != gameObject.scene)
            Build(map);
        else
            MapLoader.WhenReady(Build);
    }

    void OnDisable() => MapLoader.MapReady -= Build;

    void Build(Scene map)
    {
        MapLoader.MapReady -= Build;
        if (container != null && markers.ContainsKey(Kind.Hide) && markers.ContainsKey(Kind.Boost))
            return;
        if (container != null)
        {
            container.gameObject.SetActive(false);
            Destroy(container.gameObject);
        }
        spots.Clear();
        markers.Clear();
        nextMarkerPass = 0f;
        container = new GameObject("Nature Spots (runtime)").transform;
        container.SetParent(transform, false);
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");

        foreach (var root in map.GetRootGameObjects())
        {
            if (!root.activeInHierarchy) continue;
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                bool tree = Matches(t.name, treePrefixes), flower = !tree && Matches(t.name, flowerPrefixes);
                if (!tree && !flower) continue;
                if (t.parent != null && (Matches(t.parent.name, treePrefixes) || Matches(t.parent.name, flowerPrefixes))) continue;
                if (!TryBounds(t, out var b)) continue;

                if (tree)
                {
                    // The leafy canopy: the upper part of the tree, a little narrower than its widest branches.
                    var min = b.min; var max = b.max;
                    min.y += b.size.y * 0.45f;
                    b.SetMinMax(min, max);
                    b.size = Vector3.Scale(b.size, new Vector3(0.85f, 1f, 0.85f));
                }
                // Flowers: their own petals only, not the grass around them.

                var kind = KindAt(b.center);
                var spot = new Spot { kind = kind, bounds = b };
                spots.Add(spot);

                var go = new GameObject(kind == Kind.Hide ? "Hiding spot" : "Boost spot") { layer = ignoreRaycast };
                go.transform.SetParent(container, false);
                go.transform.position = b.center;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = b.size;
                if (kind == Kind.Hide) go.AddComponent<HidingSpot>();
                else go.AddComponent<SpeedBoostSpot>().Init(this, spots.Count - 1);
            }
        }

        foreach (Kind k in System.Enum.GetValues(typeof(Kind)))
        {
            markers[k] = new List<ParticleSystem>();
            for (int i = 0; i < markersPerKind; i++) markers[k].Add(MakeMarker(k));
        }
    }

    static bool Matches(string name, string[] prefixes)
    {
        foreach (var p in prefixes) if (name.StartsWith(p)) return true;
        return false;
    }

    static bool TryBounds(Transform t, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any;
    }

    // Fixed per plant: the same tree or flower is always the same kind.
    Kind KindAt(Vector3 p)
    {
        unchecked
        {
            int h = Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.z * 10f) * 19349663;
            return (h & 0x7fffffff) % 1000 < boostShare * 1000f ? Kind.Boost : Kind.Hide;
        }
    }

    internal void Touch(int index, Collider other)
    {
        var spot = spots[index];
        if (Time.time < spot.readyAt) return;
        if (GameFlow.Instance != null && !GameFlow.Instance.IsRunning) return;
        var butterfly = other.GetComponentInParent<ButterflyController>();
        if (butterfly == null) return;
        butterfly.ApplySpeedBoost(boostFactor, boostSeconds);
        spot.readyAt = Time.time + rechargeSeconds;
        if (spot.marker != null) spot.marker.Emit(40);
        nextMarkerPass = 0f; // hide its marker while it recharges
    }

    void Update()
    {
        if (GameHUD.Instance != null)
        {
            bool running = GameFlow.Instance == null || GameFlow.Instance.IsRunning;
            GameHUD.Instance.SetCover(running && shelter != null && shelter.IsHidden,
                running && flight != null ? flight.SpotBoostSecondsLeft : 0f);
        }

        if (container == null || player == null || Time.time < nextMarkerPass) return;
        nextMarkerPass = Time.time + 0.25f;
        UpdateMarkers(Kind.Hide);
        UpdateMarkers(Kind.Boost);
        ExplainNearbyKinds();
    }

    // The first time the player comes close to each kind of spot, say what its marker means.
    void ExplainNearbyKinds()
    {
        if (toldHide && toldBoost) return;
        if (GameHUD.Instance == null || (GameFlow.Instance != null && !GameFlow.Instance.IsRunning)) return;
        foreach (var s in spots)
        {
            if (s.marker == null || (s.bounds.center - player.position).sqrMagnitude > 64f) continue;
            if (s.kind == Kind.Hide && !toldHide) { toldHide = true; GameHUD.Instance.Toast("Blue motes: a hiding spot. Fly in to hide", 4f); return; }
            if (s.kind == Kind.Boost && !toldBoost) { toldBoost = true; GameHUD.Instance.Toast("Gold sparkles: a speed boost. Fly through it", 4f); return; }
        }
    }

    void UpdateMarkers(Kind kind)
    {
        if (!markers.TryGetValue(kind, out var pool)) return;

        // The nearest ready spots of this kind within range get a marker; the rest lose theirs.
        nearby.Clear();
        Vector3 p = player.position;
        float range2 = markerRange * markerRange;
        foreach (var s in spots)
            if (s.kind == kind && Time.time >= s.readyAt && (s.bounds.center - p).sqrMagnitude <= range2) nearby.Add(s);
        nearby.Sort((a, b) => (a.bounds.center - p).sqrMagnitude.CompareTo((b.bounds.center - p).sqrMagnitude));
        if (nearby.Count > markersPerKind) nearby.RemoveRange(markersPerKind, nearby.Count - markersPerKind);

        foreach (var s in spots)
        {
            if (s.kind != kind || s.marker == null || nearby.Contains(s)) continue;
            s.marker.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            s.marker = null;
        }
        foreach (var s in nearby)
        {
            if (s.marker != null) continue;
            var free = pool.Find(m => !m.isEmitting);
            if (free == null) break;
            free.Clear();
            free.transform.position = s.bounds.center;
            var shape = free.shape;
            shape.scale = s.bounds.size;
            free.Play();
            s.marker = free;
        }
    }

    ParticleSystem MakeMarker(Kind kind)
    {
        bool boost = kind == Kind.Boost;
        var go = new GameObject(boost ? "Boost marker" : "Hiding marker");
        go.transform.SetParent(container, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = boost ? 1.6f : 2.6f;
        main.startSpeed = boost ? 0.5f : 0.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(boost ? 0.2f : 0.25f, boost ? 0.45f : 0.5f);
        main.startColor = boost ? boostColor : hideColor;
        main.gravityModifier = boost ? -0.12f : 0.02f;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = boost ? 22f : 14f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;

        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        if (!boost)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;
        }

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = boost ? boostMaterial : hideMaterial;
        return ps;
    }
}
