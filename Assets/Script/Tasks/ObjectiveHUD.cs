using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ObjectiveHUD : MonoBehaviour
{
    private VisualElement hud, taskList, powerRow, exitRow;
    private Label powerValue, exitValue, lives;
    private ExitZone exit;
    private float nextRefresh;

    // Cache each tasks ui row so it can be updated or removed efficiently
    private readonly Dictionary<StationTask, (VisualElement row, Label value)> rows = new();

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        hud = root.Q("hud");
        taskList = root.Q("tasks");
        powerRow = root.Q("power-row");
        powerValue = root.Q<Label>("power-value");
        exitRow = root.Q("exit-row");
        exitValue = root.Q<Label>("exit-value");
        lives = root.Q<Label>("lives");
        rows.Clear();
    }

    private void Update()
    {
        // Refresh periodically rather than updating the UI every frame
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;

        var gm = GameManager.Instance;
        bool show = gm != null && gm.IsSpawned && gm.State.Value == GameState.Playing;
        hud.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show) return;

        lives.text = $"LIVES  {gm.Lives.Value}";
        RefreshTasks();
        RefreshObjectives();
    }

    // Create or update rows for active tasks and remove rows for inactive ones
    private void RefreshTasks()
    {
        var seen = new HashSet<StationTask>();

        foreach (var t in StationTask.All)
        {
            if (t == null || !t.IsSpawned || !t.IsActive.Value) continue;
            seen.Add(t);

            if (!rows.TryGetValue(t, out var r))
            {
                r = MakeRow(t);
                rows[t] = r;
            }

            bool done = t.IsDone.Value;
            r.row.EnableInClassList("hud__row--done", done);
            string percent = $"{Mathf.RoundToInt(t.Progress.Value * 100f)}%";
            string detail = t.HudDetail;
            r.value.text = done ? "DONE" : string.IsNullOrEmpty(detail) ? percent : $"{detail}   {percent}";
        }

        var stale = new List<StationTask>();
        foreach (var kv in rows)
            if (!seen.Contains(kv.Key)) stale.Add(kv.Key);

        foreach (var t in stale)
        {
            rows[t].row.RemoveFromHierarchy();
            rows.Remove(t);
        }
    }

    private (VisualElement, Label) MakeRow(StationTask t)
    {
        var row = new VisualElement();
        row.AddToClassList("hud__row");

        var name = new Label(t.MinPlayers > 1 ? $"{t.TaskName}  [TEAM]" : t.TaskName);
        name.AddToClassList("hud__name");
        var value = new Label();
        value.AddToClassList("hud__value");

        row.Add(name);
        row.Add(value);
        taskList.Add(row);
        return (row, value);
    }

    // Update energy and exit status from their networked objective state
    private void RefreshObjectives()
    {
        var obj = ObjectiveManager.Instance;
        if (obj == null || !obj.IsSpawned) return;

        // power bar
        powerRow.style.display = obj.RequireEnergy ? DisplayStyle.Flex : DisplayStyle.None;
        var score = ScoreManager.scoreManager;
        if (score != null && score.IsSpawned)
            powerValue.text = $"{score.electricityScore.Value}/{obj.EnergyNeeded}";
        powerRow.EnableInClassList("hud__row--done", obj.EnergyFull.Value);

        // Show whether the exit is locked or how many players are inside
        if (exit == null) exit = FindFirstObjectByType<ExitZone>();
        bool open = obj.ExitOpen.Value;
        exitValue.text = !open ? "LOCKED" : exit != null ? $"OPEN  {exit.PlayersInside.Value}/{exit.PlayersTotal.Value}" : "OPEN";
        exitRow.EnableInClassList("hud__row--done", open);
    }
}