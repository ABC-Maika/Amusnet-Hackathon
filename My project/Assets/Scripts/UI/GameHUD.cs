using System;
using ButterflyUI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Drives GameUI.uxml: HUD (mess bar + predator meter), pause, settings, win and game-over screens.
/// Lives on the GameUI prefab next to its UIDocument, GameFlow and MainMenu; drop the prefab into the game scene.
/// Other systems feed it with GameHUD.Instance.SetMess / SetPredator / SetMultiplier / SetCover / Toast.
/// </summary>
[RequireComponent(typeof(UIDocument), typeof(GameFlow))]
public class GameHUD : MonoBehaviour
{
    [Tooltip("Predator percentage at which the meter turns red and starts pulsing.")]
    [SerializeField, Range(0f, 100f)] float predatorDangerAt = 70f;

    public static GameHUD Instance { get; private set; }

    GameFlow flow;
    SettingsPanel settings;
    VisualElement hud, pauseScreen, settingsScreen, winScreen, loseScreen, fade, messFill;
    Label messValue, predatorValue, messMultiplier, coverStatus, toast, activityPrompt;
    RadialMeter predatorMeter;
    float mess, multiplier = 1f, predator, safeUntil, toastUntil;
    bool wasHidden, chasedWhileHidden;
    string coverKind = "", coverText;
    static readonly string[] CoverKinds = { "hidden", "boost", "safe" };
    bool leaving;
    Action afterSettings;

    /// <param name="normalized">Mess score progress toward the goal, 0..1.</param>
    public void SetMess(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        if (normalized > mess)
        {
            UIAnim.Bump(messValue, "mess-value--bump");
            UIAnim.Bump(messFill, "mess-fill--bump", 300);
        }
        mess = normalized;
        messFill.style.width = Length.Percent(normalized * 100f); // a live value, so it can't come from USS
        messValue.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
    }

    /// <param name="value">Points multiplier of each new mess; the tag is hidden at x1.</param>
    public void SetMultiplier(float value)
    {
        if (Mathf.Approximately(value, multiplier)) return;
        if (value > multiplier) UIAnim.Bump(messMultiplier, "mess-mult--bump");
        multiplier = value;
        messMultiplier.text = $"×{value:0.##}";
        messMultiplier.EnableInClassList("mess-mult--off", value <= 1.001f);
    }

    /// <summary>Cover pill under the predator ring: Hidden, Speed boost, or Safe once the predator gives up.</summary>
    public void SetCover(bool hidden, float boostSecondsLeft)
    {
        // "Safe": the predator was after you when you hid, and has now lost you.
        if (hidden && !wasHidden) chasedWhileHidden = predator > 5f;
        if (hidden && chasedWhileHidden && predator <= 0.5f) { chasedWhileHidden = false; safeUntil = Time.unscaledTime + 2.5f; }
        if (!hidden) safeUntil = 0f;
        wasHidden = hidden;

        string kind = null, text = null;
        if (Time.unscaledTime < safeUntil) { kind = "safe"; text = "Safe: the predator lost you"; }
        else if (hidden) { kind = "hidden"; text = "Hidden"; }
        else if (boostSecondsLeft > 0f) { kind = "boost"; text = BoostText(Mathf.CeilToInt(boostSecondsLeft)); }

        // Called every frame: only touch the UI when something changed.
        if (kind != coverKind)
        {
            coverKind = kind;
            coverStatus.EnableInClassList("cover-status--off", kind == null);
            foreach (var k in CoverKinds) coverStatus.EnableInClassList("cover-status--" + k, k == kind);
        }
        if (text != null && !ReferenceEquals(text, coverText)) coverStatus.text = coverText = text;
    }

    static readonly string[] boostTexts = new string[16];
    static string BoostText(int seconds)
    {
        seconds = Mathf.Clamp(seconds, 0, boostTexts.Length - 1);
        return boostTexts[seconds] ??= $"Speed boost {seconds}s";
    }

