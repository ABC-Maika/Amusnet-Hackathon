using System;
using UnityEngine;

/// <summary>
/// Player settings, saved in PlayerPrefs. Master volume and fullscreen apply themselves;
/// the audio and flight code read MusicVolume, SfxVolume and MouseSensitivity, and can listen to Changed.
/// </summary>
public static class GameSettings
{
    public static event Action Changed;

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat("settings.masterVolume", 0.8f);
        set { PlayerPrefs.SetFloat("settings.masterVolume", value); AudioListener.volume = value; Changed?.Invoke(); }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat("settings.musicVolume", 0.8f);
        set { PlayerPrefs.SetFloat("settings.musicVolume", value); Changed?.Invoke(); }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat("settings.sfxVolume", 0.8f);
        set { PlayerPrefs.SetFloat("settings.sfxVolume", value); Changed?.Invoke(); }
    }

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat("settings.mouseSensitivity", 1f);
        set { PlayerPrefs.SetFloat("settings.mouseSensitivity", value); Changed?.Invoke(); }
    }

    // Unity already remembers fullscreen between launches.
    public static bool Fullscreen
    {
        get => Screen.fullScreen;
        set { Screen.fullScreen = value; Changed?.Invoke(); }
    }

    public static void Save() => PlayerPrefs.Save();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        AudioListener.volume = MasterVolume;
        // Quality is no longer a player setting: drop any level saved by older builds.
        PlayerPrefs.DeleteKey("settings.quality");
    }
}
