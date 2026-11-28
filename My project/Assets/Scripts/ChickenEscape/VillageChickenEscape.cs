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
        TextMesh entranceLabel;
        bool listenerWasEnabled, cursorWasVisible;
        string activityPrompt = "";
        CursorLockMode cursorLock;
        float timeBeforeEntry, nextEntry;
        int completedThisVisit;

        public bool Active => session != null;
        public bool Passed { get; private set; }

        void Awake()
        {
            preparedRun = yard.GetComponentInChildren<MarketEscapeRun>(true);
            entranceLabel = entrance.GetComponentInChildren<TextMesh>(true);
        }

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
            bool nearEntrance = (position - entrance.position).sqrMagnitude <= EntryRange * EntryRange;
            if (Passed)
            {
                ShowPrompt(nearDog || nearEntrance ? "Chicken Escape  ·  Passed" : "");
                return;
            }

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
            entrance.gameObject.SetActive(false);
            StartAttempt(false, untieFrom ?? entrance.position, untieFrom.HasValue);
        }

        internal void Retry(MarketEscapeRun run, bool skipBond)
        {
            if (session != run) return;
            RememberCompletion(run);
            ReplaceYard();
            Time.timeScale = 1f;
            StartAttempt(skipBond, skipBond ? null : entrance.position);
        }

        internal void Leave(MarketEscapeRun run)
        {
            if (session != run) return;
            RememberCompletion(run);
            Passed = completedThisVisit > 0;
            RestoreVillage();
            ReplaceYard();
            if (!Passed) return;

            preparedRun.ShowCompletedYard(completedThisVisit);
            if (entranceLabel)
            {
                entranceLabel.text = "PASSED";
                entranceLabel.color = new Color(.77f, 1f, .82f);
                entranceLabel.characterSize *= .84f;
            }
            Reward(completedThisVisit);
        }

        void RememberCompletion(MarketEscapeRun run)
        {
            if (run.Current == MarketEscapeRun.Phase.Won)
                completedThisVisit = Mathf.Max(completedThisVisit, run.Saved);
        }

        void StartAttempt(bool skipBond, Vector3? spawnPoint, bool untieDog = false)
        {
            session = preparedRun;
            session.AttachToVillage(this, skipBond, spawnPoint, untieDog);
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
            entrance.gameObject.SetActive(true);
            nextEntry = Time.unscaledTime + .55f;
        }

        void Reward(int saved)
        {
            var mess = player ? player.GetComponent<MessManager>() : null;
            int points = mess ? mess.Multiplied(saved * PointsPerHen) : 0;
            if (points > 0) mess.AddMess(points);
            string result = $"Chicken Escape passed  ·  {saved}/{preparedRun.Total} hens freed";
            GameHUD.Instance?.Toast(points > 0 ? $"{result}  ·  +{points} mess" : result, 4f);
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
