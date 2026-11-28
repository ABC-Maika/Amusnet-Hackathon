using System;
using ButterflyUI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Drives GameUI.uxml: HUD (mess bar + predator meter), pause, settings, win and game-over screens.
/// Lives on the GameUI prefab next to its UIDocument, GameFlow and MainMenu; drop the prefab into the game scene.
/// Other systems feed it with GameHUD.Instance.SetMess / SetPredator.
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
    Label messValue, predatorValue;
    RadialMeter predatorMeter;
    float mess;
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

    /// <param name="percent">Predator percentage, 0..100.</param>
    public void SetPredator(float percent)
    {
        percent = Mathf.Clamp(percent, 0f, 100f);
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
        hud.EnableInClassList("hud--off", flow.Current == GameFlow.State.Menu || flow.WinPending);

        if (leaving || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
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