    /// <summary>Sets the game-over screen's title and line (e.g. who ate the butterfly).</summary>
    public void SetLoseText(string title, string line)
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root.Q<Label>("loseTitle") is { } t) t.text = title;
        if (root.Q<Label>("loseLine") is { } l) l.text = line;
    }

    /// <summary>Shows a short message above the bottom of the screen.</summary>
    public void Toast(string text, float seconds = 3f)
    {
        toast.text = text;
        toast.RemoveFromClassList("hud-toast--off");
        toastUntil = Time.unscaledTime + seconds;
    }

    public void SetActivityPrompt(string text)
    {
        if (activityPrompt == null) return;
        activityPrompt.text = text;
        activityPrompt.EnableInClassList("hud-toast--off", string.IsNullOrEmpty(text));
    }

    /// <param name="percent">Predator percentage, 0..100.</param>
    public void SetPredator(float percent)
    {
        percent = Mathf.Clamp(percent, 0f, 100f);
        predator = percent;
        predatorMeter.Value = percent / 100f;
        predatorValue.text = $"{Mathf.RoundToInt(percent)}%";
        predatorMeter.EnableInClassList("predator-meter--danger", percent >= predatorDangerAt);
    }

    void Awake()
    {
        Instance = this;
        flow = GetComponent<GameFlow>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        hud = root.Q("hud");
        pauseScreen = root.Q("pauseScreen");
        settingsScreen = root.Q("settingsScreen");
        winScreen = root.Q("winScreen");
        loseScreen = root.Q("loseScreen");
        fade = root.Q("fade");
        messFill = root.Q("messFill");
        messValue = root.Q<Label>("messValue");
        predatorValue = root.Q<Label>("predatorValue");
        predatorMeter = root.Q<RadialMeter>("predatorMeter");
        messMultiplier = root.Q<Label>("messMultiplier");
        coverStatus = root.Q<Label>("coverStatus");
        toast = root.Q<Label>("toast");
        activityPrompt = root.Q<Label>("activityPrompt");

        settings = new SettingsPanel(settingsScreen);
        settings.BackClicked += CloseSettings;

        root.Q<Button>("resumeButton").clicked += flow.TogglePause;
        root.Q<Button>("pauseSettingsButton").clicked += () =>
        {
            UIAnim.SetShown(pauseScreen, false);
            OpenSettings(() => UIAnim.SetShown(pauseScreen, flow.Current == GameFlow.State.Paused));
        };
        root.Q<Button>("pauseMenuButton").clicked += () => Leave(flow.QuitToMenu);
        root.Q<Button>("winRestartButton").clicked += () => Leave(flow.Restart);
        root.Q<Button>("winMenuButton").clicked += () => Leave(flow.QuitToMenu);
        root.Q<Button>("loseRestartButton").clicked += () => Leave(flow.Restart);
        root.Q<Button>("loseMenuButton").clicked += () => Leave(flow.QuitToMenu);

        UIAnim.Loop(root.Q(className: "mess-shine"), "mess-shine--b");
        UIAnim.Loop(predatorMeter, "predator-meter--b");
        UIAnim.Cover(fade, false);

        flow.StateChanged += OnState;
        SetMess(0f);
        SetPredator(0f);
    }

    // In Start, not OnEnable: GameFlow decides the opening state in its Awake, which may run after our OnEnable.
    void Start() => OnState(flow.Current);

    void OnDisable()
    {
        flow.StateChanged -= OnState;
    }

    void Update()
    {
        // Hidden on the menu and while the win cinematic plays (WinPending has no event, so check each frame).
        hud.EnableInClassList("hud--off", flow.Current == GameFlow.State.Menu || flow.WinPending || flow.ActivityActive);
        if (toastUntil > 0f && Time.unscaledTime > toastUntil) { toastUntil = 0f; toast.AddToClassList("hud-toast--off"); }

        if (leaving || flow.ActivityActive || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (settingsScreen.ClassListContains("is-shown")) settings.Back();
        else if (flow.Current is GameFlow.State.Playing or GameFlow.State.Paused) flow.TogglePause();
    }

    void OnState(GameFlow.State state)
    {
        hud.EnableInClassList("hud--dimmed", state is not (GameFlow.State.Playing or GameFlow.State.Menu));
        UIAnim.SetShown(pauseScreen, state == GameFlow.State.Paused);
        UIAnim.SetShown(settingsScreen, false);
        UIAnim.SetShown(winScreen, state == GameFlow.State.Won);
        UIAnim.SetShown(loseScreen, state == GameFlow.State.Lost);
    }

    /// <summary>Opens the shared settings screen; <paramref name="onClosed"/> runs when the player goes back.</summary>
    public void OpenSettings(Action onClosed)
    {
        settings.Refresh();
        afterSettings = onClosed;
        UIAnim.SetShown(settingsScreen, true);
    }

    void CloseSettings()
    {
        UIAnim.SetShown(settingsScreen, false);
        afterSettings?.Invoke();
        afterSettings = null;
    }

    /// <summary>Fades to white, then changes scene.</summary>
    void Leave(Action loadScene)
    {
        if (leaving) return;
        leaving = true;
        UIAnim.Cover(fade, true);
        fade.schedule.Execute(loadScene).ExecuteLater(UIAnim.CoverMs + 50);
    }
}
