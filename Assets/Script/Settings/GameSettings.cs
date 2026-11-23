using System;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;


public static class GameSettings
{
    public static event Action Changed;

    public const float DefaultMaster = 1f;
    public const float DefaultMusic = 0.7f;
    public const float DefaultSfx = 1f;
    public const float DefaultVoice = 1f;
    public const float DefaultSensitivity = 1f;
    public const float DefaultFov = 75f;

    private const string KMaster = "set.master", KMusic = "set.music", KSfx = "set.sfx", KVoice = "set.voice";
    private const string KSens = "set.sens", KFov = "set.fov", KFullscreen = "set.fullscreen", KVsync = "set.vsync";

    private static bool loaded;
    private static float master, music, sfx, voice, sens, fov;
    private static bool fullscreen, vsync;

    private const string KPtt = "set.ptt";
    private static bool pushToTalk;
    public const Key PushToTalkKey = Key.V; // Change this one line if you want another key

    public static float MasterVolume
    {
        get { Load(); return master; }
        set { SetFloat(ref master, KMaster, Mathf.Clamp01(value)); AudioListener.volume = master; }
    }

    public static float MusicVolume
    {
        get { Load(); return music; }
        set => SetFloat(ref music, KMusic, Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get { Load(); return sfx; }
        set => SetFloat(ref sfx, KSfx, Mathf.Clamp01(value));
    }

    public static float VoiceVolume
    {
        get { Load(); return voice; }
        set { SetFloat(ref voice, KVoice, Mathf.Clamp01(value)); ApplyVoice(); }
    }

    public static float MouseSensitivity
    {
        get { Load(); return sens; }
        set => SetFloat(ref sens, KSens, Mathf.Clamp(value, 0.2f, 3f));
    }

    public static float Fov
    {
        get { Load(); return fov; }
        set => SetFloat(ref fov, KFov, Mathf.Clamp(value, 60f, 100f));
    }

    public static bool Fullscreen
    {
        get { Load(); return fullscreen; }
        set { SetBool(ref fullscreen, KFullscreen, value); ApplyDisplay(); }
    }

    public static bool VSync
    {
        get { Load(); return vsync; }
        set { SetBool(ref vsync, KVsync, value); ApplyDisplay(); }
    }

    public static bool PushToTalk
    {
        get { Load(); return pushToTalk; }
        set => SetBool(ref pushToTalk, KPtt, value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Load();
        AudioListener.volume = master;
        ApplyDisplay();
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        master = PlayerPrefs.GetFloat(KMaster, DefaultMaster);
        music = PlayerPrefs.GetFloat(KMusic, DefaultMusic);
        sfx = PlayerPrefs.GetFloat(KSfx, DefaultSfx);
        voice = PlayerPrefs.GetFloat(KVoice, DefaultVoice);
        sens = PlayerPrefs.GetFloat(KSens, DefaultSensitivity);
        fov = PlayerPrefs.GetFloat(KFov, DefaultFov);
        pushToTalk = PlayerPrefs.GetInt(KPtt, 0) == 1;
        fullscreen = PlayerPrefs.GetInt(KFullscreen, 1) == 1;
        vsync = PlayerPrefs.GetInt(KVsync, 1) == 1;
    }

    private static void SetFloat(ref float field, string key, float value)
    {
        Load();
        field = value;
        PlayerPrefs.SetFloat(key, value);
        Changed?.Invoke();
    }

    private static void SetBool(ref bool field, string key, bool value)
    {
        Load();
        field = value;
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        Changed?.Invoke();
    }

    public static void Save() => PlayerPrefs.Save();

    public static void ResetToDefaults()
    {
        MasterVolume = DefaultMaster;
        MusicVolume = DefaultMusic;
        SfxVolume = DefaultSfx;
        VoiceVolume = DefaultVoice;
        MouseSensitivity = DefaultSensitivity;
        Fov = DefaultFov;
        PushToTalk = false;
        Fullscreen = true;
        VSync = true;
        Save();
    }

    private static void ApplyDisplay()
    {
        Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        QualitySettings.vSyncCount = vsync ? 1 : 0;
    }


    public static void ApplyVoice()
    {
        Load();
        try
        {
            if (VivoxService.Instance.IsLoggedIn)
                VivoxService.Instance.SetOutputDeviceVolume(Mathf.RoundToInt(Mathf.Lerp(-50f, 0f, voice)));
        }
        catch (Exception) { /* Vivox isn't ready yet */ }
    }
}