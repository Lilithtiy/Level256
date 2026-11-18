using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectiveManager : NetworkBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    public bool RequireEnergy => requireEnergy;
    public int EnergyNeeded => energyNeeded;
    

    [Tooltip("Untick to test")]
    [SerializeField] private bool requireEnergy = true;
    [SerializeField] private int energyNeeded = 100;

    public NetworkVariable<bool> TasksDone = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> EnergyFull = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> ExitOpen = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> Progress = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (TaskManager.Instance != null)
            TaskManager.Instance.OnAllTasksCompleted += OnTasksDone;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && TaskManager.Instance != null)
            TaskManager.Instance.OnAllTasksCompleted -= OnTasksDone;
    }

    private void OnTasksDone()
    {
        TasksDone.Value = true;
        Evaluate();
    }

    private void SetEnergyFull(bool full)
    {
        if (!IsServer) return;

        EnergyFull.Value = full;
        Evaluate();
    }

    private void Evaluate()
    {
        if (ExitOpen.Value) return;

        if (TasksDone.Value && (EnergyFull.Value || !requireEnergy))
            ExitOpen.Value = true;
    }

    private void Update()
    {
        if (!IsServer) return;

        var score = ScoreManager.scoreManager;
        if (!EnergyFull.Value && score != null && score.IsSpawned &&
            score.electricityScore.Value >= energyNeeded)
        {
            SetEnergyFull(true);
        }

        float p = ComputeProgress();
        if (Mathf.Abs(p - Progress.Value) > 0.001f) Progress.Value = p;

        // just temporaty test keys so F6 fills the power bar, F7 finishes all tasks
        if (Keyboard.current == null) return;
        if (Keyboard.current.f6Key.wasPressedThisFrame) SetEnergyFull(true);
        if (Keyboard.current.f7Key.wasPressedThisFrame) OnTasksDone();
    }

    // Overall progress from 0 to 1
    private float ComputeProgress()
    {
        float tasks = 0f;
        var tm = TaskManager.Instance;
        if (TasksDone.Value) tasks = 1f;
        else if (tm != null && tm.IsSpawned && tm.Total.Value > 0)
            tasks = (float)tm.Completed.Value / tm.Total.Value;

        if (!requireEnergy) return tasks;

        float energy = 0f;
        var score = ScoreManager.scoreManager;
        if (EnergyFull.Value) energy = 1f;
        else if (score != null && score.IsSpawned)
            energy = Mathf.Clamp01((float)score.electricityScore.Value / energyNeeded);

        return (tasks + energy) * 0.5f;
    }
}