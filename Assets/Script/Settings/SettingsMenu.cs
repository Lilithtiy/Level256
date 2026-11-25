using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SettingsMenu : MonoBehaviour
{
    // Keeps one persistent settings menu available across scenes
    public static SettingsMenu Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;

    [Tooltip("Higher than the lobby/HUD documents so this draws on top")]
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private ScriptableAudio clickAudio;

    private static readonly Color Yellow = new Color(1f, 0.92f, 0.016f, 1f);
    private static readonly Color Text = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);

    private VisualElement overlay;
    // Callbacks used to refresh UI controls from the current settings
    private readonly List<Action> refreshers = new();
    private PlayerController lockedPlayer;
    private bool open;
    private bool panelWasOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Instance != this) return;

        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        if (open) Close();
        else if (CanOpenWithEscape()) Open();
    }

    private void LateUpdate()
    {
        // Remember whether a task panel was open, so Escape doesn't open settings over it
        panelWasOpen = TaskPanelHost.Instance != null && TaskPanelHost.Instance.Open != null;
    }

    private bool CanOpenWithEscape()
    {
        if (panelWasOpen) return false;

        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && nm.LocalClient != null && nm.LocalClient.PlayerObject != null;
    }

    public void Open()
    {
        if (open || !EnsureBuilt()) return;

        open = true;
        foreach (var refresh in refreshers) refresh();
        overlay.style.display = DisplayStyle.Flex;

        // Lock the local player's controls while the menu is open
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && nm.LocalClient != null && nm.LocalClient.PlayerObject != null &&
            nm.LocalClient.PlayerObject.TryGetComponent(out lockedPlayer))
        {
            lockedPlayer.SetInputLocked(true);
        }
    }

    public void Close()
    {
        if (!open) return;

        open = false;
        if (overlay != null) overlay.style.display = DisplayStyle.None;
        GameSettings.Save();

        if (lockedPlayer != null) lockedPlayer.SetInputLocked(false);
        lockedPlayer = null;
    }

    private void Click()
    {
        if (clickAudio != null && AudioManager.audioManagerInstance != null)
            AudioManager.audioManagerInstance.PlayLocal(clickAudio);
    }

    // UI
    private bool EnsureBuilt()
    {
        if (overlay != null && overlay.panel != null) return true;

        var doc = GetComponent<UIDocument>();
        if (doc.rootVisualElement == null) return false;

        doc.sortingOrder = sortingOrder;
        refreshers.Clear();

        overlay = new VisualElement { name = "settings-overlay" };
        var o = overlay.style;
        o.position = Position.Absolute;
        o.left = 0; o.right = 0; o.top = 0; o.bottom = 0;
        o.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
        o.alignItems = Align.Center;
        o.justifyContent = Justify.Center;
        o.display = DisplayStyle.None;
        doc.rootVisualElement.Add(overlay);

        var panel = new VisualElement();
        var p = panel.style;
        p.width = 820;
        p.paddingTop = 24; p.paddingBottom = 24; p.paddingLeft = 40; p.paddingRight = 40;
        p.backgroundColor = new Color(14 / 255f, 14 / 255f, 18 / 255f, 1f);
        p.borderTopWidth = 2; p.borderBottomWidth = 2; p.borderLeftWidth = 2; p.borderRightWidth = 2;
        p.borderTopColor = Yellow; p.borderBottomColor = Yellow; p.borderLeftColor = Yellow; p.borderRightColor = Yellow;
        overlay.Add(panel);

        var title = new Label("SETTINGS");
        title.style.fontSize = 56;
        title.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        title.style.color = Yellow;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom = 8;
        panel.Add(title);

        Header(panel, "AUDIO");
        AddSlider(panel, "Master volume", 0f, 1f, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent);
        AddSlider(panel, "Music", 0f, 1f, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent);
        AddSlider(panel, "Sound effects", 0f, 1f, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, Percent);
        AddSlider(panel, "Voice chat", 0f, 1f, () => GameSettings.VoiceVolume, v => GameSettings.VoiceVolume = v, Percent);
        AddToggle(panel, $"Push to talk ({GameSettings.PushToTalkKey})", () => GameSettings.PushToTalk, v => GameSettings.PushToTalk = v);

        Header(panel, "CONTROLS");
        AddSlider(panel, "Mouse sensitivity", 0.2f, 3f, () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.0"));
        AddSlider(panel, "Field of view", 60f, 100f, () => GameSettings.Fov, v => GameSettings.Fov = v, v => $"{Mathf.RoundToInt(v)}°");

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.justifyContent = Justify.SpaceBetween;
        buttons.style.marginTop = 18;
        buttons.Add(MakeButton("RESET", () =>
        {
            Click();
            GameSettings.ResetToDefaults();
            foreach (var refresh in refreshers) refresh();
        }));
        buttons.Add(MakeButton("BACK", () => { Click(); Close(); }));
        panel.Add(buttons);

        return true;
    }

    private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

    private static void Header(VisualElement parent, string text)
    {
        var l = new Label(text);
        l.style.fontSize = 20;
        l.style.color = Dim;
        l.style.marginTop = 14;
        l.style.marginBottom = 2;
        parent.Add(l);
    }

    private static VisualElement NewRow(VisualElement parent, string label)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.height = 50;

        var l = new Label(label);
        l.style.fontSize = 26;
        l.style.color = Text;
        l.style.width = 260;
        row.Add(l);

        parent.Add(row);
        return row;
    }

    private void AddSlider(VisualElement parent, string label, float min, float max,
        Func<float> get, Action<float> set, Func<float, string> format)
    {
        var row = NewRow(parent, label);

        var slider = new Slider(min, max);
        slider.style.flexGrow = 1;
        slider.style.marginLeft = 10;
        slider.style.marginRight = 10;
        row.Add(slider);

        var value = new Label();
        value.style.width = 80;
        value.style.fontSize = 24;
        value.style.color = Yellow;
        value.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(value);

        // Update the setting and displayed value whenever the slider changes
        slider.RegisterValueChangedCallback(e =>
        {
            set(e.newValue);
            value.text = format(e.newValue);
        });

        // Refresh the slider and label when opening the menu or resetting settings
        refreshers.Add(() =>
        {
            float current = get();
            slider.SetValueWithoutNotify(current);
            value.text = format(current);
        });
    }

    private void AddToggle(VisualElement parent, string label, Func<bool> get, Action<bool> set)
    {
        var row = NewRow(parent, label);

        var toggle = new Toggle();
        toggle.style.marginRight = 14;
        toggle.style.scale = new Scale(new Vector3(1.5f, 1.5f, 1f));
        row.Add(toggle);

        toggle.RegisterValueChangedCallback(e => set(e.newValue));
        refreshers.Add(() => toggle.SetValueWithoutNotify(get()));
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var b = new Button(onClick) { text = text };
        var s = b.style;
        s.backgroundColor = Color.clear;
        s.borderTopWidth = 0; s.borderBottomWidth = 0; s.borderLeftWidth = 0; s.borderRightWidth = 0;
        s.color = Yellow;
        s.fontSize = 38;

        b.RegisterCallback<PointerEnterEvent>(_ => b.style.color = Color.white);
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.color = Yellow);
        return b;
    }
}