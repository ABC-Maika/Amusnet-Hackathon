using UnityEngine;
using UnityEngine.UIElements;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class EscapeHud : MonoBehaviour
    {
        [SerializeField] MarketEscapeRun run;
        [SerializeField] Camera view;
        VisualElement root, pips, backdrop;
        Label progress, role, clock, key, hint, details, spot, orchard, lane, title, result;
        Button retry, menu;
        int shownSaved = -1;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            progress = root.Q<Label>("progress");
            role = root.Q<Label>("role");
            clock = root.Q<Label>("clock");
            key = root.Q<Label>("prompt-key");
            hint = root.Q<Label>("prompt-text");
            details = root.Q<Label>("controls");
            spot = root.Q<Label>("guide");
            orchard = root.Q<Label>("orchard");
            lane = root.Q<Label>("lane");
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
                pips.Add(new VisualElement { name = $"hen-{i}" });
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
            bool finished = phase is MarketEscapeRun.Phase.Won or MarketEscapeRun.Phase.Lost;
            bool overlay = finished || run.IsPaused;
            Toggle(backdrop, overlay);
            if (overlay)
            {
                title.text = run.IsPaused ? "Paused" : phase == MarketEscapeRun.Phase.Won ? "The flock is free" : "The farm is closing";
                result.text = run.IsPaused ? $"{run.Saved} of {run.Total} chickens safe" :
                    phase == MarketEscapeRun.Phase.Won ? $"{run.Saved} saved  ·  {FormatTime(run.Elapsed)}  ·  {run.OrchardSaved} via orchard" :
                    $"{run.Saved} of {run.Total} chickens made it out";
                retry.text = run.IsPaused ? "Resume" : "Play again";
            }
            if (shownSaved != run.Saved)
            {
                shownSaved = run.Saved;
                progress.text = $"{run.Saved} / {run.Total}";
                for (int i = 0; i < pips.childCount; i++) pips[i].EnableInClassList("saved", i < run.Saved);
            }
            role.text = run.IsButterfly ? "Butterfly" : "Farm dog";
            clock.text = FormatTime(run.Remaining);
            clock.EnableInClassList("urgent", run.Remaining < 24f);
            Toggle(clock, phase == MarketEscapeRun.Phase.Chase);
            Prompt(phase);
            var marker = run.PrimaryMarker();
            Point(spot, marker, !overlay && marker != Vector3.zero);
            spot.text = phase == MarketEscapeRun.Phase.FirstHen ? "Loose hen" :
                phase == MarketEscapeRun.Phase.BackToDog ? "Farm dog" :
                phase == MarketEscapeRun.Phase.OpenPen && !run.IsButterfly ? "Butterfly" :
                run.ActorPosition.z > 2.24f || phase == MarketEscapeRun.Phase.Chase ? "Latch" : "Blue gap";
            Point(orchard, run.OrchardMarker, !overlay && phase is MarketEscapeRun.Phase.FirstHen or MarketEscapeRun.Phase.Chase);
            Point(lane, run.LaneMarker, !overlay && phase == MarketEscapeRun.Phase.Chase);
        }

        void Prompt(MarketEscapeRun.Phase phase)
        {
            details.text = run.IsButterfly
                ? "WASD fly   ·   Space rise   ·   Shift descend   ·   F swap"
                : phase == MarketEscapeRun.Phase.FirstHen ? "Shift sprint" :
                  phase == MarketEscapeRun.Phase.Chase ? "WASD run   ·   Shift sprint   ·   Space bark   ·   F swap" :
                  "WASD run   ·   Shift sprint";
            if (!run.IsButterfly && phase == MarketEscapeRun.Phase.Chase)
                details.text = run.CanGrabLaundry ? (run.Dragging ? "Q drop cloth" : "Q grab cloth") + "   ·   " + details.text :
                    details.text + (run.BarkCharge < .99f ? "   ·   Bark recovering" : "");

            (string control, string message) = phase switch
            {
                MarketEscapeRun.Phase.FirstHen => ("WASD", "Run behind the hen and guide it to the orchard gap."),
                MarketEscapeRun.Phase.OpenPen when !run.IsButterfly => ("F", "Switch to the butterfly to get inside the pen."),
                MarketEscapeRun.Phase.OpenPen when run.CanUseLatch => ("E", "Gust the latch to open the pen."),
                MarketEscapeRun.Phase.OpenPen when run.ActorPosition.z > 2.24f => ("WASD", "Fly up to the latch."),
                MarketEscapeRun.Phase.OpenPen => ("WASD", "Fly through the blue gap to reach the latch."),
                MarketEscapeRun.Phase.BackToDog => ("F", "Switch back to the dog and move the flock."),
                MarketEscapeRun.Phase.Chase when run.CanUseLatch => ("E", "Gust the latch to open the pen again."),
                MarketEscapeRun.Phase.Chase when run.GateNeedsOpening && !run.IsButterfly => ("F", "The keeper shut the gate. Fly in and reopen it."),
                MarketEscapeRun.Phase.Chase when run.GateNeedsOpening => ("WASD", "Go inside the pen to reach the latch."),
                MarketEscapeRun.Phase.Chase when run.IsButterfly => ("F", "Switch back to the dog to move the flock."),
                MarketEscapeRun.Phase.Chase when run.KeeperClosing => ("SPACE", "The keeper is coming to close the gate. Bark to draw her away."),
                MarketEscapeRun.Phase.Chase when run.DogStopped => ("", "The keeper cut you off. Slip around her."),
                MarketEscapeRun.Phase.Chase when run.JustRescued => ("", "Chicken safe."),
                MarketEscapeRun.Phase.Chase when run.Dragging => ("Q", "Drop the cloth in the keeper’s path to make her go around."),
                MarketEscapeRun.Phase.Chase when run.CanGrabLaundry => ("Q", "Drag the cloth to set up a detour for the keeper."),
                MarketEscapeRun.Phase.Chase => ("SPACE", "Face the way you want the hens to run and bark."),
                _ => ("", "")
            };
            key.text = control;
            Toggle(key, control.Length != 0);
            hint.text = message;
        }

        void Point(Label element, Vector3 point, bool shown)
        {
            Toggle(element, shown);
            if (!shown || root.layout.width <= 1 || root.layout.height <= 1) return;
            var anchor = view.WorldToViewportPoint(point);
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
