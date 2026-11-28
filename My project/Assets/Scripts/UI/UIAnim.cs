using UnityEngine.UIElements;

/// <summary>
/// Plays the animations defined in Common.uss by toggling USS classes.
/// Timings and looks live in the stylesheets; this only decides when.
/// UI Toolkit animates in real time, so all of this keeps working while Time.timeScale is 0.
/// </summary>
public static class UIAnim
{
    const string Open = "screen--open";
    const string Hidden = "hidden";
    const string Shown = "is-shown"; // marker only: the state we're heading to, so quick show/hide calls don't race
    const long HideAfterMs = 350;    // matches the .screen transition in Common.uss

    /// <summary>Fades and zooms a .screen in or out, then takes it out of layout once hidden.</summary>
    public static void SetShown(VisualElement screen, bool show)
    {
        if (screen.ClassListContains(Shown) == show) return;
        screen.EnableInClassList(Shown, show);
        if (show)
        {
            screen.RemoveFromClassList(Hidden);
            // Wait a frame so the closed style is applied first and the transition has a start point.
            screen.schedule.Execute(() => { if (screen.ClassListContains(Shown)) screen.AddToClassList(Open); })
                .ExecuteLater(20);
        }
        else
        {
            screen.RemoveFromClassList(Open);
            screen.schedule.Execute(() => { if (!screen.ClassListContains(Shown)) screen.AddToClassList(Hidden); })
                .ExecuteLater(HideAfterMs);
        }
    }

    /// <summary>Adds <paramref name="cls"/> briefly, for a pop that animates back.</summary>
    public static void Bump(VisualElement element, string cls, long ms = 140)
    {
        element.AddToClassList(cls);
        element.schedule.Execute(() => element.RemoveFromClassList(cls)).ExecuteLater(ms);
    }

    /// <summary>
    /// Ping-pongs forever between an element's base style and <paramref name="altClass"/>,
    /// flipping each time its transition ends. The motion itself is defined in USS.
    /// </summary>
    public static void Loop(VisualElement element, string altClass)
    {
        void Flip() => element.schedule.Execute(() => element.ToggleInClassList(altClass)).ExecuteLater(10);
        element.RegisterCallback<TransitionEndEvent>(e => { if (e.target == element) Flip(); });
        // A hidden element cancels its transition; restart once it's laid out again.
        element.RegisterCallback<GeometryChangedEvent>(_ => Flip());
    }

    /// <summary>Fades the full-screen .fade cover in (true) or out (false).</summary>
    public static void Cover(VisualElement fade, bool on)
    {
        // Same one-frame wait as screens, so the fade-out also animates on a freshly loaded scene.
        fade.schedule.Execute(() => fade.EnableInClassList("fade--on", on)).ExecuteLater(20);
    }

    public const long CoverMs = 450; // matches .fade in Common.uss
}
