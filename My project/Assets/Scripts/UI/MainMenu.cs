using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Drives the main-menu screen in GameUI.uxml. The menu sits over the live village while
/// MenuCamera circles it; Play swoops the camera down to the player, then starts the run.
/// </summary>
[RequireComponent(typeof(UIDocument), typeof(GameFlow), typeof(GameHUD))]
public class MainMenu : MonoBehaviour
{
    GameFlow flow;
    GameHUD hud;
    VisualElement menuScreen;
    bool starting;

    void Awake()
    {
        flow = GetComponent<GameFlow>();
        hud = GetComponent<GameHUD>();
    }

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        menuScreen = root.Q("menuScreen");
        root.Q<Button>("playButton").clicked += Play;
        root.Q<Button>("settingsButton").clicked += () =>
        {
            UIAnim.SetShown(menuScreen, false);
            hud.OpenSettings(() => UIAnim.SetShown(menuScreen, true));
        };
        root.Q<Button>("quitButton").clicked += Quit;
        UIAnim.Loop(root.Q("logo"), "bob-loop--b");
    }

    // In Start, not OnEnable: GameFlow decides the opening state in its Awake, which may run after our OnEnable.
    void Start() => UIAnim.SetShown(menuScreen, flow.Current == GameFlow.State.Menu);

    void Play()
    {
        if (starting) return;
        starting = true;
        // The logo drifts up and the buttons drop (MainMenu.uss, .menu--exit); the camera swoops once they're gone.
        menuScreen.pickingMode = PickingMode.Ignore;
        menuScreen.AddToClassList("menu--exit");
        menuScreen.schedule.Execute(() =>
        {
            UIAnim.SetShown(menuScreen, false);
            if (MenuCamera.Instance != null) MenuCamera.Instance.FlyToPlayer(flow.StartRun);
            else flow.StartRun();
        }).ExecuteLater(UiExitMs);
    }

    const long UiExitMs = 300; // matches the .menu--exit transitions in MainMenu.uss

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
