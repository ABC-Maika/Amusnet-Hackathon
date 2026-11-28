using Amusnet.Mess;
using Butterfly.Animals;
using Butterfly.NPC;
using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// Glue between the Sprint 4 systems (death, anger, allies) and the main game loop.
    ///  - MessManager score  -> AngerLevels.SetMess(score / goal) and GameHUD.SetMess
    ///  - RebelAnimal.AllyMess -> MessManager.AddMess (allies' chaos counts as mess)
    ///  - DeathManager.DeathFinished -> GameFlow.Lose()
    ///  - MessManager goal reached -> GameFlow.Win(); messes score only while GameFlow.IsRunning
    ///  - AngerLevels tier multiplier x ButterflySwarm brood multiplier -> MessManager.Multiplier and the HUD
    /// Created automatically after every scene load; nothing to place by hand.
    /// </summary>
    public sealed class GameIntegration : MonoBehaviour
    {
        static GameIntegration instance;
        MessManager mess;
        float allyRemainder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("[GameIntegration]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameIntegration>();
        }

        void OnEnable()
        {
            DeathManager.DeathFinished += OnDeathFinished;
            RebelAnimal.AllyMess += OnAllyMess;
        }

        void OnDisable()
        {
            DeathManager.DeathFinished -= OnDeathFinished;
            RebelAnimal.AllyMess -= OnAllyMess;
            Unhook();
        }

        void Update()
        {
            // Scenes reload on restart, so re-find the MessManager whenever it changes.
            if (mess == null)
            {
                mess = FindFirstObjectByType<MessManager>();
                if (mess == null) return;
                mess.ScoreChanged += OnScore;
                allyRemainder = 0f;
                OnScore(mess.Score);
            }
            // Each new mess is worth more when the village is angrier and the brood is bigger.
            float anger = AngerLevels.Instance != null ? AngerLevels.Instance.ScoreMultiplier : 1f;
            float brood = ButterflySwarm.Instance != null ? ButterflySwarm.Instance.Multiplier : 1f;
            mess.Multiplier = anger * brood;
            if (GameHUD.Instance != null) GameHUD.Instance.SetMultiplier(mess.Multiplier);

            // Messes only score during live play; the run clock starts on Play.
            var flow = GameFlow.Instance;
            if (flow == null) return;
            mess.Flow = flow;
            if (flow.IsRunning) mess.BeginRun();
        }

        void Unhook()
        {
            if (mess != null) mess.ScoreChanged -= OnScore;
            mess = null;
        }

        void OnScore(int score)
        {
            if (mess == null) return;
            float normalised = mess.GoalScore > 0 ? Mathf.Clamp01(score / (float)mess.GoalScore) : 0f;
            if (AngerLevels.Instance != null) AngerLevels.Instance.SetMess(normalised);
            if (GameHUD.Instance != null) GameHUD.Instance.SetMess(normalised);
            // Enough mess drives the villagers out: the run is won.
            // (Not while dying: the martyr's landing blast can push the score over, but the run is lost.)
            bool dying = DeathManager.Instance != null && DeathManager.Instance.IsDying;
            if (mess.HasReachedGoal && !dying && GameFlow.Instance != null && GameFlow.Instance.IsRunning) GameFlow.Instance.Win();
        }

        void OnAllyMess(RebelAnimal ally, float amount)
        {
            if (mess == null || amount <= 0f) return;
            if (GameFlow.Instance != null && !GameFlow.Instance.IsRunning) return;
            allyRemainder += amount * mess.Multiplier;
            int whole = Mathf.FloorToInt(allyRemainder);
            if (whole <= 0) return;
            allyRemainder -= whole;
            mess.AddMess(whole);
        }

        static void OnDeathFinished()
        {
            if (GameFlow.Instance != null) GameFlow.Instance.Lose();
        }
    }
}
