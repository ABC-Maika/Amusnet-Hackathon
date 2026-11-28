using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Butterfly.UI
{
    /// <summary>
    /// Esc (or gamepad Start) pause menu: Resume, Controls, Restart, Quit.
    /// The Controls page replaces the old always-on controls hint.
    /// Freezes Time.timeScale and audio, frees the cursor, and restores everything on resume.
    /// Other systems check PauseMenu.IsPaused (DeathManager waits on it during the martyr sequence).
    /// Built by Death > Add Anger and Predator UI (Editor/DeathHudBuilder.cs).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        static bool ownPaused;

        /// <summary>True while this menu or the game's own pause (GameFlow.Paused) is open.</summary>
        public static bool IsPaused
        {
            get => ownPaused || (GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Paused);
            private set => ownPaused = value;
        }

        // Enter Play Mode without a domain reload keeps statics: start every session unpaused.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsPaused = false;

        [Header("Pages")]
        public CanvasGroup overlay;
        public GameObject mainPage;
        public GameObject controlsPage;

        [Header("Buttons")]
        public Button resumeButton;
        public Button controlsButton;
        public Button restartButton;
        public Button quitButton;
        public Button backButton;

        float savedTimeScale = 1f;
        float savedFixedDelta = 0.02f;
        CursorLockMode savedLock;
        bool savedCursorVisible;

        void Awake()
        {
            if (resumeButton) resumeButton.onClick.AddListener(Resume);
            if (controlsButton) controlsButton.onClick.AddListener(ShowControls);
            if (restartButton) restartButton.onClick.AddListener(Restart);
            if (quitButton) quitButton.onClick.AddListener(Quit);
            if (backButton) backButton.onClick.AddListener(ShowMain);
            SetVisible(false);
        }

        void OnDisable()
        {
            if (IsPaused) Resume();
        }

        void Update()
        {
            if (GameFlow.Instance != null && GameFlow.Instance.ActivityActive) return;
            bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool start = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            bool back = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;

            if (!IsPaused)
            {
                if (esc || start) Pause();
                return;
            }

            if (esc || back)
            {
                if (controlsPage && controlsPage.activeSelf) ShowMain();
                else Resume();
            }
            else if (start) Resume();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;

            savedTimeScale = Time.timeScale;
            savedFixedDelta = Time.fixedDeltaTime;
            savedLock = Cursor.lockState;
            savedCursorVisible = Cursor.visible;

            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SetVisible(true);
            ShowMain();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;

            Time.timeScale = savedTimeScale;
            Time.fixedDeltaTime = savedFixedDelta;
            AudioListener.pause = false;
            Cursor.lockState = savedLock;
            Cursor.visible = savedCursorVisible;

            SetVisible(false);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        public void ShowControls()
        {
            if (mainPage) mainPage.SetActive(false);
            if (controlsPage) controlsPage.SetActive(true);
            Select(backButton);
        }

        public void ShowMain()
        {
            if (controlsPage) controlsPage.SetActive(false);
            if (mainPage) mainPage.SetActive(true);
            Select(resumeButton);
        }

        public void Restart()
        {
            // Hand back normal time before the reload: the martyr sequence may have left slow-mo on.
            IsPaused = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            AudioListener.pause = false;
            // In the game the map is the active scene; GameFlow reloads the systems scene that loads it.
            if (GameFlow.Instance != null) { GameFlow.Instance.Restart(); return; }
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
#if UNITY_EDITOR
            else // test scenes that are not in Build Settings
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetVisible(bool on)
        {
            if (!overlay) return;
            overlay.alpha = on ? 1f : 0f;
            overlay.interactable = on;
            overlay.blocksRaycasts = on;
        }

        static void Select(Button b)
        {
            if (b && EventSystem.current) EventSystem.current.SetSelectedGameObject(b.gameObject);
        }
    }
}
