using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class TaskManager : NetworkBehaviour
{
    public static TaskManager Instance { get; private set; }

    [SerializeField] private int tasksPerRound = 4;

    public NetworkVariable<int> Completed = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Total = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private ScriptableAudio taskCompletedAudio;

    // SERVER ONLY. The exit (and the energy bar) can listen to this.
    public event System.Action OnAllTasksCompleted;

    private void Awake()
    {
        Instance = this;
    }

    // SERVER ONLY. Called by GameManager.BeginRound.
    public void SetupRound()
    {
        if (!IsServer) return;

        var all = new List<StationTask>(FindObjectsByType<StationTask>(FindObjectsSortMode.None));

        // Shuffle
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }

        int count = Mathf.Min(tasksPerRound, all.Count);
        for (int i = 0; i < all.Count; i++)
            all[i].ServerSetup(i < count);   // the first few are active this round

        Completed.Value = 0;
        Total.Value = count;
        Debug.Log($"Round tasks: {count} active out of {all.Count}");
    }

    public void TaskCompleted(StationTask task)
    {
        if (!IsServer) return;

        Completed.Value++;
        AudioManager.audioManagerInstance.PlayLocal(taskCompletedAudio);
        Debug.Log($"Task done: {task.TaskName} ({Completed.Value}/{Total.Value})");

        if (Completed.Value >= Total.Value)
            OnAllTasksCompleted?.Invoke();
    }

    // Temporary UI
    private void OnGUI()
    {
        if (!IsSpawned || Total.Value == 0) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperCenter };
        GUI.Label(new Rect(Screen.width / 2f - 100, 10, 200, 30),
            $"Tasks: {Completed.Value}/{Total.Value}", style);
    }
}