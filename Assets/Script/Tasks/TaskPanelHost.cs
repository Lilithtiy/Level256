using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class TaskPanelHost : MonoBehaviour
{
    public static TaskPanelHost Instance { get; private set; }

    public StationTask Open { get; private set; }

    private VisualElement prompt, hold, holdFill, overlay, content;
    private Label promptText, holdLabel, title, status;
    private float statusUntil;

    private void OnEnable()
    {
        Instance = this;
        var root = GetComponent<UIDocument>().rootVisualElement;

        prompt = root.Q("prompt");
        promptText = root.Q<Label>("prompt-text");
        hold = root.Q("hold");
        holdLabel = root.Q<Label>("hold-label");
        holdFill = root.Q("hold-fill");
        overlay = root.Q("overlay");
        content = root.Q("content");
        title = root.Q<Label>("title");
        status = root.Q<Label>("status");
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Let the open task update its panel, and clear messages after their timeout
        if (Open != null) Open.TickPanel();

        if (statusUntil > 0f && Time.time >= statusUntil)
        {
            statusUntil = 0f;
            status.text = "";
        }
    }

    // prompt/hold bar
    public void ShowPrompt(string text)
    {
        promptText.text = text;
        prompt.style.display = DisplayStyle.Flex;
    }

    public void HidePrompt() => prompt.style.display = DisplayStyle.None;

    public void ShowHold(string label, float progress)
    {
        holdLabel.text = label;
        holdFill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        hold.style.display = DisplayStyle.Flex;
    }

    public void HideHold() => hold.style.display = DisplayStyle.None;

    // Instantiate and bind the selected tasks ui panel
    public bool OpenPanel(StationTask task)
    {
        if (task.PanelAsset == null)
        {
            Debug.LogWarning($"{task.name}: no Panel Asset assigned.");
            return false;
        }

        ClosePanel();
        Open = task;
        title.text = task.TaskName.ToUpper();
        SetStatus("", false);

        var panel = task.PanelAsset.Instantiate();
        content.Add(panel);
        task.BindPanel(panel);

        overlay.style.display = DisplayStyle.Flex;
        return true;
    }

    // Unbind and clear the current panel
    public void ClosePanel()
    {
        if (Open != null) Open.UnbindPanel();
        Open = null;

        content?.Clear();
        if (overlay != null) overlay.style.display = DisplayStyle.None;
    }

    public void ShowStatus(string text, bool good, float seconds)
    {
        SetStatus(text, good);
        statusUntil = Time.time + seconds;
    }

    private void SetStatus(string text, bool good)
    {
        status.text = text;
        status.EnableInClassList("panel-status--good", good && text != "");
        status.EnableInClassList("panel-status--bad", !good && text != "");
        statusUntil = 0f;
    }
}