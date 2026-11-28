using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Finds the player butterfly. Prefers the real flight controller (ButterflyController), then the
    /// test flyer (SimFlyer), then any object named "Butterfly..." that is not a camera.
    /// </summary>
    public static class ButterflyLocator
    {
        static Transform cached;

        public static Transform Find()
        {
            if (cached != null && cached.gameObject.activeInHierarchy) return cached;

            var real = Object.FindFirstObjectByType<ButterflyController>();
            if (real != null) return cached = real.transform;

            var sim = Object.FindFirstObjectByType<SimFlyer>();
            if (sim != null) return cached = sim.transform;

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Butterfly") && t.GetComponent<Camera>() == null && !t.name.Contains("Camera"))
                    return cached = t;
            return null;
        }
    }
}
