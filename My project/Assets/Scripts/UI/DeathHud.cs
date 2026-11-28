using System.Collections.Generic;
using Butterfly.Animals;
using Butterfly.Death;
using Butterfly.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace Butterfly.UI
{
    /// <summary>
    /// uGUI HUD: rebel legacy card, village anger bar, predator bird ring, warning banner with countdown,
    /// danger vignette, recruit toast and the Martyr's Last Flutter overlay. The pause menu (with the
    /// controls list) is a separate component, PauseMenu, on the same canvas.
    /// Built into the scene by Death > Add Anger and Predator UI (Editor/DeathHudBuilder.cs).
    /// </summary>
    public class DeathHud : MonoBehaviour
    {
        // ---------- palette ("forest lodge": deep green panels, engraved gold frames, amber numbers) ----------
        public static readonly Color Surface = new Color(0.055f, 0.17f, 0.12f, 0.93f);
        public static readonly Color SurfaceDeep = new Color(0.035f, 0.11f, 0.08f, 0.96f);
        public static readonly Color Track = new Color(0.02f, 0.07f, 0.05f, 0.92f);
        public static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
        public static readonly Color Muted = new Color(0.80f, 0.80f, 0.68f);
        public static readonly Color Gold = new Color(0.86f, 0.73f, 0.45f);
        public static readonly Color Amber = new Color(1f, 0.70f, 0.22f);
        public static readonly Color Leaf = new Color(0.66f, 0.80f, 0.56f);
        public static readonly Color Lilac = new Color(0.55f, 0.80f, 0.86f); // "hidden" teal
        public static readonly Color Monarch = Amber;
        public static readonly Color Ink = new Color(0.06f, 0.18f, 0.12f);   // text on light pills
        public static readonly Color Danger = new Color(0.95f, 0.30f, 0.22f);

        public static readonly Color[] TierColors =
        {
            new Color(0.66f, 0.80f, 0.56f), // Calm
            new Color(0.96f, 0.82f, 0.38f), // Annoyed
            new Color(1.00f, 0.60f, 0.20f), // Angry
            new Color(0.92f, 0.26f, 0.20f), // Furious
        };

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        [Header("Gameplay group (fades while dying)")]
        public CanvasGroup gameplayGroup;

        [Header("Anger")]
        public RectTransform angerFillRect;
        public Image angerFill;
        public RectTransform angerMarker;
        public Image angerTierPill;
        public Text angerTierText;
        public Text angerPercent;
        public Text angerTools;
        public Image[] angerTicks;

        [Header("Predator")]
        public RectTransform birdRoot;
        public Image birdRing;
        public Image birdDim;
        public Image birdFill;
        public Text birdPercent;
        public Image birdStatusPill;
        public Text birdStatus;

        [Header("Legacy")]
        public Text legacyText;
        public Text multiplierText;
        public Text alliesText;
        public Text martyrsText;
        [Tooltip("Single compact line: allies and the ally multiplier. Used instead of the three texts above.")]
        public Text statsText;

        [Header("Warning banner")]
        public CanvasGroup bannerGroup;
        public Image bannerAccent;
        public Image bannerIcon;
        public Text bannerText;
        public GameObject countdownRoot;
        public Image countdownRing;
        public Text countdownText;

        [Header("Danger vignette")]
        public Image vignette;

        [Header("Toast")]
        public CanvasGroup toastGroup;
        public Text toastText;

        [Header("Martyr overlay")]
        public CanvasGroup deathGroup;
        public Text deathTitle;
        public Text deathSub;
        public RectTransform steerFillRect;
        public CanvasGroup legacyPopupGroup;
        public Text legacyPopupText;

        [TextArea] public string landOnFlowerMessage = "You are tired!  Land on a flower, or the predator will catch you.";
        [TextArea] public string hideMessage = "HIDE NOW!  The predator is diving for you.";

        // ---------- shared procedural sprites (no art needed) ----------
        static Sprite white, rounded, vignetteSprite, ring, circle;

        public static Sprite White()
        {
            if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return white;
        }

        /// <summary>9-sliced rounded rectangle (radius 20 px). Use Image.Type.Sliced.</summary>
        public static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            const int size = 64, radius = 20;
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
                }
            tex.Apply();
            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }

        /// <summary>Transparent centre, soft edges.</summary>
        public static Sprite Vignette()
        {
            if (vignetteSprite != null) return vignetteSprite;
            const int size = 128;
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f, ny = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0.55f, 1.25f, d)));
                }
            tex.Apply();
            vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return vignetteSprite;
        }

        /// <summary>Anti-aliased ring for radial meters. Use Image.Type.Filled, Radial360.</summary>
        public static Sprite Ring()
        {
            if (ring != null) return ring;
            const int size = 256;
            const float outer = 127f, thickness = 22f;
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - (outer - thickness) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return ring;
        }

        /// <summary>Anti-aliased filled disc.</summary>
        public static Sprite Circle()
        {
            if (circle != null) return circle;
            const int size = 128;
            const float r = 63f;
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f)));
                }
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return circle;
        }

        static Texture2D NewTex(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        // ---------- state ----------
        bool dying;
        float popupLeft;
        float toastLeft;
        float shownAnger;
        string killerName = "";
        Amusnet.Mess.MessManager mess;

        void OnEnable()
        {
            DeathManager.DeathStarted += OnDeathStarted;
            DeathManager.Pooped += OnPooped;
            DeathManager.ScoreAwarded += OnScoreAwarded;
            DeathManager.DeathFinished += OnDeathFinished;
            RebelAnimal.Recruited += OnRecruited;
        }

        void OnDisable()
        {
            DeathManager.DeathStarted -= OnDeathStarted;
            DeathManager.Pooped -= OnPooped;
            DeathManager.ScoreAwarded -= OnScoreAwarded;
            DeathManager.DeathFinished -= OnDeathFinished;
            RebelAnimal.Recruited -= OnRecruited;
        }

        void OnDeathStarted(DeathCause cause)
        {
            dying = true;
            killerName = cause != null && !string.IsNullOrEmpty(cause.displayName) ? cause.displayName.ToLowerInvariant() : "predator";
            bool eaten = cause != null && cause.EatsVictim;
            if (deathTitle) deathTitle.text = eaten ? "GOBBLED UP!" : "CAUGHT!";
            if (deathSub) deathSub.text = eaten
                ? $"The {killerName} ate you.  Now it just has to... digest."
                : $"The {killerName} got you.";
            if (steerFillRect && steerFillRect.parent) steerFillRect.parent.gameObject.SetActive(false); // no steering any more
            if (legacyPopupGroup) legacyPopupGroup.alpha = 0f;
        }

        void OnPooped(DeathCause cause)
        {
            if (deathTitle) deathTitle.text = "...AND POOPED OUT";
            if (deathSub) deathSub.text = $"You're {killerName} poop now.  At least you'll make a mess.";
        }

        void OnScoreAwarded(int gained)
        {
            if (gained <= 0) return;
            if (legacyPopupText) legacyPopupText.text = $"SPLAT!  +{gained} SCORE";
            popupLeft = 3.5f;
        }

        void OnDeathFinished() { dying = false; }

        void OnRecruited(RebelAnimal a)
        {
            if (toastText) toastText.text = $"{a.animalName} joined the rebellion!  +×{a.MultiplierBonus:0.00}";
            toastLeft = 3f;
        }

        // ---------- update ----------
        void Update()
        {
            // HUD timers stop while the game is paused
            float dt = PauseMenu.IsPaused ? 0f : Time.unscaledDeltaTime;
            float uiDt = Time.unscaledDeltaTime;

            UpdateAnger(dt);
            UpdatePredator(dt);
            UpdateLegacy();
            UpdateToast(dt);
            UpdateDeath(dt);

            if (gameplayGroup)
            {
                // Hidden on the menu, during the win cinematic and on the end screens, like GameHUD.
                bool offPlay = GameFlow.Instance != null && !GameFlow.Instance.IsRunning;
                float target = PauseMenu.IsPaused || offPlay ? 0f : dying ? 0.12f : 1f;
                gameplayGroup.alpha = Mathf.MoveTowards(gameplayGroup.alpha, target, 4f * uiDt);
            }
        }

        void UpdateAnger(float dt)
        {
            var a = AngerLevels.Instance;
            if (a == null) return;

            shownAnger = Mathf.Lerp(shownAnger, a.Mess, 1f - Mathf.Exp(-8f * dt));
            Color tierColor = TierColors[Mathf.Clamp((int)a.Tier, 0, TierColors.Length - 1)];

            if (angerFillRect)
            {
                var max = angerFillRect.anchorMax;
                max.x = Mathf.Clamp01(shownAnger);
                angerFillRect.anchorMax = max;
            }
            if (angerMarker)
            {
                float x = Mathf.Clamp01(shownAnger);
                angerMarker.anchorMin = new Vector2(x, angerMarker.anchorMin.y);
                angerMarker.anchorMax = new Vector2(x, angerMarker.anchorMax.y);
            }
            if (angerFill)
            {
                angerFill.enabled = shownAnger > 0.005f;
                angerFill.color = tierColor;
            }
            if (angerTierPill) angerTierPill.color = tierColor;
            if (angerTierText) angerTierText.text = a.Tier.ToString().ToUpper();
            if (angerPercent) angerPercent.text = $"{Mathf.RoundToInt(a.Mess * 100)}%";
            if (angerTools) angerTools.text = ToolsText(a);

            if (angerTicks != null)
            {
                int i = 0;
                foreach (var t in a.tiers)
                {
                    if (t.threshold <= 0f) continue;
                    if (i < angerTicks.Length && angerTicks[i])
                        angerTicks[i].color = a.Mess >= t.threshold ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.35f);
                    i++;
                }
            }
        }

        static string ToolsText(AngerLevels a)
        {
            var list = new List<string>();
            if (a.Can(AngerTools.Swatter)) list.Add("swatters");
            if (a.Can(AngerTools.Net)) list.Add("nets");
            if (a.Can(AngerTools.Trap)) list.Add("traps");
            if (a.Can(AngerTools.CloseWindows)) list.Add("windows shut");
            if (a.Can(AngerTools.CloseGates)) list.Add("gates shut");
            return list.Count == 0 ? "Villagers are only grumbling." : "Villagers now use " + string.Join(" · ", list) + ".";
        }

        void UpdatePredator(float dt)
        {
            var p = PredatorMeter.Instance;
            if (p == null) return;

            float t = p.Percent / 100f;
            bool hideStage = p.CurrentStage == PredatorMeter.Stage.Hide;
            bool alertStage = p.CurrentStage == PredatorMeter.Stage.LandOnFlower;
            float pulse = PauseMenu.IsPaused ? 0f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);

            Color meter = Color.Lerp(Amber, Danger, Mathf.InverseLerp(0.4f, 0.9f, t));
            if (hideStage) meter = Color.Lerp(meter, Color.white, pulse * 0.5f);

            if (birdRing) { birdRing.fillAmount = t; birdRing.color = meter; }
            // the colour portrait rises from the bottom as the bird closes in, reddening near the end
            if (birdFill) { birdFill.fillAmount = t; birdFill.color = Color.Lerp(Color.white, new Color(1f, 0.62f, 0.55f), t * t); }
            if (birdRoot) birdRoot.localScale = Vector3.one * (hideStage ? 1f + 0.06f * pulse : 1f);
            if (birdPercent) birdPercent.text = $"{Mathf.RoundToInt(p.Percent)}%";

            string status; Color statusColor;
            if (hideStage) { status = "DANGER"; statusColor = Danger; }
            else if (alertStage) { status = "ALERT"; statusColor = Gold; }
            else if (p.IsHidden) { status = "HIDDEN"; statusColor = Lilac; }
            else { status = "SAFE"; statusColor = Leaf; }
            if (birdStatus) birdStatus.text = status;
            if (birdStatusPill) birdStatusPill.color = statusColor;

            // banner
            bool show = p.CurrentStage != PredatorMeter.Stage.Safe && !dying;
            Color accent = hideStage ? Danger : Gold;
            if (bannerText) bannerText.text = hideStage ? hideMessage : landOnFlowerMessage;
            if (bannerAccent) bannerAccent.color = accent;
            if (bannerIcon) bannerIcon.color = accent;
            if (countdownRoot) countdownRoot.SetActive(hideStage);
            if (hideStage)
            {
                float arrival = Mathf.Max(0.01f, p.arrivalSeconds);
                if (countdownRing) countdownRing.fillAmount = Mathf.Clamp01(p.CountdownLeft / arrival);
                if (countdownText)
                {
                    countdownText.text = Mathf.CeilToInt(Mathf.Max(0f, p.CountdownLeft)).ToString();
                    countdownText.color = Color.Lerp(Cream, Danger, pulse);
                }
            }
            if (bannerGroup) bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, show ? 1f : 0f, 4f * Time.unscaledDeltaTime);

            // danger vignette: red edges, pulsing in the hide stage
            if (vignette)
            {
                float target = PauseMenu.IsPaused || dying ? 0f
                             : hideStage ? 0.35f + 0.25f * pulse
                             : alertStage ? 0.12f : 0f;
                var c = vignette.color; c.a = Mathf.MoveTowards(c.a, target, 1.5f * Time.unscaledDeltaTime);
                vignette.color = c;
            }
        }

        void UpdateLegacy()
        {
            string amber = Hex(Amber);
            if (!mess) mess = FindFirstObjectByType<Amusnet.Mess.MessManager>();
            if (legacyText) legacyText.text = mess ? mess.Score.ToString() : "0";
            int allies = RebelAnimal.Allies.Count;
            float mult = mess ? mess.Multiplier : RebelAnimal.AllyMultiplier();
            if (statsText)
                statsText.text = $"Allies <color=#{amber}>{allies}</color>   ·   " +
                                 $"Multiplier <color=#{amber}>×{mult:0.00}</color>";
            if (multiplierText) multiplierText.text = $"Multiplier  <color=#{amber}><b>×{mult:0.00}</b></color>";
            if (alliesText) alliesText.text = $"Allies  <color=#{amber}><b>{allies}</b></color>";
            if (martyrsText) martyrsText.text = "";
        }

        void UpdateToast(float dt)
        {
            toastLeft -= dt;
            if (toastGroup) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, toastLeft > 0f ? 1f : 0f, 4f * Time.unscaledDeltaTime);
        }

        void UpdateDeath(float dt)
        {
            float uiDt = Time.unscaledDeltaTime;
            bool showOverlay = dying && !PauseMenu.IsPaused;
            if (deathGroup) deathGroup.alpha = Mathf.MoveTowards(deathGroup.alpha, showOverlay ? 1f : 0f, 5f * uiDt);

            popupLeft -= dt;
            if (legacyPopupGroup)
            {
                bool showPopup = popupLeft > 0f && !PauseMenu.IsPaused;
                legacyPopupGroup.alpha = Mathf.MoveTowards(legacyPopupGroup.alpha, showPopup ? 1f : 0f, 4f * uiDt);
            }
        }
    }
}
