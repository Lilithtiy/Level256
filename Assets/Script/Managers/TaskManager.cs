using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class TaskManager : NetworkBehaviour
{
    public static TaskManager Instance { get; private set; }

    [SerializeField] private int tasksPerRound = 5;
    [SerializeField] private bool guaranteeTeamTask = true;

    public NetworkVariable<int> Completed = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Total = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private ScriptableAudio taskCompletedAudio;

    // SERVER ONLY. The exit (and the energy bar) can listen to this.
    public event System.Action OnAllTasksCompleted;

    private void Awake() => Instance = this;

     private float nextCheck;

    private void Update()
    {
        if (!IsServer || !IsSpawned || Time.time < nextCheck) return;
        nextCheck = Time.time + 1f;

        int players = NetworkManager.Singleton.ConnectedClientsList.Count;
        bool dropped = false;

        foreach (var t in StationTask.All)
        {
            if (!t.IsSpawned || !t.IsActive.Value || t.IsDone.Value || t.MinPlayers <= players) continue;

            t.ServerDeactivate();
            Total.Value = Mathf.Max(0, Total.Value - 1);
            dropped = true;
            Debug.Log($"{t.TaskName} dropped: needs {t.MinPlayers} players, {players} left");
        }

        // If that was the last unfinished task, the exit logic must hear about it
        if (dropped && Total.Value > 0 && Completed.Value >= Total.Value)
            OnAllTasksCompleted?.Invoke();
    }


    public void SetupRound()
    {
        if (!IsServer) return;

        int players = NetworkManager.Singleton.ConnectedClientsList.Count;
        var all = new List<StationTask>(FindObjectsByType<StationTask>(FindObjectsSortMode.None));

        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }

        if (guaranteeTeamTask && players >= 2)
        {
            int team = all.FindIndex(t => t.MinPlayers > 1 && t.MinPlayers <= players);
            if (team > 0) (all[0], all[team]) = (all[team], all[0]);
        }

        int active = 0;
        foreach (var t in all)
        {
            bool use = active < tasksPerRound && t.MinPlayers <= players;
            t.ServerSetup(use);
            if (use) active++;
        }

        Completed.Value = 0;
        Total.Value = active;
        Debug.Log($"Round tasks: {active} active out of {all.Count} ({players} players)");
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
}