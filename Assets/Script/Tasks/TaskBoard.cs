using TMPro;
using UnityEngine;

public class TaskBoard : MonoBehaviour
{
    [SerializeField] private StationTask task;
    [SerializeField] private TMP_Text text;

    private string shown = null;

    private void Update()
    {
        if (task == null || text == null || !task.IsSpawned) return;

        // Show the task's board text only while it is active and unfinished
        string wanted = (task.IsActive.Value && !task.IsDone.Value) ? task.BoardText : "----";

        // Update the display only when its text changes
        if (wanted == shown) return;
        shown = wanted;
        text.text = wanted;
    }
}