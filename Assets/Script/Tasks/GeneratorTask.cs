using UnityEngine;

public class GeneratorTask : StationTask
{
    [SerializeField] private float repairSeconds = 10f;   // time for ONE player

    public override TaskMode Mode => TaskMode.Hold;
    public override string Prompt => "Hold E to repair";

    protected override void ServerTick(float deltaTime)
    {
        // 2 players are twice as fast
        Progress.Value = Mathf.Min(1f, Progress.Value + WorkerCount * deltaTime / repairSeconds);
    }
}