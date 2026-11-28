using Butterfly.Death;
using Butterfly.NPC;
using Butterfly.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Menu: Death > Add Anger and Predator UI. (Re)builds the whole HUD canvas in the "forest lodge" style
/// (deep-green chamfered panels, engraved gold frames, serif type, amber numbers): legacy card with butterfly
/// emblem, village anger bar, predator dial, warning banner, toast, martyr overlay and the pause menu with its
/// Controls page. Makes sure AngerLevels, PredatorMeter, the predator bird and an EventSystem exist, then saves.
/// Safe to run again: it replaces the old canvas and regenerates the sprites (Editor/HudArt.cs).
/// </summary>
public static class DeathHudBuilder
{
    const string HeadPath = "Assets/UI/Predator/BirdHead.png";
    const string BirdModel = "Assets/Art/Flycatcher/Flycatcher.fbx";
    const string BirdController = "Assets/Art/Flycatcher/Flycatcher.controller";
    const string SerifPath = "Assets/UI/Fonts/LibreBaskerville.ttf";
    const string SerifItalicPath = "Assets/UI/Fonts/LibreBaskerville-Italic.ttf";
    const string GenDir = "Assets/UI/Generated";

    static Sprite WhiteS, VigS, PanelS, FrameS, PillS, PillFrameS, DialS, ArcS, DiscS, NeedleS, ButterflyS;
    static Font Serif, SerifItalic;

    // ---------- menu ----------

