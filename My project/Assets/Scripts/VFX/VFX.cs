using UnityEngine;

/// <summary>
/// One-line spawner for the game's particle effects, e.g. <c>VFX.MessPop(pos)</c>.
/// One-shot effects destroy themselves when they finish; trails live as long as the object they are attached to.
/// </summary>
public static class VFX
{
    const string LibraryPath = "VFXLibrary";
    static VFXLibrary library;

    // Domain reload is off for Play Mode, so statics must be cleared by hand.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => library = null;

    static VFXLibrary Library
    {
        get
        {
            if (library == null)
            {
                library = Resources.Load<VFXLibrary>(LibraryPath);
                if (library == null) Debug.LogError($"VFX: no VFXLibrary found at Resources/{LibraryPath}.");
            }
            return library;
        }
    }

    /// <summary>Confetti, sparkles and a "+" pop where a mess happened.</summary>
    public static GameObject MessPop(Vector3 position) => Spawn(Library?.messPop, position);

    /// <summary>Burst of wing scales where the butterfly died.</summary>
    public static GameObject DeathBurst(Vector3 position) => Spawn(Library?.deathBurst, position);

    /// <summary>Anger mark that bounces in above the villager's head, follows it, and fades out.</summary>
    public static GameObject Anger(Transform villager)
    {
        if (villager == null) return null;
        var fx = Spawn(Library?.angerIcon, HeadTop(villager));
        if (fx != null) fx.transform.SetParent(villager, worldPositionStays: true);
        return fx;
    }

    /// <summary>Attaches the pollen trail to the player. It emits only while the player moves.</summary>
    public static GameObject PollenTrail(Transform player) => Attach(Library?.pollenTrail, player);

    static GameObject Spawn(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return null;
        return Object.Instantiate(prefab, position, Quaternion.identity);
    }

    static GameObject Attach(GameObject prefab, Transform parent)
    {
        if (prefab == null || parent == null) return null;
        return Object.Instantiate(prefab, parent, worldPositionStays: false);
    }

    /// <summary>A point just above the top of the target's renderers, or 2 m above its pivot if it has none.</summary>
    static Vector3 HeadTop(Transform target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        bool found = false;
        Bounds bounds = default;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!found) return target.position + Vector3.up * 2f;
        return new Vector3(target.position.x, bounds.max.y + 0.45f, target.position.z);
    }
}
