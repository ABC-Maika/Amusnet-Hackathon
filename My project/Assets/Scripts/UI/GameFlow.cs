using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the run state and Time.timeScale. The game scene opens on the main menu (State.Menu);
/// StartRun() begins play. Other systems call Win() / Lose() and should only act while Current is Playing.
/// The UI listens to StateChanged.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum State { Menu, Playing, Paused, Won, Lost }

    public static GameFlow Instance { get; private set; }
    public State Current { get; private set; }
    public event Action<State> StateChanged;
    MonoBehaviour activityOwner;

    // Set by Restart so the reloaded scene skips the menu and drops straight into play.
    static bool skipMenu;

    // Domain reload is off in this project, so statics must be reset for each Play Mode session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        skipMenu = false;
        Instance = null;
    }

    void Awake()
    {
        Instance = this;
        Current = skipMenu ? State.Playing : State.Menu;
        skipMenu = false;
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }

    public void StartRun() { if (Current == State.Menu) SetState(State.Playing); }

    /// <summary>
    /// Optional cinematic that Win() runs before entering Won (set by WinSequence). It must call the
    /// given callback when finished. Time keeps running meanwhile; pause and Lose() are ignored.
    /// </summary>
    public Action<Action> WinCinematic { get; set; }
    public bool WinPending { get; private set; }

    /// <summary>True only during live play: Playing and not in the win cinematic. Gameplay systems should tick only while this holds.</summary>
    public bool ActivityActive => activityOwner != null;
    public bool IsRunning => Current == State.Playing && !WinPending && !ActivityActive;

    public bool TryBeginActivity(MonoBehaviour owner)
    {
        if (!owner || !IsRunning) return false;
        activityOwner = owner;
        StateChanged?.Invoke(Current);
        return true;
    }

    public void EndActivity(MonoBehaviour owner)
    {
        if (activityOwner != owner) return;
        activityOwner = null;
        StateChanged?.Invoke(Current);
    }

    public void TogglePause()
    {
        if (WinPending || ActivityActive) return;
        if (Current == State.Playing) SetState(State.Paused);
        else if (Current == State.Paused) SetState(State.Playing);
    }

    public void Win()
    {
        if (!IsRunning) return;
        if (WinCinematic == null) { SetState(State.Won); return; }
        WinPending = true;
        WinCinematic(() =>
        {
            WinPending = false;
            if (Current == State.Playing) SetState(State.Won);
        });
    }

    public void Lose() { if (IsRunning) SetState(State.Lost); }

    public void Restart()
    {
        skipMenu = true;
        ReloadScene();
    }

    public void QuitToMenu() => ReloadScene();

    // Reload this scene, not the active one: the active scene is the additively loaded map (see MapLoader).
    void ReloadScene() => SceneManager.LoadScene(gameObject.scene.buildIndex);

    void SetState(State next)
    {
        Current = next;
        Time.timeScale = next is State.Playing or State.Menu ? 1f : 0f;
        StateChanged?.Invoke(next);
    }
}
