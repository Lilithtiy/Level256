using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectiveManager : NetworkBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    [Tooltip("Untick to test")]
    [SerializeField] private bool requireEnergy = true;
    [SerializeField] private int energyNeeded = 100;

    public NetworkVariable<bool> TasksDone = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> EnergyFull = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> ExitOpen = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        // just temporaty test keys so F6 fills the power bar, F7 finishes all tasks
        if (Keyboard.current == null) return;
        if (Keyboard.current.f6Key.wasPressedThisFrame) SetEnergyFull(true);
        if (Keyboard.current.f7Key.wasPressedThisFrame) OnTasksDone();
    }

    // UI (Placeholder)
    private void OnGUI()
    {
        if (!IsSpawned || GameManager.Instance == null ||
            GameManager.Instance.State.Value != GameState.Playing) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperRight };
        float x = Screen.width - 300f;

        string tasks = TaskManager.Instance != null ? $"{TaskManager.Instance.Completed.Value}/{TaskManager.Instance.Total.Value}" : "-";

        var score = ScoreManager.scoreManager;
        string energy = (score != null && score.IsSpawned) ? $"{score.electricityScore.Value}/{energyNeeded}" : "-";

        GUI.Label(new Rect(x, 40, 290, 24), $"{Mark(TasksDone.Value)} Repair the systems ({tasks})", style);

        if (requireEnergy)
            GUI.Label(new Rect(x, 62, 290, 24), $"{Mark(EnergyFull.Value)} Fill the power bar ({energy})", style);

        GUI.Label(new Rect(x, 84, 290, 24),
            ExitOpen.Value ? "EXIT OPEN, get everyone out!" : "[ ] Escape", style);
    }

    private static string Mark(bool done) => done ? "[x]" : "[ ]";
}