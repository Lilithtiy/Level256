using UnityEngine;

public class GeneratorTask : StationTask
{
    [SerializeField] private float repairSeconds = 10f; // Time for 1 player

    public override TaskMode Mode => TaskMode.Hold;
    public override string Prompt => "Hold E to repair";

    // Server advances progress based on the number of workers, and more players working together complete the repair faster
    protected override void ServerTick(float deltaTime)
    {
        // 2 players are twice as fast
        Progress.Value = Mathf.Min(1f, Progress.Value + WorkerCount * deltaTime / repairSeconds);
    }
}