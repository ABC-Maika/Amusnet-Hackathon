using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ButterflyPollen : MonoBehaviour
{
    private ParticleSystem trail;
    public bool IsEmitting { get; private set; }

    /// <summary>The player's pollen that is live right now. Map content saved with the map's own preview
    /// butterfly switches to this one when the game turns that preview rig off.</summary>
    public static ButterflyPollen Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Active = null;

    private void OnEnable() => Active = this;

    private void Awake()
    {
        trail = GetComponent<ParticleSystem>();
        var main = trail.main;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = trail.emission;
        emission.enabled = false;
    }

    private void Update()
    {
        SetEmitting(Keyboard.current?.spaceKey.isPressed == true);
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        if (trail != null)
            SetEmitting(false);
    }

    private void SetEmitting(bool held)
    {
        if (IsEmitting == held)
            return;

        IsEmitting = held;
        var emission = trail.emission;
        emission.enabled = held;
        if (held)
            trail.Play(false);
        else
            trail.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }
}
