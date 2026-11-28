using UnityEngine;

/// <summary>
/// Lets the player act only during live play (GameFlow.IsRunning): flight is switched off on the menu,
/// while paused, during the win cinematic and after the run ends, and the follow camera stops taking the
/// mouse so the cursor is free for the UI. Also applies the mouse-sensitivity setting.
/// Reacts to GameFlow.StateChanged as well as polling, because the death sequence disables every script
/// on the butterfly (this one included) before the game-over screen opens, and the cursor must still be freed.
/// </summary>
public class GameplayGate : MonoBehaviour
{
    [Tooltip("Components that only run during live play, e.g. ButterflyController.")]
    [SerializeField] Behaviour[] whileRunning;
    [SerializeField] ButterflyCamera followCamera;

    bool? wasRunning;
    GameFlow flow;

    void Start()
    {
        flow = GameFlow.Instance;
        if (flow != null) flow.StateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (flow != null) flow.StateChanged -= OnStateChanged;
    }

    // Still called while this component is disabled (e.g. by the death sequence).
    void OnStateChanged(GameFlow.State _) => Apply();

    // WinPending has no event, so poll too.
    void Update()
    {
        if (followCamera != null) followCamera.SensitivityScale = GameSettings.MouseSensitivity;
        Apply();
    }

    void Apply()
    {
        var current = GameFlow.Instance;
        bool dying = Butterfly.Death.DeathManager.Instance != null && Butterfly.Death.DeathManager.Instance.IsDying;
        bool running = current != null && current.IsRunning && !dying;
        if (running == wasRunning) return;
        wasRunning = running;
        foreach (var b in whileRunning) if (b != null) b.enabled = running;
        if (followCamera != null) followCamera.InputEnabled = running;
    }
}
