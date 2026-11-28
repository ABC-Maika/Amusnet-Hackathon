using UnityEngine;
using UnityEngine.UIElements;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class EscapeHud : MonoBehaviour
    {
        [SerializeField] MarketEscapeRun run;
        [SerializeField] Camera view;

        VisualElement root, pips, backdrop, rescueProgress, stamina, staminaFill, promptLine, dogRisk, dogRiskFill;
        Label objective, progress, flock, role, clock, key, hint, details, guide, orchard, lane, keeper, decoy, title, result, dogRiskLabel;
        Button retry, menu;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            objective = root.Q<Label>("objective");
            rescueProgress = root.Q("rescue-progress");
            flock = root.Q<Label>("flock");
            progress = root.Q<Label>("progress");
            role = root.Q<Label>("role");
            clock = root.Q<Label>("clock");
            stamina = root.Q("stamina");
            staminaFill = root.Q("stamina-fill");
            dogRisk = root.Q("dog-risk");
            dogRiskFill = root.Q("dog-risk-fill");
            dogRiskLabel = root.Q<Label>("dog-risk-label");
            promptLine = root.Q("prompt-line");
            key = root.Q<Label>("prompt-key");
            hint = root.Q<Label>("prompt-text");
            details = root.Q<Label>("controls");
            guide = root.Q<Label>("guide");
            orchard = root.Q<Label>("orchard");
            lane = root.Q<Label>("lane");
            keeper = root.Q<Label>("keeper");
            decoy = root.Q<Label>("decoy");
            pips = root.Q("pips");
            backdrop = root.Q("end");
            title = root.Q<Label>("end-title");
            result = root.Q<Label>("end-subtitle");
            retry = root.Q<Button>("retry");
            menu = root.Q<Button>("menu");
            retry.clicked += PrimaryAction;
            menu.clicked += run.QuitToWorkspace;
            pips.Clear();
            for (int i = 0; i < run.Total; i++)
                pips.Add(new VisualElement());
        }

        void OnDisable()
        {
            if (retry != null) retry.clicked -= PrimaryAction;
            if (menu != null) menu.clicked -= run.QuitToWorkspace;
        }

        void PrimaryAction()
        {
            if (run.IsPaused) run.Pause(false);
            else run.Restart();
        }

        void LateUpdate()
        {
            if (progress == null) return;
            var phase = run.Current;
            bool inFarm = phase == MarketEscapeRun.Phase.Farm;
            bool finished = phase is MarketEscapeRun.Phase.Won or MarketEscapeRun.Phase.Lost;
            bool overlay = finished || run.IsPaused;
            Toggle(backdrop, overlay);
            backdrop.EnableInClassList("failed", phase == MarketEscapeRun.Phase.Lost);
            if (overlay) ShowOutcome();

            objective.text = phase == MarketEscapeRun.Phase.MeetDog ? "Free the farm dog" :
                phase == MarketEscapeRun.Phase.Trust ? "You earned his trust" :
                $"Rescue at least {MarketEscapeRun.RescueTarget} hens";
            progress.text = $"{run.Saved}/{run.Total} safe";
            flock.text = $"{run.Following}/{MarketEscapeRun.FlockLimit} in tow   ·   {run.Caught} caught";
            Toggle(rescueProgress, inFarm || finished);
            Toggle(flock, inFarm || finished);
            for (int i = 0; i < pips.childCount; i++)
            {
                bool saved = i < run.Saved;
                pips[i].EnableInClassList("saved", saved);
                pips[i].EnableInClassList("caught", i >= run.Total - run.Caught);
                pips[i].EnableInClassList("following", !saved && i < run.Saved + run.Following);
            }

            role.text = run.IsButterfly ? "Butterfly" : "Farm dog";
            clock.text = $"Lockdown  {FormatTime(run.Remaining)}";
            clock.EnableInClassList("urgent", run.Remaining <= 11f);
            Toggle(clock, inFarm && run.AlarmActive);
            Toggle(stamina, inFarm && !run.IsButterfly);
            staminaFill.style.width = Length.Percent(run.Stamina * 100f);
            staminaFill.EnableInClassList("spent", run.Stamina < .23f);
            Toggle(dogRisk, inFarm && (run.DogCaptured || run.Keeper.DogPressure > .01f));
            dogRiskLabel.text = run.DogCaptured ? $"Free the dog  {Mathf.CeilToInt(run.Keeper.DogRescueRemaining)}s" : "Dog within reach";
            dogRiskFill.style.width = Length.Percent(run.Keeper.DogPressure * 100f);

            Prompt(phase);
            var marker = run.PrimaryMarker();
            guide.text = phase == MarketEscapeRun.Phase.MeetDog ? "Dog’s leash" :
                run.DogCaptured ? "Dog caught" : run.InsideLatchSide ? "Latch" : "Blue gap";
            Point(guide, marker, !overlay && marker != Vector3.zero);
            Point(orchard, run.OrchardMarker, inFarm && !overlay);
            Point(lane, run.LaneMarker, inFarm && !overlay);
            Point(decoy, run.DecoyMarker, inFarm && !overlay && run.DecoyActive, false);
            ShowKeeper(inFarm && !overlay);
        }

        void ShowOutcome()
        {
            if (run.IsPaused)
            {
                title.text = "Paused";
                result.text = $"{run.Saved}/{run.Total} rescued   ·   {run.Caught} caught";
                retry.text = "Resume";
                menu.text = run.IsHosted ? "Return to village" : "Back to menu";
                return;
            }

            title.text = run.Current == MarketEscapeRun.Phase.Lost ? "Mission failed" :
                run.Saved == run.Total ? "Clean escape" : "Partial escape";
            var ending = run.FinishedBy switch
            {
                MarketEscapeRun.Finish.Lockdown => "The yard locked down.",
                MarketEscapeRun.Finish.TooManyCaught => "The keeper caught too many hens.",
                MarketEscapeRun.Finish.DogSecured => "The keeper caught the dog.",
                MarketEscapeRun.Finish.Left when run.Current == MarketEscapeRun.Phase.Lost => "You left before enough hens got out.",
                _ => ""
            };
            string tally = $"{run.Saved}/{run.Total} rescued   ·   {run.Caught} caught";
            result.text = ending.Length > 0 ? $"{tally}\n{ending}" : tally;
            retry.text = "Retry";
            menu.text = run.IsHosted ? "Return to village" : "Close";
        }

        void ShowKeeper(bool shown)
        {
            var guard = run.Keeper;
            keeper.text = guard.Current switch
            {
                EscapeKeeper.PatrolState.Investigating => "Heard a bark",
                EscapeKeeper.PatrolState.Distracted => "Distracted",
                EscapeKeeper.PatrolState.Pursuing => "Spotted",
                EscapeKeeper.PatrolState.ClosingGate => "Closing gate",
                EscapeKeeper.PatrolState.Windup => "About to grab!",
                EscapeKeeper.PatrolState.ScoopingHen => "Taking a hen",
                EscapeKeeper.PatrolState.CarryingHen => "Carrying a hen",
                EscapeKeeper.PatrolState.PuttingHenDown => "Penning a hen",
                _ => ""
            };
            Point(keeper, guard.transform.position + Vector3.up * 1.9f, shown && keeper.text.Length > 0, false);
        }

        void Prompt(MarketEscapeRun.Phase phase)
        {
            details.text = phase switch
            {
                MarketEscapeRun.Phase.MeetDog => "WASD fly   ·   Space rise   ·   Shift descend",
                MarketEscapeRun.Phase.Trust => "",
                _ when run.DogCaptured => "WASD fly   ·   Space rise   ·   Shift descend",
                _ when run.IsButterfly => $"WASD fly   ·   Space rise   ·   Shift descend   ·   F swap   ·   {GustLabel()}",
                _ => $"WASD move   ·   Shift sprint   ·   {BarkLabel()}   ·   F swap"
            };
            (string control, string message) = phase switch
            {
                MarketEscapeRun.Phase.MeetDog when run.CanUntieDog => ("E", "Untie the dog"),
                MarketEscapeRun.Phase.MeetDog => ("", "The keeper tied up the dog. Fly over to free him."),
                MarketEscapeRun.Phase.Trust => ("", "He lowers his head and lets you land."),
                MarketEscapeRun.Phase.Farm when run.CanLeave => ("E", $"Leave with {run.Saved} rescued"),
                MarketEscapeRun.Phase.Farm when run.CanFreeDog => ("E", "Gust at the keeper to free the dog"),
                MarketEscapeRun.Phase.Farm when run.DogCaptured => ("", "Fly to the dog before the keeper secures him."),
                MarketEscapeRun.Phase.Farm when run.CanUseLatch => ("E", "Unlatch gate"),
                MarketEscapeRun.Phase.Farm when run.JustCaught => ("", "The keeper caught a hen."),
                MarketEscapeRun.Phase.Farm when run.Keeper.DogPressure > .15f => ("", "The keeper is reaching for the dog."),
                MarketEscapeRun.Phase.Farm when !run.IsButterfly && run.Dragging => ("Q", "Drop cloth"),
                MarketEscapeRun.Phase.Farm when !run.IsButterfly && run.CanGrabLaundry => ("Q", "Drag cloth"),
                _ => ("", "")
            };
            key.text = control;
            hint.text = message;
            Toggle(key, control.Length > 0);
            Toggle(promptLine, message.Length > 0);
        }

        string GustLabel() => run.GustRemaining <= 0f ? "E gust decoy" : $"Decoy {Mathf.CeilToInt(run.GustRemaining)}s";
        string BarkLabel() => run.BarkCharge >= .99f ? "Space rally" : "Bark recovering";

        void Point(Label element, Vector3 point, bool shown, bool clamp = true)
        {
            if (!shown || root.layout.width <= 1f || root.layout.height <= 1f)
            {
                Toggle(element, false);
                return;
            }
            var anchor = view.WorldToViewportPoint(point);
            if (!clamp && (anchor.z <= 0f || anchor.x is < .06f or > .94f || anchor.y is < .12f or > .91f))
            {
                Toggle(element, false);
                return;
            }
            Toggle(element, true);
            float width = root.layout.width, height = root.layout.height;
            float x = anchor.z < 0f ? 1f - anchor.x : anchor.x;
            float y = anchor.z < 0f ? anchor.y : 1f - anchor.y;
            element.style.left = Mathf.Clamp(x * width, 78f, width - 78f);
            element.style.top = Mathf.Clamp(y * height, 97f, height - 145f);
        }

        static string FormatTime(float time)
        {
            int seconds = Mathf.CeilToInt(time);
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        static void Toggle(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
