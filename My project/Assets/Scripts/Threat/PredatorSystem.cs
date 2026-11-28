using System.Collections;
using UnityEngine;

/// <summary>
/// Predator threat, 0..100. Rises while the butterfly is out in the open, falls while it is hidden,
/// and at 100 a predator catches it. Ticks only while GameFlow is Playing and pushes the value to the
/// HUD every frame. One per scene.
/// </summary>
public class PredatorSystem : MonoBehaviour
{
    const string PlaceholderPlayerName = "Player (placeholder cube)";

    [Tooltip("The butterfly. If empty, found by the Player tag, then by the placeholder cube's name.")]
    [SerializeField] Transform player;

    [Header("Rates (percent per second)")]
    [Tooltip("Gain while in the open. 4/s fills the meter in 25 s.")]
    [SerializeField, Min(0f)] float risePerSecond = 4f;
    [Tooltip("Loss while hidden. 15/s empties a full meter in under 7 s.")]
    [SerializeField, Min(0f)] float fallPerSecond = 15f;
    [Tooltip("Seconds in the open (after a run starts or the butterfly leaves cover) before the meter starts rising.")]
    [SerializeField, Min(0f)] float graceDelay = 1.5f;

    [Header("Placeholder catch")]
    [SerializeField, Min(0.1f)] float swoopDuration = 1f;
    [SerializeField] Color predatorColor = new(0.06f, 0.05f, 0.08f);

    public static PredatorSystem Instance { get; private set; }

    /// <summary>Current threat, 0..100.</summary>
    public float Percent { get; private set; }
    public bool IsHidden { get; private set; }

    Amusnet.World.ShelterState shelter;

    float openTime;
    bool catching;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (player) return;
        GameObject found = null;
        try { found = GameObject.FindWithTag("Player"); } catch (UnityException) { }
        if (!found) found = GameObject.Find(PlaceholderPlayerName);
        if (found) player = found.transform;
        else Debug.LogWarning("PredatorSystem: no player assigned or found; the meter will not move.", this);
    }

    public void Track(Transform butterfly)
    {
        player = butterfly;
        shelter = butterfly.GetComponent<Amusnet.World.ShelterState>();
    }

    void Update()
    {
        var flow = GameFlow.Instance;
        if (catching || !player || !flow || !flow.IsRunning) return;

        // Hidden in a HidingZone, or inside one of emkata's HidingSpots (tracked by a ShelterState on the player).
        if (shelter == null) shelter = player.GetComponentInParent<Amusnet.World.ShelterState>();
        IsHidden = HidingZone.IsHidden(player.position) || (shelter != null && shelter.IsHidden);

        if (IsHidden)
        {
            openTime = 0f;
            Percent = Mathf.Max(0f, Percent - fallPerSecond * Time.deltaTime);
        }
        else
        {
            openTime += Time.deltaTime;
            if (openTime > graceDelay)
                Percent = Mathf.Min(100f, Percent + risePerSecond * Time.deltaTime);
        }

        GameHUD.Instance?.SetPredator(Percent);

        if (Percent >= 100f) TriggerCatch();
    }

    /// <summary>
    /// The predator has caught the butterfly. This is the one place to swap in Kris's death system
    /// (Sprint 4 "Martyr's Last Flutter", DeathManager plus a DeathCause for the predator) once it is
    /// merged: replace the body with a call into it and let that system call GameFlow.Lose().
    /// </summary>
    void TriggerCatch()
    {
        catching = true;
        StartCoroutine(PlaceholderCatch());
    }

    // Placeholder: a dark shape swoops down onto the player, then the run is lost.
    // Uses unscaled time and holds still while the game is paused.
    IEnumerator PlaceholderCatch()
    {
        var shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shape.name = "Predator (placeholder)";
        Destroy(shape.GetComponent<Collider>());
        shape.transform.localScale = new Vector3(2.4f, 0.5f, 1.2f);
        shape.GetComponent<Renderer>().material.color = predatorColor;

        Vector3 start = player.position + new Vector3(-14f, 12f, -14f);
        float t = 0f;
        while (t < 1f)
        {
            if (GameFlow.Instance && GameFlow.Instance.Current == GameFlow.State.Paused) { yield return null; continue; }
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / swoopDuration);
            Vector3 end = player ? player.position : start;
            // Ease in, and dip below the straight line so it reads as a dive.
            float e = t * t;
            Vector3 pos = Vector3.Lerp(start, end, e) + Vector3.down * (Mathf.Sin(e * Mathf.PI) * 3f);
            shape.transform.LookAt(pos + (pos - shape.transform.position));
            shape.transform.position = pos;
            yield return null;
        }

        if (GameFlow.Instance) GameFlow.Instance.Lose();
    }
}
