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

    [Tooltip("Higher than the other UI documents so this draws on top")]
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private bool autoScroll = true;
    [Tooltip("Pixels per second")]
    [SerializeField] private float scrollSpeed = 40f;
    [SerializeField] private ScriptableAudio clickAudio;

    [SerializeField]
    private List<Section> sections = new()
    {
        new Section { heading = "GAME DESIGN & PROGRAMMING", lines = new[] { "Your name" } },
        new Section { heading = "MUSIC & SOUND", lines = new[] { "Name / source" } },
        new Section { heading = "SPECIAL THANKS", lines = new[] { "Playtesters" } },
    };

    private VisualElement root;
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
        if (open || !TryBind()) return;

        open = true;
        root.style.display = DisplayStyle.Flex;
        scroll.verticalScroller.value = 0f;
        pauseUntil = Time.unscaledTime + 1.5f; // short pause before it starts moving
    }

    public void Close()
    {
        if (!open) return;

        open = false;
        if (root != null) root.style.display = DisplayStyle.None;
    }

    private void Click()
    {
        if (clickAudio != null && AudioManager.audioManagerInstance != null)
            AudioManager.audioManagerInstance.PlayLocal(clickAudio);
    }

    private bool TryBind()
    {
        if (root != null && root.panel != null) return true;

        var doc = GetComponent<UIDocument>();
        if (doc.rootVisualElement == null) return false;

        var r = doc.rootVisualElement.Q("credits-root");
        if (r == null)
        {
            Debug.LogError("CreditsMenu: set CreditsMenu.uxml as the Source Asset on the UIDocument.");
            return false;
        }

        doc.sortingOrder = sortingOrder;
        root = r;

        scroll = root.Q<ScrollView>("credits-scroll");
        scroll.RegisterCallback<WheelEvent>(_ => pauseUntil = Time.unscaledTime + 3f); // manual scrolling pauses auto-scroll
        root.Q<Button>("credits-back").clicked += () => { Click(); Close(); };

        BuildList();
        return true;
    }

    // The names come from the Inspector list
    private void BuildList()
    {
        scroll.Clear();

        foreach (var section in sections)
        {
            if (section == null) continue;

            if (!string.IsNullOrEmpty(section.heading))
            {
                var h = new Label(section.heading);
                h.AddToClassList("credits-heading");
                scroll.Add(h);
            }

            if (section.lines == null) continue;

            foreach (string line in section.lines)
            {
                var l = new Label(line);
                l.AddToClassList("credits-line");
                scroll.Add(l);
            }
        }

        // Empty space at the end so the last line can scroll up
        var spacer = new VisualElement();
        spacer.AddToClassList("credits-spacer");
        scroll.Add(spacer);
    }
}