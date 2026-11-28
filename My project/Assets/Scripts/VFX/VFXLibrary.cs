using UnityEngine;

/// <summary>The VFX prefabs the <see cref="VFX"/> spawner uses. One instance lives at Resources/VFXLibrary.</summary>
[CreateAssetMenu(menuName = "Butterfly Rebellion/VFX Library", fileName = "VFXLibrary")]
public class VFXLibrary : ScriptableObject
{
    [Tooltip("Looping trail parented under the player. Emits only while it moves.")]
    public GameObject pollenTrail;
    [Tooltip("One-shot confetti and sparkles where a mess happens.")]
    public GameObject messPop;
    [Tooltip("One-shot anger mark that bounces in above a villager's head and fades out.")]
    public GameObject angerIcon;
    [Tooltip("One-shot burst of wing scales when the butterfly dies.")]
    public GameObject deathBurst;
    [Tooltip("Looping ghostly glow and trail parented under the spirit successor.")]
    public GameObject spiritTrail;
}
