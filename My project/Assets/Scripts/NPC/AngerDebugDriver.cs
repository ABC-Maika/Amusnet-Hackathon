using UnityEngine;
using UnityEngine.InputSystem;

namespace Butterfly.NPC
{
    /// <summary>Test helper: simulates the mess score. ] raises it, [ lowers it. Delete once MessManager feeds AngerLevels.</summary>
    public class AngerDebugDriver : MonoBehaviour
    {
        public float step = 0.05f;
        float mess;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || AngerLevels.Instance == null || Butterfly.UI.PauseMenu.IsPaused) return;
            bool changed = false;
            if (kb.rightBracketKey.wasPressedThisFrame) { mess += step; changed = true; }
            if (kb.leftBracketKey.wasPressedThisFrame) { mess -= step; changed = true; }
            if (!changed) return;
            mess = Mathf.Clamp01(mess);
            AngerLevels.Instance.SetMess(mess);
        }
    }
}
