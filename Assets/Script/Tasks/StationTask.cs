using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public enum TaskMode { Hold, Panel, Press }

public abstract class StationTask : NetworkBehaviour
{
    public static readonly List<StationTask> All = new();

    [Header("Task")]
    [SerializeField] private string taskName = "Task";
    [SerializeField] private float maxDistance = 4f;
    [SerializeField] private float noiseRadius = 10f;
    [SerializeField] private float mistakeNoiseRadius = 20f;
    [SerializeField] private float mistakeNoiseSeconds = 3f;
    [SerializeField] private Color brokenColor = Color.red;
    [SerializeField] private Color fixedColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.gray;

    [Header("Panel")]
    [SerializeField] private VisualTreeAsset panelAsset;

    public NetworkVariable<float> Progress = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsDone = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsActive = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Workers = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string TaskName => taskName;
    public VisualTreeAsset PanelAsset => panelAsset;
    public abstract TaskMode Mode { get; }
    public virtual string Prompt => $"Press E to use {taskName}";
    public virtual string PromptAt(Vector3 point) => Prompt;
    public virtual string BoardText => "";

    public virtual int MinPlayers => 1;

    public virtual string HoldLabel =>
        MinPlayers > 1 ? $"{taskName}   {Workers.Value}/{MinPlayers} players" : taskName;
    public virtual string HudDetail => "";

    protected float MaxDistance => maxDistance;

    private readonly List<ulong> workers = new();
    protected IReadOnlyList<ulong> WorkerIds => workers;
    protected int WorkerCount => workers.Count;

    private Renderer[] renderers;
    private MarkerTarget[] markers;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        markers = GetComponentsInChildren<MarkerTarget>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (!All.Contains(this)) All.Add(this);

        Progress.OnValueChanged += OnFloatChanged;
        IsDone.OnValueChanged += OnBoolChanged;
        IsActive.OnValueChanged += OnBoolChanged;
        RefreshVisuals();
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);

        Progress.OnValueChanged -= OnFloatChanged;
        IsDone.OnValueChanged -= OnBoolChanged;
        IsActive.OnValueChanged -= OnBoolChanged;
    }

    // Panel hooks (client side)
    public virtual void BindPanel(VisualElement root) { }
    public virtual void TickPanel() { }
    public virtual void UnbindPanel() { }

    protected void Say(string text, bool good = false, float seconds = 1.5f) => TaskPanelHost.Instance?.ShowStatus(text, good, seconds);

    // RPCs
    [Rpc(SendTo.Server)]
    public void SetWorkingRpc(bool working, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (!working || !CanWork || !IsCloseEnough(sender))
            RemoveWorker(sender);
        else
            AddWorker(sender);
    }

    [Rpc(SendTo.Server)]
    public void AddProgressRpc(float amount, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!workers.Contains(sender) || !CanWork) return;

        Progress.Value = Mathf.Min(1f, Progress.Value + Mathf.Clamp01(amount));
        CheckDone();
    }

    [Rpc(SendTo.Server)]
    public void ReportMistakeRpc(RpcParams rpcParams = default)
    {
        if (!CanWork || !workers.Contains(rpcParams.Receive.SenderClientId)) return;
        EmitAlarm(mistakeNoiseRadius, mistakeNoiseSeconds);
    }

    [Rpc(SendTo.Server)]
    public void InteractRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CanWork || !IsCloseEnough(sender)) return;
        OnInteract(sender);
    }

    protected virtual void OnInteract(ulong clientId) { }

    // Noise 
    protected void EmitAlarm(float radius, float seconds) => EmitAlarmAt(transform.position, radius, seconds);

    protected void EmitAlarmAt(Vector3 position, float radius, float seconds)
    {
        if (NoiseTracker.Instance != null)
            NoiseTracker.Instance.EmitNoise(position, radius, seconds);
    }

    protected virtual void EmitWorkNoise(float radius, float seconds) => EmitAlarm(radius, seconds);

    // Server logic
    protected bool CanWork =>
        IsActive.Value && !IsDone.Value &&
        GameManager.Instance != null && GameManager.Instance.State.Value == GameState.Playing;

    protected bool IsWorker(ulong clientId) => workers.Contains(clientId);

    private void AddWorker(ulong id)
    {
        if (workers.Contains(id)) return;
        workers.Add(id);
        WorkersChanged();
    }

    private void RemoveWorker(ulong id)
    {
        if (workers.Remove(id)) WorkersChanged();
    }

    private void ClearWorkers()
    {
        if (workers.Count == 0) return;
        workers.Clear();
        WorkersChanged();
    }

    private void WorkersChanged()
    {
        Workers.Value = workers.Count;
        OnWorkersChanged();
    }

    protected virtual void OnWorkersChanged() { }

    private void Update()
    {
        if (!IsServer || !IsActive.Value || IsDone.Value) return;

        if (workers.Count == 0)
        {
            ServerIdle(Time.deltaTime);
            return;
        }

        bool changed = false;
        for (int i = workers.Count - 1; i >= 0; i--)
        {
            if (IsCloseEnough(workers[i])) continue;
            workers.RemoveAt(i);
            changed = true;
        }
        if (changed) WorkersChanged();
        if (workers.Count == 0) return;

        // players who have moved too far away are removed
        ServerTick(Time.deltaTime);
        EmitWorkNoise(noiseRadius, 0.3f);
        CheckDone();
    }

    protected virtual void ServerTick(float deltaTime) { }

    protected virtual void ServerIdle(float deltaTime) { }

    protected void CheckDone()
    {
        if (IsDone.Value || Progress.Value < 0.999f) return;

        Progress.Value = 1f;
        IsDone.Value = true;
        ClearWorkers();

        if (TaskManager.Instance != null)
            TaskManager.Instance.TaskCompleted(this);
    }

    protected void FinishNow()
    {
        Progress.Value = 1f;
        CheckDone();
    }

    public void ServerSetup(bool active)
    {
        ClearWorkers();
        Progress.Value = 0f;
        IsDone.Value = false;
        IsActive.Value = active;
        if (active) OnRoundSetup();
    }

    protected virtual void OnRoundSetup() { }

    public virtual bool InRange(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude <= maxDistance;
    }

    private bool IsCloseEnough(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
            return false;
        return InRange(client.PlayerObject.transform.position);
    }

    // A team task that can no longer be finished because a player left
    public void ServerDeactivate()
    {
        ClearWorkers();
        IsActive.Value = false;
    }

    // Visuals
    private void OnFloatChanged(float previous, float current) => RefreshVisuals();
    private void OnBoolChanged(bool previous, bool current) => RefreshVisuals();

    protected virtual void RefreshVisuals()
    {
        Color c = !IsActive.Value ? inactiveColor : Color.Lerp(brokenColor, fixedColor, Progress.Value);
        foreach (var r in renderers) r.material.color = c;

        bool showMarker = IsActive.Value && !IsDone.Value;
        foreach (var m in markers) m.enabled = showMarker;
    }
}