using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum TaskMode { Hold, Panel }

public abstract class StationTask : NetworkBehaviour
{
    [Header("Task")]
    [SerializeField] private string taskName = "Task";
    [SerializeField] private float maxDistance = 4f;
    [SerializeField] private float noiseRadius = 10f;
    [SerializeField] private Color brokenColor = Color.red;
    [SerializeField] private Color fixedColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.gray;

    public NetworkVariable<float> Progress = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsDone = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsActive = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Workers = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string TaskName => taskName;
    public abstract TaskMode Mode { get; }
    public virtual string Prompt => $"Press E to use {taskName}";

    private readonly HashSet<ulong> workers = new();
    protected int WorkerCount => workers.Count;

    private Renderer[] renderers;
    private MarkerTarget marker;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        marker = GetComponent<MarkerTarget>();
    }

    public override void OnNetworkSpawn()
    {
        Progress.OnValueChanged += OnFloatChanged;
        IsDone.OnValueChanged += OnBoolChanged;
        IsActive.OnValueChanged += OnBoolChanged;
        RefreshVisuals();
    }

    public override void OnNetworkDespawn()
    {
        Progress.OnValueChanged -= OnFloatChanged;
        IsDone.OnValueChanged -= OnBoolChanged;
        IsActive.OnValueChanged -= OnBoolChanged;
    }

    public virtual void OnPanelOpened() { }
    public virtual void DrawPanel(Rect area) { }


    [Rpc(SendTo.Server)]
    public void SetWorkingRpc(bool working, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (!working || !CanWork || !IsCloseEnough(sender))
            workers.Remove(sender);
        else
            workers.Add(sender);

        Workers.Value = workers.Count;
    }

    [Rpc(SendTo.Server)]
    public void AddProgressRpc(float amount, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!workers.Contains(sender) || !CanWork) return;

        Progress.Value = Mathf.Min(1f, Progress.Value + Mathf.Clamp01(amount));
        CheckDone();
    }

    protected bool CanWork =>
    IsActive.Value && !IsDone.Value &&
        GameManager.Instance != null && GameManager.Instance.State.Value == GameState.Playing;

    protected bool IsWorker(ulong clientId) => workers.Contains(clientId);

    private void Update()
    {
        if (!IsServer || !IsActive.Value || IsDone.Value || workers.Count == 0) return;

        workers.RemoveWhere(id => !IsCloseEnough(id));
        Workers.Value = workers.Count;
        if (workers.Count == 0) return;

        ServerTick(Time.deltaTime); 

        if (NoiseTracker.Instance != null)
            NoiseTracker.Instance.EmitNoise(transform.position, noiseRadius, 0.3f);

        CheckDone();
    }

    protected virtual void ServerTick(float deltaTime) { }

    protected void CheckDone()
    {
        if (IsDone.Value || Progress.Value < 1f) return;

        IsDone.Value = true;
        workers.Clear();
        Workers.Value = 0;

        if (TaskManager.Instance != null)
            TaskManager.Instance.TaskCompleted(this);
    }

    public void ServerSetup(bool active)
    {
        workers.Clear();
        Workers.Value = 0;
        Progress.Value = 0f;
        IsDone.Value = false;
        IsActive.Value = active;
        if (active) OnRoundSetup();
    }

    protected virtual void OnRoundSetup() { }

    private bool IsCloseEnough(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
            return false;

        Vector3 offset = client.PlayerObject.transform.position - transform.position;
        offset.y = 0f;
        return offset.magnitude <= maxDistance;
    }


    private void OnFloatChanged(float previous, float current) => RefreshVisuals();
    private void OnBoolChanged(bool previous, bool current) => RefreshVisuals();

    private void RefreshVisuals()
    {
        Color c = !IsActive.Value ? inactiveColor : Color.Lerp(brokenColor, fixedColor, Progress.Value);
        foreach (var r in renderers) r.material.color = c;

        if (marker != null) marker.enabled = IsActive.Value && !IsDone.Value;
    }

    protected void FinishNow()
    {
        Progress.Value = 1f;
        CheckDone();
    }

    public virtual string BoardText => "";

}