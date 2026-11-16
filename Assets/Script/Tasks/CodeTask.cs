using Unity.Netcode;
using UnityEngine;

public abstract class CodeTask : StationTask
{
    [Header("Code task")]
    [SerializeField] private float wrongNoiseRadius = 20f;
    [SerializeField] private float wrongNoiseSeconds = 3f;

    public NetworkVariable<int> Code = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override TaskMode Mode => TaskMode.Panel;

    private string message = "";
    private float messageUntil;

    // Host checks the answer
    [Rpc(SendTo.Server)]
    public void SubmitCodeRpc(int code, RpcParams rpcParams = default)
    {
        if (!CanWork || !IsWorker(rpcParams.Receive.SenderClientId)) return;

        if (code == Code.Value)
        {
            FinishNow();
        }
        else if (NoiseTracker.Instance != null)
        {
            // A wrong answer sets off an alarm that ghosts can hear
            NoiseTracker.Instance.EmitNoise(transform.position, wrongNoiseRadius, wrongNoiseSeconds);
        }
    }

    protected void ShowMessage(string text, float seconds = 1.5f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    protected string CurrentMessage =>
        (Time.time < messageUntil && !string.IsNullOrEmpty(message)) ? message : " ";
}