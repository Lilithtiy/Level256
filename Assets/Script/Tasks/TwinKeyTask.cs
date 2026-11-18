using Unity.Netcode;
using UnityEngine;

public class TwinKeyTask : StationTask
{
    [System.Serializable]
    public class Station
    {
        public Transform point;
        [Tooltip("Optional small lamp: green while this console is held, red when not")]
        public Renderer lamp;
    }

    [Header("Twin console")]
    [SerializeField] private Station[] stations = new Station[2];
    [SerializeField] private float holdSeconds = 10f;
    [SerializeField] private float slipPerSecond = 0.2f;
    [SerializeField] private Color heldColor = Color.green;
    [SerializeField] private Color idleColor = Color.red;

    public NetworkVariable<int> HeldMask = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override TaskMode Mode => TaskMode.Hold;
    public override string Prompt => "Hold E. Your partner must hold the other console at the same time";
    public override string BoardText => "TWIN LINK\nHOLD BOTH";
    public override int MinPlayers => Mathf.Max(2, stations.Length);

    public override string HoldLabel => $"{TaskName}   {HeldCount()}/{MinPlayers} consoles held";
    public override string HudDetail => $"{HeldCount()}/{MinPlayers} HELD";

    private int FullMask => (1 << stations.Length) - 1;

    // helpers (client + server)
    private int HeldCount()
    {
        int count = 0;
        for (int i = 0; i < stations.Length; i++)
            if ((HeldMask.Value & (1 << i)) != 0) count++;
        return count;
    }

    private int NearestStation(Vector3 position)
    {
        int best = -1;
        float bestDist = MaxDistance;

        for (int i = 0; i < stations.Length; i++)
        {
            var p = stations[i].point;
            if (p == null) continue;

            Vector3 offset = position - p.position;
            offset.y = 0f;
            float d = offset.magnitude;

            if (d <= bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    public override bool InRange(Vector3 position) => NearestStation(position) >= 0;

    // server
    // Each worker counts for their nearest console, so one player can never hold two.
    private int ComputeHeld()
    {
        int mask = 0;
        var nm = NetworkManager.Singleton;

        foreach (ulong id in WorkerIds)
        {
            if (!nm.ConnectedClients.TryGetValue(id, out var client) || client.PlayerObject == null) continue;

            int s = NearestStation(client.PlayerObject.transform.position);
            if (s >= 0) mask |= 1 << s;
        }
        return mask;
    }

    protected override void OnRoundSetup() => HeldMask.Value = 0;

    protected override void OnWorkersChanged() => HeldMask.Value = ComputeHeld();

    protected override void ServerTick(float deltaTime)
    {
        int mask = ComputeHeld();
        if (mask != HeldMask.Value) HeldMask.Value = mask;

        if (mask == FullMask)
            Progress.Value = Mathf.Min(1f, Progress.Value + deltaTime / holdSeconds);
        else
            Progress.Value = Mathf.Max(0f, Progress.Value - slipPerSecond * deltaTime);
    }

    protected override void ServerIdle(float deltaTime)
    {
        if (HeldMask.Value != 0) HeldMask.Value = 0;
        if (Progress.Value > 0f)
            Progress.Value = Mathf.Max(0f, Progress.Value - slipPerSecond * deltaTime);
    }

    // Ghosts hear whichever console is being held
    protected override void EmitWorkNoise(float radius, float seconds)
    {
        for (int i = 0; i < stations.Length; i++)
            if ((HeldMask.Value & (1 << i)) != 0 && stations[i].point != null)
                EmitAlarmAt(stations[i].point.position, radius, seconds);
    }

    // visuals
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        HeldMask.OnValueChanged += OnMaskChanged;
        RefreshVisuals();
    }

    public override void OnNetworkDespawn()
    {
        HeldMask.OnValueChanged -= OnMaskChanged;
        base.OnNetworkDespawn();
    }

    private void OnMaskChanged(int previous, int current) => RefreshVisuals();

    protected override void RefreshVisuals()
    {
        base.RefreshVisuals();

        // While the task is running, the lamps show which consoles are held
        if (!IsActive.Value || IsDone.Value) return;

        for (int i = 0; i < stations.Length; i++)
        {
            if (stations[i].lamp == null) continue;
            bool held = (HeldMask.Value & (1 << i)) != 0;
            stations[i].lamp.material.color = held ? heldColor : idleColor;
        }
    }

    // To see in the Scene view, shows the reach of each console while the task is selected
    private void OnDrawGizmosSelected()
    {
        if (stations == null) return;
        Gizmos.color = Color.cyan;
        foreach (var s in stations)
            if (s != null && s.point != null)
                Gizmos.DrawWireSphere(s.point.position, MaxDistance);
    }
}