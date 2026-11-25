using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class RelayTask : StationTask
{
    private const int TxIndex = 0;
    private const int RxIndex = 1;

    [Header("Consoles")]
    [SerializeField] private Transform transmitter;
    [SerializeField] private Transform receiver;
    [Tooltip("Lamps are blue while a player is at that console, orange when empty")]
    [SerializeField] private Renderer transmitterLamp;
    [SerializeField] private Renderer receiverLamp;
    [SerializeField] private Color presentColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color emptyColor = new Color(1f, 0.55f, 0.1f);

    [Header("Relay")]
    [SerializeField] private int rounds = 3;
    [Tooltip("Digits in the first number. It grows by one each round")]
    [SerializeField] private int startDigits = 4;
    [SerializeField] private float wrongNoiseRadius = 20f;
    [SerializeField] private float wrongNoiseSeconds = 3f;

    // Server-owned relay state synchronized with clients
    public NetworkVariable<int> Code = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RoundNo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Bit 0 = someone at the transmitter, bit 1 = someone at the receiver
    public NetworkVariable<int> Present = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override TaskMode Mode => TaskMode.Panel;
    public override string Prompt => "Press E to use the relay console";
    public override string BoardText => "RELAY LINK\n2 PLAYERS";
    public override int MinPlayers => 2;
    public override string HudDetail => $"{PresentCount()}/2 AT CONSOLES";

    private IVisualElementScheduledItem waitTimeout;

    // helpers
    private static float Flat(Vector3 position, Transform t)
    {
        if (t == null) return float.MaxValue;
        Vector3 offset = position - t.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    // 0 = transmitter, 1 = receiver, -1 = not near either
    private int NearestConsole(Vector3 position)
    {
        float dTx = Flat(position, transmitter);
        float dRx = Flat(position, receiver);
        if (dTx > MaxDistance && dRx > MaxDistance) return -1;
        return dTx <= dRx ? TxIndex : RxIndex;
    }

    public override bool InRange(Vector3 position) => NearestConsole(position) >= 0;

    public override string PromptAt(Vector3 point)
    {
        return Flat(point, transmitter) <= Flat(point, receiver) ? "Press E to use the transmitter (read the number out loud)"  : "Press E to use the receiver (type in the number)";
    }

    private int PresentCount() => ((Present.Value & 1) != 0 ? 1 : 0) + ((Present.Value & 2) != 0 ? 1 : 0);

    // server helpers
    private int ConsoleOf(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
            return -1;
        return NearestConsole(client.PlayerObject.transform.position);
    }

    private int ComputePresent()
    {
        int mask = 0;
        foreach (ulong id in WorkerIds)
        {
            int c = ConsoleOf(id);
            if (c >= 0) mask |= 1 << c;
        }
        return mask;
    }

    // Generate a new number that gains a digit each round
    private void NewCode()
    {
        int digits = Mathf.Clamp(startDigits + RoundNo.Value, 3, 9);
        int min = 1;
        for (int i = 1; i < digits; i++) min *= 10;
        Code.Value = Random.Range(min, min * 10);
    }

    protected override void OnRoundSetup()
    {
        RoundNo.Value = 0;
        NewCode();
    }

    protected override void OnWorkersChanged() => Present.Value = ComputePresent();

    // Emit sound at each occupied console so nearby ghosts can hear the task
    protected override void EmitWorkNoise(float radius, float seconds)
    {
        if ((Present.Value & 1) != 0 && transmitter != null) EmitAlarmAt(transmitter.position, radius, seconds);
        if ((Present.Value & 2) != 0 && receiver != null) EmitAlarmAt(receiver.position, radius, seconds);
    }

    // Validate submissions on the server and advance the relay when correct
    [Rpc(SendTo.Server)]
    public void SubmitRelayRpc(int code, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        // Only a player standing at the receiver can enter numbers
        if (!CanWork || !IsWorker(sender) || ConsoleOf(sender) != RxIndex) return;

        if (code != Code.Value)
        {
            EmitAlarmAt(receiver.position, wrongNoiseRadius, wrongNoiseSeconds);
            RelayResultRpc(false, false);
            return;
        }

        RoundNo.Value++;
        bool finished = RoundNo.Value >= rounds;
        Progress.Value = Mathf.Min(1f, (float)RoundNo.Value / rounds);

        if (!finished) NewCode();
        RelayResultRpc(true, finished);
        if (finished) CheckDone();
    }

    [Rpc(SendTo.Everyone)]
    private void RelayResultRpc(bool ok, bool finished)
    {
        // Only players who currently have this panel open care
        if (TaskPanelHost.Instance != null && TaskPanelHost.Instance.Open == this)
            OnResult(ok, finished);
    }

    // panel ui (client)
    private VisualElement transmitView, receiveView;
    private Label roleLabel, instruction, numberLabel, entryLabel, linkLabel, roundLabel;
    private string entry = "";
    private bool waiting;
    private int role = RxIndex;
    private int shownCode = -1, shownRound = -1, shownPresent = -1;

    private int Digits => Code.Value.ToString().Length;

    private int LocalConsole()
    {
        var obj = NetworkManager.Singleton.LocalClient?.PlayerObject;
        return obj == null ? -1 : NearestConsole(obj.transform.position);
    }

    // Build the appropriate panel for the local player's console
    public override void BindPanel(VisualElement root)
    {
        roleLabel = root.Q<Label>("role");
        instruction = root.Q<Label>("instruction");
        transmitView = root.Q("transmitter");
        receiveView = root.Q("receiver");
        numberLabel = root.Q<Label>("number");
        entryLabel = root.Q<Label>("entry");
        linkLabel = root.Q<Label>("link");
        roundLabel = root.Q<Label>("round");

        var keys = root.Q("keys");
        keys.Clear();
        foreach (string k in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" })
        {
            string key = k;
            var b = new Button { text = key };
            b.AddToClassList("key");
            b.clicked += () => Press(key);
            keys.Add(b);
        }

        role = LocalConsole() == TxIndex ? TxIndex : RxIndex;

        transmitView.style.display = role == TxIndex ? DisplayStyle.Flex : DisplayStyle.None;
        receiveView.style.display = role == RxIndex ? DisplayStyle.Flex : DisplayStyle.None;

        if (role == TxIndex)
        {
            roleLabel.text = "CONSOLE: TRANSMITTER";
            instruction.text = "Read the number out loud to your partner at the other console. They can't see it.";
        }
        else
        {
            roleLabel.text = "CONSOLE: RECEIVER";
            instruction.text = "Type in the number your partner reads out.";
        }

        entry = "";
        waiting = false;
        shownCode = shownRound = shownPresent = -1;
        Refresh();
    }

    public override void TickPanel() => Refresh();

    // Refresh the panel only when synchronized task state changes
    private void Refresh()
    {
        if (Code.Value == shownCode && RoundNo.Value == shownRound && Present.Value == shownPresent) return;

        if (Code.Value != shownCode)
        {
            entry = "";
            waiting = false;
        }

        shownCode = Code.Value;
        shownRound = RoundNo.Value;
        shownPresent = Present.Value;

        roundLabel.text = $"ROUND {Mathf.Min(RoundNo.Value + 1, rounds)}/{rounds}";

        bool partner = Present.Value == 3;
        linkLabel.text = partner ? "LINK ESTABLISHED" : "WAITING FOR YOUR PARTNER AT THE OTHER CONSOLE...";
        linkLabel.EnableInClassList("panel-status--good", partner);

        if (role == TxIndex)
            numberLabel.text = string.Join(" ", Code.Value.ToString().ToCharArray());
        else
            UpdateEntry();
    }

    private void UpdateEntry() => entryLabel.text = string.Join(" ", entry.PadRight(Digits, '_').ToCharArray());

    // Handle keypad input and submit once all digits have been entered
    private void Press(string key)
    {
        if (waiting) return;

        if (key == "C")
        {
            entry = "";
        }
        else if (key == "OK")
        {
            if (entry.Length != Digits)
            {
                Say($"Enter all {Digits} digits", false, 1f);
                return;
            }
            waiting = true;
            SubmitRelayRpc(int.Parse(entry));
            waitTimeout?.Pause();
            waitTimeout = entryLabel.schedule.Execute(() => waiting = false).StartingIn(2500);
            return;
        }
        else if (entry.Length < Digits)
        {
            entry += key;
        }

        UpdateEntry();
    }

    private void OnResult(bool ok, bool finished)
    {
        waitTimeout?.Pause();
        waiting = false;
        entry = "";
        if (entryLabel != null) UpdateEntry();

        if (finished) Say("Link restored!", true, 2f);
        else if (ok) Say("Number accepted. Next number incoming...", true, 2f);
        else Say("Wrong number! The alarm is loud!", false, 2f);
    }

    // visuals, update console indicator lamps when player presence changes
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Present.OnValueChanged += OnPresentChanged;
        RefreshVisuals();
    }

    public override void OnNetworkDespawn()
    {
        Present.OnValueChanged -= OnPresentChanged;
        base.OnNetworkDespawn();
    }

    private void OnPresentChanged(int previous, int current) => RefreshVisuals();

    protected override void RefreshVisuals()
    {
        base.RefreshVisuals();

        // While the task is running, the lamps show who is at a console
        if (!IsActive.Value || IsDone.Value) return;

        if (transmitterLamp != null)
            transmitterLamp.material.color = (Present.Value & 1) != 0 ? presentColor : emptyColor;
        if (receiverLamp != null)
            receiverLamp.material.color = (Present.Value & 2) != 0 ? presentColor : emptyColor;
    }

    // See in the Scene view, that shows the reach of each console while the task is selected
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (transmitter != null) Gizmos.DrawWireSphere(transmitter.position, MaxDistance);
        Gizmos.color = Color.magenta;
        if (receiver != null) Gizmos.DrawWireSphere(receiver.position, MaxDistance);
    }
}