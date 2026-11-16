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

        string wanted = (task.IsActive.Value && !task.IsDone.Value) ? task.BoardText : "----";

        if (wanted == shown) return;
        shown = wanted;
        text.text = wanted;
    }
}