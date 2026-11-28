using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Implement on anything that should react when the dying butterfly lands
    /// (pots, laundry, gates, chickens...). The Interactable base class can implement this.
    /// </summary>
    public interface IChainReactionTarget
    {
        /// <summary>Called once per target in the blast radius. Returns the mess value it caused.</summary>
        float TriggerChain(Vector3 origin, float power);
    }
}
