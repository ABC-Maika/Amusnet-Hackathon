using UnityEngine;

/// <summary>
/// Lets the player act only during live play (GameFlow.IsRunning): flight is switched off on the menu,
/// while paused, during the win cinematic and after the run ends, and the follow camera stops taking the
/// mouse so the cursor is free for the UI. Also applies the mouse-sensitivity setting.
/// </summary>
public class GameplayGate : MonoBehaviour
{
    [Tooltip("Components that only run during live play, e.g. ButterflyController.")]
    [SerializeField] Behaviour[] whileRunning;
    [SerializeField] ButterflyCamera followCamera;

    bool? wasRunning;

    void Update()
    {
        var flow = GameFlow.Instance;
        bool running = flow != null && flow.IsRunning;
        if (followCamera != null) followCamera.SensitivityScale = GameSettings.MouseSensitivity;
        if (running == wasRunning) return;
        wasRunning = running;
        foreach (var b in whileRunning) if (b != null) b.enabled = running;
        if (followCamera != null) followCamera.InputEnabled = running;
    }
}
