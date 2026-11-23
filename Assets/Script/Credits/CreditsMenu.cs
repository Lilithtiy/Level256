using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CreditsMenu : MonoBehaviour
{
    [Serializable]
    public class Section
    {
        public string heading;
        public string[] lines;
    }

    public static CreditsMenu Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;

    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private string title = "CREDITS";
    [SerializeField] private bool autoScroll = true;
    [Tooltip("Pixels per second")]
    [SerializeField] private float scrollSpeed = 40f;
    [SerializeField] private ScriptableAudio clickAudio;

    [SerializeField]
    private List<Section> sections = new()
    {
        new Section { heading = "GAME DESIGN & PROGRAMMING", lines = new[] { "Name" } },
        new Section { heading = "MUSIC & SOUND", lines = new[] { "Name / source" } },
        new Section { heading = "SPECIAL THANKS", lines = new[] { "Playtesters" } },
    };

    private static readonly Color Yellow = new Color(1f, 0.92f, 0.016f, 1f);
    private static readonly Color Text = new Color(1f, 1f, 1f, 0.85f);

    private VisualElement overlay;
    private ScrollView scroll;
    private bool open;
    private float pauseUntil;

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
        if (Instance != this || !open) return;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (autoScroll && scroll != null && Time.unscaledTime >= pauseUntil)
            scroll.verticalScroller.value += scrollSpeed * Time.unscaledDeltaTime;
    }

    public void Open()
    {
        if (open || !EnsureBuilt()) return;

        open = true;
        overlay.style.display = DisplayStyle.Flex;
        scroll.verticalScroller.value = 0f;
        pauseUntil = Time.unscaledTime + 1.5f; // short pause before it starts moving
    }

    public void Close()
    {
        if (!open) return;

        open = false;
        if (overlay != null) overlay.style.display = DisplayStyle.None;
    }

    private void Click()
    {
        if (clickAudio != null && AudioManager.audioManagerInstance != null)
            AudioManager.audioManagerInstance.PlayLocal(clickAudio);
    }

    private bool EnsureBuilt()
    {
        if (overlay != null && overlay.panel != null) return true;

        var doc = GetComponent<UIDocument>();
        if (doc.rootVisualElement == null) return false;

        doc.sortingOrder = sortingOrder;

        overlay = new VisualElement { name = "credits-overlay" };
        var o = overlay.style;
        o.position = Position.Absolute;
        o.left = 0; o.right = 0; o.top = 0; o.bottom = 0;
        o.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
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

        var titleLabel = new Label(title);
        titleLabel.style.fontSize = 56;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        titleLabel.style.color = Yellow;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.marginBottom = 12;
        panel.Add(titleLabel);

        scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.height = 520;
        scroll.RegisterCallback<WheelEvent>(_ => pauseUntil = Time.unscaledTime + 3f);   // manual scrolling pauses auto-scroll
        panel.Add(scroll);

        foreach (var section in sections)
        {
            if (section == null) continue;

            if (!string.IsNullOrEmpty(section.heading))
            {
                var h = new Label(section.heading);
                h.style.fontSize = 30;
                h.style.color = Yellow;
                h.style.unityTextAlign = TextAnchor.MiddleCenter;
                h.style.marginTop = 26;
                h.style.marginBottom = 6;
                scroll.Add(h);
            }

            if (section.lines == null) continue;

            foreach (string line in section.lines)
            {
                var l = new Label(line);
                l.style.fontSize = 26;
                l.style.color = Text;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.marginTop = 2;
                scroll.Add(l);
            }
        }

        // Empty space at the end so the last line can scroll up
        var spacer = new VisualElement();
        spacer.style.height = 200;
        scroll.Add(spacer);

        var back = MakeButton("BACK", () => { Click(); Close(); });
        back.style.marginTop = 14;
        panel.Add(back);

        return true;
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