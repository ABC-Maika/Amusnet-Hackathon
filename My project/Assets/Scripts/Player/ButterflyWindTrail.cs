using Amusnet.World;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ButterflyWindTrail : MonoBehaviour
{
    [SerializeField] private ButterflyController butterfly;
    [SerializeField] private WindSystem wind;

    private ParticleSystem trail;

    private void Awake()
    {
        trail = GetComponent<ParticleSystem>();
        var main = trail.main;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
    }

    private void LateUpdate()
    {
        if (butterfly == null || !butterfly.isActiveAndEnabled || !butterfly.IsBoosting)
        {
            StopEmitting();
            return;
        }

        Vector3 drift = wind != null && wind.isActiveAndEnabled
            ? wind.Sample(transform.position, Time.time)
            : Vector3.zero;
        var motion = trail.velocityOverLifetime;
        motion.x = drift.x;
        motion.y = drift.y;
        motion.z = drift.z;

        if (!trail.isEmitting)
            trail.Play(false);
    }

    private void OnDisable()
    {
        StopEmitting();
    }

    private void StopEmitting()
    {
        if (trail != null && trail.isEmitting)
            trail.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }
}
