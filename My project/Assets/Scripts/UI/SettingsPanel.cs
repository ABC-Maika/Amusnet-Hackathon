using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Connects the controls in Settings.uxml to GameSettings. Used by both the main menu and the pause menu.</summary>
public sealed class SettingsPanel
{
    public event Action BackClicked;

    readonly List<Action> refreshers = new();

    public SettingsPanel(VisualElement root)
    {
        BindSlider(root, "masterVolume", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent);
        BindSlider(root, "musicVolume", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent);
        BindSlider(root, "sfxVolume", () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, Percent);
        BindSlider(root, "sensitivity", () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v,
            v => $"{v:0.0}x");

        var fullscreen = root.Q<Toggle>("fullscreen");
        fullscreen.RegisterValueChangedCallback(e => GameSettings.Fullscreen = e.newValue);
        refreshers.Add(() => fullscreen.SetValueWithoutNotify(GameSettings.Fullscreen));

        root.Q<Button>("settingsBack").clicked += Back;
        Refresh();
    }

    /// <summary>Shows the saved values. Call before opening the panel.</summary>
    public void Refresh()
    {
        foreach (var refresh in refreshers) refresh();
    }

    public void Back()
    {
        GameSettings.Save();
        BackClicked?.Invoke();
    }

    void BindSlider(VisualElement root, string name, Func<float> get, Action<float> set, Func<float, string> format)
    {
        var slider = root.Q<Slider>(name);
        var value = root.Q<Label>(name + "Value");
        slider.RegisterValueChangedCallback(e =>
        {
            set(e.newValue);
            value.text = format(e.newValue);
        });
        refreshers.Add(() =>
        {
            slider.SetValueWithoutNotify(get());
            value.text = format(get());
        });
    }

    static string Percent(float v) => $"{Mathf.RoundToInt(v * 100)}%";
}
