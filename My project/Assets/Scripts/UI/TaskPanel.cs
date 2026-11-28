using System;
using System.Collections.Generic;
using Butterfly.Animals;
using Butterfly.Death;
using Butterfly.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace Butterfly.UI
{
    /// <summary>
    /// "Tasks" card on the HUD. Any system can post objectives:
    ///   TaskPanel.Set("laundry", "Drop the laundry in the square", 0, 3);
    ///   TaskPanel.Progress("laundry", 2);   TaskPanel.Complete("laundry");   TaskPanel.Remove("laundry");
    /// Starter tasks (startTasks) can track existing systems automatically (allies, anger tier, hiding, survival).
    /// Finished tasks light up, then leave the list after a short delay.
    /// Built by Death > Add Anger and Predator UI (Editor/DeathHudBuilder.cs).
    /// </summary>
    public class TaskPanel : MonoBehaviour
    {
        public enum Tracker { Manual, RecruitAllies, ReachAngerTier, HideFromPredator, SurviveSeconds }

        [Serializable]
        public class TaskDef
        {
            public string id;
            public string text;
            public Tracker tracker = Tracker.Manual;
            [Min(1)] public int target = 1;
        }

        public static TaskPanel Instance { get; private set; }

        [Header("Layout (set by the builder)")]
        public RectTransform list;
        public GameObject rowTemplate;
        public CanvasGroup group;

        [Header("Starter tasks")]
        public List<TaskDef> startTasks = new List<TaskDef>();
        [Tooltip("Seconds a finished task stays visible before it leaves the list.")]
        public float doneLinger = 4f;

        class Row
        {
            public TaskDef def;
            public GameObject go;
            public Image box, check;
            public Text label, progress;
            public int value;
            public bool done;
            public float doneAt;
            public float flash;
        }

        readonly List<Row> rows = new List<Row>();
        float survived;

        static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
        static readonly Color Muted = new Color(0.62f, 0.66f, 0.56f);
        static readonly Color Amber = new Color(1f, 0.70f, 0.22f);

        void Awake()
        {
            // The map scene may carry its own copy for standalone testing: the game's panel stays the one.
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (rowTemplate) rowTemplate.SetActive(false);
            foreach (var d in startTasks) Add(d);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------- static API ----------

        public static void Set(string id, string text, int progress = 0, int target = 1)
        {
            if (Instance == null) return;
            var r = Instance.Find(id);
            if (r == null) r = Instance.Add(new TaskDef { id = id, text = text, target = Mathf.Max(1, target) });
            r.def.text = text;
            r.def.target = Mathf.Max(1, target);
            Instance.SetValue(r, progress);
        }

        public static void Progress(string id, int value)
        {
            var r = Instance != null ? Instance.Find(id) : null;
            if (r != null) Instance.SetValue(r, value);
        }

        public static void Complete(string id)
        {
            var r = Instance != null ? Instance.Find(id) : null;
            if (r != null) Instance.SetValue(r, r.def.target);
        }

        public static void Remove(string id)
        {
            var r = Instance != null ? Instance.Find(id) : null;
            if (r == null) return;
            Destroy(r.go);
            Instance.rows.Remove(r);
        }

        // ---------- internals ----------

        Row Find(string id) => rows.Find(r => r.def.id == id);

        Row Add(TaskDef def)
        {
            if (rowTemplate == null || list == null) return null;
            var go = Instantiate(rowTemplate, list);
            go.name = "Task_" + def.id;
            go.SetActive(true);
            var r = new Row
            {
                def = new TaskDef { id = def.id, text = def.text, tracker = def.tracker, target = Mathf.Max(1, def.target) },
                go = go,
                box = go.transform.Find("Box")?.GetComponent<Image>(),
                check = go.transform.Find("Box/Check")?.GetComponent<Image>(),
                label = go.transform.Find("Label")?.GetComponent<Text>(),
                progress = go.transform.Find("Progress")?.GetComponent<Text>(),
            };
            rows.Add(r);
            Refresh(r);
            return r;
        }

        void SetValue(Row r, int value)
        {
            if (r.done) return;
            r.value = Mathf.Clamp(value, 0, r.def.target);
            if (r.value >= r.def.target)
            {
                r.done = true;
                r.doneAt = Time.unscaledTime;
                r.flash = 1f;
            }
            Refresh(r);
        }

        void Refresh(Row r)
        {
            if (r.label)
            {
                r.label.text = r.def.text;
                r.label.color = r.done ? Muted : Cream;
            }
            if (r.progress)
            {
                r.progress.text = r.def.target > 1 ? $"{r.value}/{r.def.target}" : r.done ? "done" : "";
                r.progress.color = r.done ? Muted : Amber;
            }
            if (r.check) r.check.enabled = r.done;
        }

        void Update()
        {
            if (PauseMenu.IsPaused) return;
            survived += Time.deltaTime;

            foreach (var r in rows.ToArray())
            {
                if (!r.done) Track(r);

                if (r.flash > 0f)
                {
                    r.flash = Mathf.Max(0f, r.flash - Time.unscaledDeltaTime * 1.5f);
                    if (r.box) r.box.transform.localScale = Vector3.one * (1f + 0.35f * r.flash);
                }
                if (r.done && Time.unscaledTime - r.doneAt > doneLinger)
                {
                    var cg = r.go.GetComponent<CanvasGroup>();
                    if (cg == null) cg = r.go.AddComponent<CanvasGroup>();
                    cg.alpha = Mathf.MoveTowards(cg.alpha, 0f, Time.unscaledDeltaTime * 2f);
                    if (cg.alpha <= 0f) { Destroy(r.go); rows.Remove(r); }
                }
            }

            if (group) group.alpha = Mathf.MoveTowards(group.alpha, rows.Count > 0 ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        }

        void Track(Row r)
        {
            switch (r.def.tracker)
            {
                case Tracker.RecruitAllies:
                    SetValue(r, RebelAnimal.Allies.Count);
                    break;
                case Tracker.ReachAngerTier:
                    if (AngerLevels.Instance != null) SetValue(r, (int)AngerLevels.Instance.Tier);
                    break;
                case Tracker.HideFromPredator:
                    if (PredatorMeter.Instance != null && PredatorMeter.Instance.IsHidden) SetValue(r, r.def.target);
                    break;
                case Tracker.SurviveSeconds:
                    SetValue(r, Mathf.FloorToInt(survived));
                    break;
            }
        }
    }
}
