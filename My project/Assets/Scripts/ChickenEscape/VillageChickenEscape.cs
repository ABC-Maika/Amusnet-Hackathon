using Amusnet.Mess;
using Butterfly.Death;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Amusnet.ChickenEscape
{
    public sealed class VillageChickenEscape : MonoBehaviour
    {
        const float EntryRange = 3.4f;
        const int PointsPerHen = 5;

        [SerializeField] GameObject yardPrefab;
        [SerializeField] GameObject yard;
        [SerializeField] Transform entrance;

        MarketEscapeRun session;
        GameFlow flow;
        ButterflyController player;
        Camera villageCamera;
        AudioListener villageListener;
        bool listenerWasEnabled, cursorWasVisible, promptVisible;
        CursorLockMode cursorLock;
        float timeBeforeEntry, nextEntry;
        int bestRescue, completedThisVisit;

        public bool Active => session != null;

        void Update()
        {
            if (Active) return;
            var game = GameFlow.Instance;
            bool canApproach = game != null && game.IsRunning && Time.timeScale > 0f &&
                Time.unscaledTime >= nextEntry && (!DeathManager.Instance || !DeathManager.Instance.IsDying);
            if (!canApproach)
            {
                ShowPrompt(false);
                return;
            }

            if (!player) player = FindAnyObjectByType<ButterflyController>();
            bool nearby = player && ((player.transform.position - entrance.position).sqrMagnitude <= EntryRange * EntryRange ||
                (player.transform.position - transform.position).sqrMagnitude <= EntryRange * EntryRange);
            ShowPrompt(nearby);
            if (nearby && Keyboard.current?.eKey.wasPressedThisFrame == true) Enter(game);
        }

        void Enter(GameFlow game)
        {
            var run = yard.GetComponentInChildren<MarketEscapeRun>(true);
            var camera = Camera.main;
            if (!run || !camera) return;

            cursorLock = Cursor.lockState;
            cursorWasVisible = Cursor.visible;
            timeBeforeEntry = Time.timeScale;
            if (!game.TryBeginActivity(this)) return;

            flow = game;
            completedThisVisit = 0;
            villageCamera = camera;
            villageListener = camera.GetComponent<AudioListener>();
            listenerWasEnabled = villageListener && villageListener.enabled;
            ShowPrompt(false);
            player.gameObject.SetActive(false);
            villageCamera.enabled = false;
            if (villageListener) villageListener.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            StartAttempt(run, false);
        }

        internal void Retry(MarketEscapeRun run, bool skipBond)
        {
            if (session != run) return;
            RememberCompletion(run);
            ReplaceYard();
            Time.timeScale = 1f;
            StartAttempt(yard.GetComponentInChildren<MarketEscapeRun>(true), skipBond);
        }

        internal void Leave(MarketEscapeRun run)
        {
            if (session != run) return;
            RememberCompletion(run);
            RestoreVillage();
            ReplaceYard();
            if (completedThisVisit > 0) Reward(completedThisVisit);
        }

        void RememberCompletion(MarketEscapeRun run)
        {
            if (run.Current == MarketEscapeRun.Phase.Won)
                completedThisVisit = Mathf.Max(completedThisVisit, run.Saved);
        }

        void StartAttempt(MarketEscapeRun run, bool skipBond)
        {
            session = run;
            run.AttachToVillage(this, skipBond);
            run.gameObject.SetActive(true);
        }

        void ReplaceYard()
        {
            yard.SetActive(false);
            Destroy(yard);
            yard = Instantiate(yardPrefab, transform, false);
        }

        void RestoreVillage()
        {
            session.gameObject.SetActive(false);
            session = null;
            if (villageCamera) villageCamera.enabled = true;
            if (villageListener) villageListener.enabled = listenerWasEnabled;
            if (player) player.gameObject.SetActive(true);
            Time.timeScale = timeBeforeEntry;
            if (flow) flow.EndActivity(this);
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorWasVisible;
            nextEntry = Time.unscaledTime + .55f;
        }

        void Reward(int saved)
        {
            int improvement = Mathf.Max(0, saved - bestRescue);
            bestRescue = Mathf.Max(saved, bestRescue);
            var mess = player ? player.GetComponent<MessManager>() : null;
            int points = mess ? mess.Multiplied(improvement * PointsPerHen) : 0;
            if (points > 0) mess.AddMess(points);
            GameHUD.Instance?.Toast(points > 0 ? $"{saved} hens freed  ·  +{points} mess" : $"{saved} hens freed");
        }

        void ShowPrompt(bool show)
        {
            if (promptVisible == show) return;
            promptVisible = show;
            if (GameHUD.Instance) GameHUD.Instance.SetActivityPrompt(show ? "E  ·  Start Chicken Escape" : "");
        }

        void OnDisable()
        {
            ShowPrompt(false);
            if (Active) RestoreVillage();
        }
    }
}
