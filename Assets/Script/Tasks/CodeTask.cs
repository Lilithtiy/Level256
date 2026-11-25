using Unity.Netcode;
using UnityEngine;

public abstract class CodeTask : StationTask
{
    [Header("Code task")]
    [SerializeField] private float wrongNoiseRadius = 20f;
    [SerializeField] private float wrongNoiseSeconds = 3f;

    // The server sets the answer and synchronizes it with clients
    public NetworkVariable<int> Code = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override TaskMode Mode => TaskMode.Panel;

    // Validate the submitted answer on the server and report the result
    [Rpc(SendTo.Server)]
    public void SubmitCodeRpc(int code, RpcParams rpcParams = default)
    {
        if (!CanWork || !IsWorker(rpcParams.Receive.SenderClientId)) return;

        bool ok = code == Code.Value;
        if (ok) FinishNow();
        else EmitAlarm(wrongNoiseRadius, wrongNoiseSeconds);

        CodeResultRpc(ok); // tells the panel whether the answer was correct

    }

    [Rpc(SendTo.Everyone)]
    private void CodeResultRpc(bool ok)
    {
        // Only players who currently have this panel open care
        if (TaskPanelHost.Instance != null && TaskPanelHost.Instance.Open == this)
            OnCodeResult(ok);
    }

    // Override this in derived tasks to handle the submitted-code result
    protected virtual void OnCodeResult(bool ok) { }
}