using UnityEngine;

namespace Amusnet.Mess
{
    // Shared slow-motion requests. Anything that wants slow time calls TimeSlow.Hold(scale) every frame it needs it;
    // the lowest requested scale wins and time eases back to normal once nobody asks. Ignored while the death sequence runs.
    public sealed class TimeSlow : MonoBehaviour
    {
        private static TimeSlow instance;
        private float requested = 1f;
        private bool touched;

        public static void Hold(float scale)
        {
            if (!instance)
            {
                instance = new GameObject("Time Slow").AddComponent<TimeSlow>();
                DontDestroyOnLoad(instance.gameObject);
            }
            instance.requested = Mathf.Min(instance.requested, Mathf.Clamp(scale, 0.05f, 1f));
        }

        private void Update()
        {
            var death = Butterfly.Death.DeathManager.Instance;
            if (death && death.IsDying) { requested = 1f; touched = false; return; }
            if (requested < 1f)
            {
                touched = true;
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, requested, Time.unscaledDeltaTime * 6f);
            }
            else if (touched)
            {
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, Time.unscaledDeltaTime * 3f);
                if (Mathf.Approximately(Time.timeScale, 1f)) touched = false;
            }
            if (touched) Time.fixedDeltaTime = 0.02f * Time.timeScale;
            else Time.fixedDeltaTime = 0.02f;
            requested = 1f;
        }

        private void OnDestroy()
        {
            if (instance == this) { instance = null; Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; }
        }
    }
}
