using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ButterflyPollen : MonoBehaviour
{
    private ParticleSystem trail;
    public bool IsEmitting { get; private set; }

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
