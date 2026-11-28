using Amusnet.Mess;
using Butterfly.Death;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Amusnet.ChickenEscape
{
    [DefaultExecutionOrder(-50)]
    public sealed class VillageChickenEscape : MonoBehaviour
    {
        const float EntryRange = 3.4f;
        const int PointsPerHen = 5;

        [SerializeField] GameObject yardPrefab;
        [SerializeField] GameObject yard;
        [SerializeField] Transform entrance;

        MarketEscapeRun preparedRun, session;
        GameFlow flow;
        ButterflyController player;
        Camera villageCamera;
        AudioListener villageListener;
        bool listenerWasEnabled, cursorWasVisible;
        string activityPrompt = "";
        CursorLockMode cursorLock;
        float timeBeforeEntry, nextEntry;
        int bestRescue, completedThisVisit;

        public bool Active => session != null;

        void Awake() => preparedRun = yard.GetComponentInChildren<MarketEscapeRun>(true);

        void Update()
        {
            if (Active) return;
            var game = GameFlow.Instance;
            bool canApproach = game != null && game.IsRunning && Time.timeScale > 0f &&
                Time.unscaledTime >= nextEntry && (!DeathManager.Instance || !DeathManager.Instance.IsDying);
            if (!canApproach || !preparedRun)
            {
                ShowPrompt("");
                return;
            }

            if (!player) player = FindAnyObjectByType<ButterflyController>();
            if (!player) return;

            Vector3 position = player.transform.position;
            bool nearDog = preparedRun.WithinDogReach(position);
            bool nearEntrance = (position - entrance.position).sqrMagnitude <= EntryRange * EntryRange ||
                (position - transform.position).sqrMagnitude <= EntryRange * EntryRange;
            ShowPrompt(nearDog ? "E  ·  Untie the dog" : nearEntrance ? "E  ·  Start Chicken Escape" : "");
            if ((nearDog || nearEntrance) && Keyboard.current?.eKey.wasPressedThisFrame == true)
                Enter(game, nearDog ? position : null);
        }

        void Enter(GameFlow game, Vector3? untieFrom)
        {
            var camera = Camera.main;
            if (!camera) return;

            cursorLock = Cursor.lockState;
            cursorWasVisible = Cursor.visible;
            timeBeforeEntry = Time.timeScale;
            if (!game.TryBeginActivity(this)) return;

            flow = game;
            completedThisVisit = 0;
            villageCamera = camera;
            villageListener = camera.GetComponent<AudioListener>();
            listenerWasEnabled = villageListener && villageListener.enabled;
            ShowPrompt("");
            player.gameObject.SetActive(false);
            villageCamera.enabled = false;
            if (villageListener) villageListener.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            StartAttempt(false, untieFrom);
        }

        internal void Retry(MarketEscapeRun run, bool skipBond)
        {
            if (session != run) return;
            RememberCompletion(run);
            ReplaceYard();
            Time.timeScale = 1f;
            StartAttempt(skipBond);
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

        void StartAttempt(bool skipBond, Vector3? untieFrom = null)
        {
            session = preparedRun;
            session.AttachToVillage(this, skipBond, untieFrom);
            session.gameObject.SetActive(true);
        }

        void ReplaceYard()
        {
            yard.SetActive(false);
            Destroy(yard);
            yard = Instantiate(yardPrefab, transform, false);
            preparedRun = yard.GetComponentInChildren<MarketEscapeRun>(true);
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

        void ShowPrompt(string prompt)
        {
            if (activityPrompt == prompt) return;
            activityPrompt = prompt;
            if (GameHUD.Instance) GameHUD.Instance.SetActivityPrompt(prompt);
        }

        void OnDisable()
        {
            ShowPrompt("");
            if (Active) RestoreVillage();
        }
    }
}