    [MenuItem("Death/Add Anger and Predator UI")]
    static void Build()
    {
        // procedural sprites must be real assets, otherwise the saved scene loses them
        WhiteS = SaveSprite("White", DeathHud.White(), Vector4.zero);
        VigS = SaveSprite("Vignette", DeathHud.Vignette(), Vector4.zero);
        PanelS = HudArt.PanelFill();
        FrameS = HudArt.PanelFrame();
        PillS = HudArt.PillFill();
        PillFrameS = HudArt.PillFrame();
        DialS = HudArt.Dial();
        ArcS = HudArt.Arc();
        DiscS = HudArt.Disc();
        NeedleS = HudArt.Needle();
        ButterflyS = HudArt.Butterfly();

        var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Serif = AssetDatabase.LoadAssetAtPath<Font>(SerifPath) ?? fallback;
        SerifItalic = AssetDatabase.LoadAssetAtPath<Font>(SerifItalicPath) ?? Serif;

        var old = Object.FindFirstObjectByType<DeathHud>();
        if (old != null) Object.DestroyImmediate(old.gameObject);
        foreach (var legacyHud in Object.FindObjectsByType<DeathTestHud>(FindObjectsSortMode.None))
            Object.DestroyImmediate(legacyHud); // old always-on controls text, now in the pause menu

        var imp = (TextureImporter)AssetImporter.GetAtPath(HeadPath);
        if (imp != null && imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        var head = AssetDatabase.LoadAssetAtPath<Sprite>(HeadPath);

        EnsureLogic();
        EnsureBird();
        EnsureEventSystem();

        // ---- canvas ----
        var canvasGo = new GameObject("DeathCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var hud = canvasGo.AddComponent<DeathHud>();
        var root = canvasGo.GetComponent<RectTransform>();

        hud.vignette = Img(Stretch("DangerVignette", root), VigS, new Color(0.85f, 0.05f, 0.05f, 0f));

        var gameplay = Stretch("Gameplay", root);
        hud.gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
        hud.gameplayGroup.blocksRaycasts = false;

        BuildLegacyCard(gameplay, hud);
        BuildTasks(gameplay);
        BuildAngerBar(gameplay, hud);
        BuildPredator(gameplay, hud, head);
        BuildBanner(gameplay, hud);
        BuildToast(gameplay, hud);
        BuildPauseHint(gameplay);
        BuildDeathOverlay(root, hud);
        BuildPauseMenu(root);

        Undo.RegisterCreatedObjectUndo(canvasGo, "Add death HUD");
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        EditorSceneManager.SaveScene(canvasGo.scene);
        Debug.Log("[Death] HUD built. Esc pauses (controls are in the pause menu). [ and ] change anger, hold H to hide, K to die.");
    }

    static Sprite SaveSprite(string name, Sprite src, Vector4 border)
    {
        string path = $"{GenDir}/{name}.png";
        if (!System.IO.Directory.Exists(GenDir)) System.IO.Directory.CreateDirectory(GenDir);
        if (!System.IO.File.Exists(path))
        {
            var srcTex = src.texture;
            var tmp = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
            var rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(srcTex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tmp.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(path, tmp.EncodeToPNG());
            Object.DestroyImmediate(tmp);
            AssetDatabase.ImportAsset(path);
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteBorder = border;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- scene logic ----------

    static void EnsureLogic()
    {
        if (Object.FindFirstObjectByType<AngerLevels>() == null)
        {
            var go = new GameObject("AngerLevels");
            go.AddComponent<AngerLevels>();
            go.AddComponent<AngerDebugDriver>();
        }
        if (Object.FindFirstObjectByType<PredatorMeter>() == null)
            new GameObject("PredatorMeter").AddComponent<PredatorMeter>();
    }

    static void EnsureEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null) Object.DestroyImmediate(legacy);
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    static void EnsureBird()
    {
        if (Object.FindFirstObjectByType<PredatorBird>() != null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BirdModel);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BirdController);
        if (prefab == null)
        {
            Debug.LogWarning("[Death] " + BirdModel + " not found, the predator bird was not added.");
            return;
        }

        var rootGo = new GameObject("PredatorBird");
        rootGo.transform.position = new Vector3(0f, 35f, 0f);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rootGo.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        var anim = model.GetComponentInChildren<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        var bird = rootGo.AddComponent<PredatorBird>();
        bird.model = model.transform;
        bird.animator = anim;
    }

    // ---------- HUD ----------

    static void BuildLegacyCard(RectTransform parent, DeathHud hud)
    {
        // compact: emblem | caption + total, one stats line underneath
        var p = Framed("LegacyCard", parent, TL, TL, new Vector2(24, -24), new Vector2(300, 92));

        var emblem = Child("Emblem", p);
        Place(emblem, TL, TL, new Vector2(14, -10), new Vector2(50, 44));
        Img(emblem, ButterflyS, DeathHud.Gold).preserveAspect = true;

        Label("Caption", p, "REBEL LEGACY", 14, FontStyle.Normal, DeathHud.Muted, TextAnchor.MiddleLeft, TL, TL, new Vector2(76, -10), new Vector2(210, 18));
        hud.legacyText = Label("Total", p, "0", 30, FontStyle.Bold, DeathHud.Amber, TextAnchor.MiddleLeft, TL, TL, new Vector2(74, -26), new Vector2(210, 34));

        hud.statsText = Label("Stats", p, "", 13, FontStyle.Normal, DeathHud.Cream, TextAnchor.MiddleCenter, TL, TL, new Vector2(10, -64), new Vector2(280, 20));
        hud.statsText.supportRichText = true;
    }

    /// <summary>Tasks card under the legacy card. Grows with the number of tasks (layout groups).</summary>
    static void BuildTasks(RectTransform parent)
    {
        var p = Framed("TasksCard", parent, TL, TL, new Vector2(24, -126), new Vector2(300, 60));
        p.Find("Frame").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var vlg = p.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 8, 12);
        vlg.spacing = 2;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fit = p.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var panel = p.gameObject.AddComponent<TaskPanel>();
        panel.group = p.gameObject.AddComponent<CanvasGroup>();
        panel.group.blocksRaycasts = false;

        // header: title + rule
        var header = Child("Header", p);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
        Label("Title", header, "TASKS", 14, FontStyle.Normal, DeathHud.Muted, TextAnchor.MiddleLeft, TL, TL, new Vector2(0, 0), new Vector2(200, 20));
        var rule = Child("Rule", header);
        rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0);
        rule.anchoredPosition = new Vector2(0, 3); rule.sizeDelta = new Vector2(0, 1f);
        Img(rule, WhiteS, new Color(DeathHud.Gold.r, DeathHud.Gold.g, DeathHud.Gold.b, 0.65f));

        // task list
        var list = Child("List", p);
        var lv = list.gameObject.AddComponent<VerticalLayoutGroup>();
        lv.spacing = 2;
        lv.childControlWidth = true; lv.childControlHeight = true;
        lv.childForceExpandWidth = true; lv.childForceExpandHeight = false;
        panel.list = list;

        // row template (cloned at runtime)
        var row = Child("RowTemplate", list);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

        var box = Child("Box", row);
        Place(box, ML, MC, new Vector2(6, 0), new Vector2(10, 10));
        box.localRotation = Quaternion.Euler(0, 0, 45);
        var boxImg = Img(box, PillFrameS, DeathHud.Gold);
        boxImg.type = Image.Type.Sliced; boxImg.pixelsPerUnitMultiplier = 4f;
        var check = Stretch("Check", box);
        check.offsetMin = new Vector2(2, 2); check.offsetMax = new Vector2(-2, -2);
        var checkImg = Img(check, WhiteS, DeathHud.Amber);
        checkImg.enabled = false;

        var label = Label("Label", row, "Task", 14, FontStyle.Normal, DeathHud.Cream, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
        label.rectTransform.offsetMin = new Vector2(20, 0); label.rectTransform.offsetMax = new Vector2(-50, 0);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 11; label.resizeTextMaxSize = 14;

        var prog = Label("Progress", row, "", 14, FontStyle.Bold, DeathHud.Amber, TextAnchor.MiddleRight, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
        prog.rectTransform.anchorMin = new Vector2(1, 0); prog.rectTransform.pivot = new Vector2(1, 0.5f);
        prog.rectTransform.offsetMin = new Vector2(-48, 0); prog.rectTransform.offsetMax = Vector2.zero;
        panel.rowTemplate = row.gameObject;
        row.gameObject.SetActive(false);

        // starter tasks that track existing systems; other systems add theirs with TaskPanel.Set(...)
        panel.startTasks = new System.Collections.Generic.List<TaskPanel.TaskDef>
        {
            new TaskPanel.TaskDef { id = "recruit", text = "Recruit an animal ally", tracker = TaskPanel.Tracker.RecruitAllies, target = 1 },
            new TaskPanel.TaskDef { id = "annoy", text = "Make the villagers annoyed", tracker = TaskPanel.Tracker.ReachAngerTier, target = 1 },
            new TaskPanel.TaskDef { id = "hide", text = "Hide from the predator", tracker = TaskPanel.Tracker.HideFromPredator, target = 1 },
            new TaskPanel.TaskDef { id = "survive", text = "Stay alive for 60 seconds", tracker = TaskPanel.Tracker.SurviveSeconds, target = 60 },
        };
    }

    static void BuildAngerBar(RectTransform parent, DeathHud hud)
    {
        // compact: one header row (title · percent · tier) and a slim bar
        var p = Framed("AngerCard", parent, TC, TC, new Vector2(0, -24), new Vector2(480, 66));

        Label("Caption", p, "VILLAGE ANGER", 14, FontStyle.Normal, DeathHud.Muted, TextAnchor.MiddleLeft, TL, TL, new Vector2(20, -10), new Vector2(200, 20));
        hud.angerPercent = Label("Percent", p, "0%", 16, FontStyle.Bold, DeathHud.Amber, TextAnchor.MiddleRight, TR, TR, new Vector2(-104, -9), new Vector2(60, 22));

        var pill = Pill("TierPill", p, TR, TR, new Vector2(-20, -10), new Vector2(78, 20), DeathHud.TierColors[0], 2f);
        hud.angerTierPill = pill.GetComponent<Image>();
        hud.angerTierText = Label("Tier", pill, "CALM", 11, FontStyle.Bold, DeathHud.Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);

        // slim track with faint tier zones, fill, dividers and a needle at the fill end
        var track = Pill("Track", p, TC, TC, new Vector2(0, -38), new Vector2(440, 14), DeathHud.Track, 3f);
        var inner = Stretch("Inner", track);
        inner.offsetMin = new Vector2(3, 3); inner.offsetMax = new Vector2(-3, -3);

        var levels = Object.FindFirstObjectByType<AngerLevels>();
        var tiers = levels.tiers;
        tiers.Sort((a, b) => a.threshold.CompareTo(b.threshold));
        for (int i = 0; i < tiers.Count; i++)
        {
            float from = tiers[i].threshold, to = i + 1 < tiers.Count ? tiers[i + 1].threshold : 1f;
            var zone = Stretch("Zone_" + tiers[i].tier, inner);
            zone.anchorMin = new Vector2(from, 0); zone.anchorMax = new Vector2(to, 1);
            var c = DeathHud.TierColors[Mathf.Clamp((int)tiers[i].tier, 0, 3)]; c.a = 0.10f;
            Img(zone, WhiteS, c);
        }

        var fill = Stretch("Fill", inner);
        fill.anchorMax = new Vector2(0f, 1f);
        var fillImg = Img(fill, PillS, DeathHud.TierColors[0]);
        fillImg.type = Image.Type.Sliced;
        fillImg.pixelsPerUnitMultiplier = 6f;
        hud.angerFillRect = fill;
        hud.angerFill = fillImg;

        var ticks = new System.Collections.Generic.List<Image>();
        foreach (var t in tiers)
        {
            if (t.threshold <= 0f) continue;
            var tick = Stretch("Tick_" + t.tier, inner);
            tick.anchorMin = new Vector2(t.threshold, 0); tick.anchorMax = new Vector2(t.threshold, 1);
            tick.offsetMin = new Vector2(-0.75f, -3); tick.offsetMax = new Vector2(0.75f, 3);
            ticks.Add(Img(tick, WhiteS, new Color(1f, 1f, 1f, 0.35f)));
        }
        hud.angerTicks = ticks.ToArray();

        var needle = Child("Needle", inner);
        needle.anchorMin = new Vector2(0, 0.5f); needle.anchorMax = new Vector2(0, 0.5f);
        needle.pivot = new Vector2(0.25f, 0.5f);
        needle.sizeDelta = new Vector2(13, 20);
        Img(needle, NeedleS, DeathHud.Cream);
        hud.angerMarker = needle;
        hud.angerTools = null; // dropped for a cleaner bar; the tier pill already says how angry they are
    }

    static void BuildPredator(RectTransform parent, DeathHud hud, Sprite head)
    {
        // compact: just the dial (bird + percent inside) and a small status pill under it
        var p = Framed("PredatorCard", parent, TR, TR, new Vector2(-24, -24), new Vector2(136, 160));
        hud.birdRoot = p;

        Label("Caption", p, "PREDATOR", 12, FontStyle.Normal, DeathHud.Muted, TextAnchor.MiddleCenter, TC, TC, new Vector2(0, -8), new Vector2(130, 16));

        var gauge = Child("Gauge", p);
        Place(gauge, TC, TC, new Vector2(0, -24), new Vector2(104, 104));

        var disc = Stretch("Disc", gauge);
        disc.offsetMin = new Vector2(13, 13); disc.offsetMax = new Vector2(-13, -13);
        Img(disc, DiscS, DeathHud.SurfaceDeep);

        var headRt = Stretch("Head", gauge);
        headRt.offsetMin = new Vector2(24, 34); headRt.offsetMax = new Vector2(-22, -20);
        Sprite s = head != null ? head : DiscS;
        var dim = Img(Stretch("Dim", headRt), s, new Color(0.12f, 0.22f, 0.17f, 0.95f));
        dim.preserveAspect = true;
        hud.birdDim = dim;
        var fill = Img(Stretch("Fill", headRt), s, Color.white);
        fill.preserveAspect = true;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 0f;
        hud.birdFill = fill;

        Img(Stretch("Dial", gauge), DialS, DeathHud.Gold);
        var arc = Img(Stretch("Arc", gauge), ArcS, DeathHud.Amber);
        arc.type = Image.Type.Filled;
        arc.fillMethod = Image.FillMethod.Radial360;
        arc.fillOrigin = (int)Image.Origin360.Top;
        arc.fillClockwise = true;
        arc.fillAmount = 0f;
        hud.birdRing = arc;

        hud.birdPercent = Label("Percent", gauge, "0%", 16, FontStyle.Bold, DeathHud.Amber, TextAnchor.MiddleCenter, MC, MC, new Vector2(0, -24), new Vector2(80, 22));
        Soft(hud.birdPercent, 1);

        var pill = Pill("StatusPill", p, TC, TC, new Vector2(0, -132), new Vector2(72, 20), DeathHud.Leaf, 2f);
        hud.birdStatusPill = pill.GetComponent<Image>();
        hud.birdStatus = Label("Status", pill, "SAFE", 11, FontStyle.Bold, DeathHud.Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
    }

    static void BuildBanner(RectTransform parent, DeathHud hud)
    {
        var p = Framed("WarningBanner", parent, BC, BC, new Vector2(0, 96), new Vector2(720, 58), DeathHud.SurfaceDeep);
        hud.bannerGroup = p.gameObject.AddComponent<CanvasGroup>();
        hud.bannerGroup.alpha = 0f;
        hud.bannerGroup.blocksRaycasts = false;
        hud.bannerAccent = p.Find("Frame").GetComponent<Image>(); // frame turns red in the hide stage

        var icon = Child("Icon", p);
        Place(icon, ML, ML, new Vector2(16, 0), new Vector2(34, 34));
        hud.bannerIcon = Img(icon, DiscS, DeathHud.Gold);
        Label("Mark", icon, "!", 22, FontStyle.Bold, DeathHud.Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);

        var msg = Label("Message", p, "", 18, FontStyle.Normal, DeathHud.Cream, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
        msg.rectTransform.offsetMin = new Vector2(64, 0); msg.rectTransform.offsetMax = new Vector2(-18, 0);
        hud.bannerText = msg;

        // dial countdown above the banner
        var cd = Child("Countdown", p);
        cd.anchorMin = cd.anchorMax = new Vector2(0.5f, 1f);
        cd.pivot = new Vector2(0.5f, 0f);
        cd.anchoredPosition = new Vector2(0, 10);
        cd.sizeDelta = new Vector2(96, 96);
        var cdDisc = Stretch("Disc", cd);
        cdDisc.offsetMin = new Vector2(7, 7); cdDisc.offsetMax = new Vector2(-7, -7);
        Img(cdDisc, DiscS, DeathHud.SurfaceDeep);
        Img(Stretch("Dial", cd), DialS, DeathHud.Gold);
        var ring = Img(Stretch("Arc", cd), ArcS, DeathHud.Danger);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = false;
        hud.countdownRing = ring;
        hud.countdownText = Label("Number", cd, "10", 36, FontStyle.Bold, DeathHud.Cream, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
        hud.countdownRoot = cd.gameObject;
        cd.gameObject.SetActive(false);
    }

    static void BuildToast(RectTransform parent, DeathHud hud)
    {
        var p = Framed("RecruitToast", parent, BR, BR, new Vector2(-24, 24), new Vector2(340, 48));
        hud.toastGroup = p.gameObject.AddComponent<CanvasGroup>();
        hud.toastGroup.alpha = 0f;
        hud.toastGroup.blocksRaycasts = false;

        var icon = Child("Icon", p);
        Place(icon, ML, ML, new Vector2(12, 0), new Vector2(32, 28));
        Img(icon, ButterflyS, DeathHud.Leaf).preserveAspect = true;

        var t = Label("Text", p, "", 15, FontStyle.Normal, DeathHud.Cream, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
        t.rectTransform.offsetMin = new Vector2(52, 0); t.rectTransform.offsetMax = new Vector2(-14, 0);
        hud.toastText = t;
    }

    static void BuildPauseHint(RectTransform parent)
    {
        var p = Pill("PauseHint", parent, BL, BL, new Vector2(24, 24), new Vector2(116, 34), DeathHud.Surface, 2f);
        var key = Pill("Key", p, ML, ML, new Vector2(5, 0), new Vector2(44, 24), DeathHud.SurfaceDeep, 2.5f, DeathHud.Cream);
        Label("Esc", key, "ESC", 11, FontStyle.Bold, DeathHud.Cream, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
        Label("Text", p, "Pause", 14, FontStyle.Normal, DeathHud.Cream, TextAnchor.MiddleLeft, ML, ML, new Vector2(58, 0), new Vector2(56, 22));
    }

    static void BuildDeathOverlay(RectTransform root, DeathHud hud)
    {
        var d = Stretch("MartyrOverlay", root);
        hud.deathGroup = d.gameObject.AddComponent<CanvasGroup>();
        hud.deathGroup.alpha = 0f;
        hud.deathGroup.blocksRaycasts = false;

        // cinematic letterbox with gold edge lines
        var top = Child("BarTop", d); top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(0.5f, 1); top.sizeDelta = new Vector2(0, 96);
        Img(top, WhiteS, new Color(0.02f, 0.06f, 0.04f, 0.94f));
        var topLine = Child("Edge", top); topLine.anchorMin = Vector2.zero; topLine.anchorMax = new Vector2(1, 0); topLine.pivot = new Vector2(0.5f, 1); topLine.sizeDelta = new Vector2(0, 2);
        Img(topLine, WhiteS, new Color(DeathHud.Gold.r, DeathHud.Gold.g, DeathHud.Gold.b, 0.7f));
        var bot = Child("BarBottom", d); bot.anchorMin = Vector2.zero; bot.anchorMax = new Vector2(1, 0); bot.pivot = new Vector2(0.5f, 0); bot.sizeDelta = new Vector2(0, 96);
        Img(bot, WhiteS, new Color(0.02f, 0.06f, 0.04f, 0.94f));
        var botLine = Child("Edge", bot); botLine.anchorMin = new Vector2(0, 1); botLine.anchorMax = Vector2.one; botLine.pivot = new Vector2(0.5f, 0); botLine.sizeDelta = new Vector2(0, 2);
        Img(botLine, WhiteS, new Color(DeathHud.Gold.r, DeathHud.Gold.g, DeathHud.Gold.b, 0.7f));

        var emblem = Child("Emblem", d);
        Place(emblem, TC, TC, new Vector2(0, -112), new Vector2(90, 80));
        Img(emblem, ButterflyS, DeathHud.Gold).preserveAspect = true;

        hud.deathTitle = Label("Title", d, "MARTYR'S LAST FLUTTER", 64, FontStyle.Normal, DeathHud.Amber, TextAnchor.MiddleCenter, TC, TC, new Vector2(0, -196), new Vector2(1400, 80));
        Soft(hud.deathTitle, 4);
        hud.deathSub = Label("Sub", d, "", 26, FontStyle.Italic, DeathHud.Cream, TextAnchor.MiddleCenter, TC, TC, new Vector2(0, -272), new Vector2(1400, 36));
        Soft(hud.deathSub, 2);

        var track = Pill("SteerTrack", d, TC, TC, new Vector2(0, -324), new Vector2(540, 22), DeathHud.Track, 2f);
        var fill = Stretch("Fill", track);
        fill.offsetMin = new Vector2(5, 5); fill.offsetMax = new Vector2(-5, -5);
        var fi = Img(fill, PillS, DeathHud.Amber);
        fi.type = Image.Type.Sliced;
        fi.pixelsPerUnitMultiplier = 4f;
        hud.steerFillRect = fill;
        Label("SteerHint", d, "W  A  S  D   steer the fall", 18, FontStyle.Normal, DeathHud.Muted, TextAnchor.MiddleCenter, TC, TC, new Vector2(0, -352), new Vector2(540, 26));

        var pop = Child("LegacyPopup", root);
        Place(pop, MC, MC, new Vector2(0, -40), new Vector2(1200, 140));
        hud.legacyPopupGroup = pop.gameObject.AddComponent<CanvasGroup>();
        hud.legacyPopupGroup.alpha = 0f;
        hud.legacyPopupGroup.blocksRaycasts = false;
        hud.legacyPopupText = Label("Text", pop, "+0  REBEL LEGACY", 80, FontStyle.Bold, DeathHud.Amber, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
        Soft(hud.legacyPopupText, 5);
    }

    // ---------- pause menu ----------

    static void BuildPauseMenu(RectTransform root)
    {
        var overlay = Stretch("PauseMenu", root);
        var menu = overlay.gameObject.AddComponent<PauseMenu>();
        menu.overlay = overlay.gameObject.AddComponent<CanvasGroup>();
        menu.overlay.alpha = 0f;
        menu.overlay.interactable = false;
        menu.overlay.blocksRaycasts = false;

        var dim = Img(Stretch("Dim", overlay), WhiteS, new Color(0.01f, 0.05f, 0.03f, 0.72f));
        dim.raycastTarget = true; // swallow clicks behind the menu

        // main page
        var main = Framed("MainPage", overlay, MC, MC, Vector2.zero, new Vector2(560, 640), ornate: true);
        menu.mainPage = main.gameObject;
        var emblem = Child("Emblem", main);
        Place(emblem, TC, TC, new Vector2(0, -30), new Vector2(110, 96));
        Img(emblem, ButterflyS, DeathHud.Gold).preserveAspect = true;
        Label("Title", main, "PAUSED", 52, FontStyle.Normal, DeathHud.Cream, TextAnchor.MiddleCenter, TC, TC, new Vector2(0, -128), new Vector2(500, 64));
        Label("Sub", main, "The rebellion can wait a moment.", 20, FontStyle.Italic, DeathHud.Muted, TextAnchor.MiddleCenter, TC, TC, new Vector2(0, -190), new Vector2(500, 28));
        HLine(main, TC, new Vector2(0, -232), 380, centered: true);

        menu.resumeButton = MenuButton("Resume", main, "RESUME", new Vector2(0, -258), primary: true);
        menu.controlsButton = MenuButton("Controls", main, "CONTROLS", new Vector2(0, -346));
        menu.restartButton = MenuButton("Restart", main, "RESTART RUN", new Vector2(0, -434));
        menu.quitButton = MenuButton("Quit", main, "QUIT GAME", new Vector2(0, -522));
        Chain(menu.resumeButton, menu.controlsButton, menu.restartButton, menu.quitButton);

        // controls page
        var ctl = Framed("ControlsPage", overlay, MC, MC, Vector2.zero, new Vector2(1020, 690), ornate: true);
        menu.controlsPage = ctl.gameObject;
        Label("Title", ctl, "CONTROLS", 46, FontStyle.Normal, DeathHud.Cream, TextAnchor.MiddleCenter, TC, TC, new Vector2(0, -36), new Vector2(900, 60));
        HLine(ctl, TC, new Vector2(0, -104), 300, centered: true);

        Column(ctl, "FLIGHT", new Vector2(-245, -132), new[]
        {
            ("W  S", "Fly forward / back"),
            ("Space  Ctrl", "Rise / descend"),
            ("A  D", "Turn left / right"),
            ("Shift", "Boost"),
            ("Mouse", "Look around"),
        });
        Column(ctl, "REBELLION", new Vector2(245, -132), new[]
        {
            ("Hold E", "Recruit a nearby animal"),
            ("W A S D", "Steer the martyr's fall"),
            ("Hold H", "Hide  (test)"),
            ("K", "Die  (test)"),
            ("[   ]", "Village anger  (test)"),
        });

        menu.backButton = MenuButton("Back", ctl, "BACK", new Vector2(0, -590));
        Chain(menu.backButton);
        ctl.gameObject.SetActive(false);
    }

    static void Column(RectTransform parent, string title, Vector2 pos, (string key, string action)[] rows)
    {
        var col = Child(title + "Column", parent);
        Place(col, TC, TC, pos, new Vector2(440, 430));
        Label("Header", col, title, 20, FontStyle.Bold, DeathHud.Gold, TextAnchor.MiddleLeft, TL, TL, Vector2.zero, new Vector2(440, 28));
        HLine(col, TL, new Vector2(0, -36), 440);

        for (int i = 0; i < rows.Length; i++)
        {
            float y = -54 - i * 72;
            float w = Mathf.Max(56, 26 + rows[i].key.Length * 13);
            var key = Pill("Key_" + i, col, TL, TL, new Vector2(0, y), new Vector2(w, 48), DeathHud.SurfaceDeep, 1.6f);
            Label("Key", key, rows[i].key, 18, FontStyle.Bold, DeathHud.Amber, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
            Label("Action", col, rows[i].action, 21, FontStyle.Normal, DeathHud.Cream, TextAnchor.MiddleLeft, TL, TL, new Vector2(w + 20, y), new Vector2(440 - w - 20, 48));
        }
    }

    static Button MenuButton(string name, RectTransform parent, string text, Vector2 pos, bool primary = false)
    {
        var rt = Pill(name, parent, TC, TC, pos, new Vector2(400, 70), Color.white, 1.2f);
        var img = rt.GetComponent<Image>();
        img.raycastTarget = true;

        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        if (primary)
        {
            cb.normalColor = DeathHud.Amber;
            cb.highlightedColor = new Color(1f, 0.80f, 0.42f);
            cb.selectedColor = new Color(1f, 0.80f, 0.42f);
            cb.pressedColor = new Color(0.82f, 0.52f, 0.12f);
        }
        else
        {
            cb.normalColor = new Color(0.09f, 0.25f, 0.17f, 1f);
            cb.highlightedColor = new Color(0.17f, 0.38f, 0.26f, 1f);
            cb.selectedColor = new Color(0.17f, 0.38f, 0.26f, 1f);
            cb.pressedColor = new Color(0.05f, 0.16f, 0.11f, 1f);
        }
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        Label("Label", rt, text, 24, FontStyle.Normal, primary ? DeathHud.Ink : DeathHud.Cream, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, stretch: true);
        return b;
    }

    static void Chain(params Button[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = buttons[(i - 1 + buttons.Length) % buttons.Length];
            nav.selectOnDown = buttons[(i + 1) % buttons.Length];
            buttons[i].navigation = nav;
        }
    }

    // ---------- UI helpers ----------

    static readonly Vector2 TL = new Vector2(0, 1), TC = new Vector2(0.5f, 1), TR = new Vector2(1, 1);
    static readonly Vector2 ML = new Vector2(0, 0.5f), MC = new Vector2(0.5f, 0.5f);
    static readonly Vector2 BL = new Vector2(0, 0), BC = new Vector2(0.5f, 0), BR = new Vector2(1, 0);

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static RectTransform Child(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        var rt = Child(name, parent);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>
    /// Chamfered deep-green panel. HUD cards (default) get a single thin gold outline; ornate = true gives the
    /// engraved double frame with corner rivets (used for the pause menu).
    /// </summary>
    static RectTransform Framed(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        Color? fill = null, float scale = -1f, bool ornate = false)
    {
        if (scale <= 0f) scale = ornate ? 1.5f : 1.3f;
        var rt = Child(name, parent);
        Place(rt, anchor, pivot, pos, size);
        var body = Img(rt, ornate ? PanelS : PillS, fill ?? DeathHud.Surface);
        body.type = Image.Type.Sliced;
        body.pixelsPerUnitMultiplier = scale;
        var sh = rt.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
        sh.effectDistance = new Vector2(0, ornate ? -6 : -3);

        var frame = Img(Stretch("Frame", rt), ornate ? FrameS : PillFrameS, new Color(DeathHud.Gold.r, DeathHud.Gold.g, DeathHud.Gold.b, ornate ? 1f : 0.8f));
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = scale;
        return rt;
    }

    /// <summary>Small chamfered pill with a single gold outline. The body Image is on the returned object.</summary>
    static RectTransform Pill(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        Color fill, float scale = 1.4f, Color? frameColor = null)
    {
        var rt = Child(name, parent);
        Place(rt, anchor, pivot, pos, size);
        var body = Img(rt, PillS, fill);
        body.type = Image.Type.Sliced;
        body.pixelsPerUnitMultiplier = scale;

        var frame = Img(Stretch("Frame", rt), PillFrameS, frameColor ?? DeathHud.Gold);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = scale;
        return rt;
    }

    static void HLine(RectTransform parent, Vector2 anchor, Vector2 pos, float width, bool pivotRight = false, bool centered = false)
    {
        var rt = Child("Rule", parent);
        Place(rt, anchor, new Vector2(centered ? 0.5f : pivotRight ? 1f : 0f, 0.5f), pos, new Vector2(width, 1.5f));
        Img(rt, WhiteS, new Color(DeathHud.Gold.r, DeathHud.Gold.g, DeathHud.Gold.b, 0.65f));
    }

    static void VLine(RectTransform parent, Vector2 anchor, Vector2 pos, float height)
    {
        var rt = Child("Divider", parent);
        Place(rt, anchor, new Vector2(0.5f, 1f), pos, new Vector2(1.5f, height));
        Img(rt, WhiteS, new Color(DeathHud.Gold.r, DeathHud.Gold.g, DeathHud.Gold.b, 0.65f));
    }

    static Image Img(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text Label(string name, Transform parent, string content, int size, FontStyle style, Color color, TextAnchor align,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 boxSize, bool stretch = false)
    {
        var rt = stretch ? Stretch(name, parent) : Child(name, parent);
        if (!stretch) Place(rt, anchor, pivot, pos, boxSize);

        var t = rt.gameObject.AddComponent<Text>();
        t.text = content;
        bool italic = style == FontStyle.Italic || style == FontStyle.BoldAndItalic;
        t.font = italic ? SerifItalic : Serif;
        t.fontStyle = italic && SerifItalic != Serif ? (style == FontStyle.BoldAndItalic ? FontStyle.Bold : FontStyle.Normal) : style;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Soft(Text t, float distance)
    {
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.6f);
        s.effectDistance = new Vector2(0, -distance);
    }
}
