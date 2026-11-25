using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class SyncTask : CodeTask
{
    private static readonly string[] Symbols = { "ALPHA", "BRAVO", "CHARLIE", "DELTA" };
    private static readonly string[] ColorNames = { "RED", "BLUE", "GREEN", "YELLOW" };
    private static readonly int[] Pow10 = { 1000, 100, 10, 1 };
    private const ulong None = ulong.MaxValue;

    // Server-generated puzzle data and the two assigned player roles
    public NetworkVariable<int> Legend = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> ReaderId = new(None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> OperatorId = new(None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override string Prompt => "Press E to use the signal decoder (needs 2 players)";
    public override string BoardText => "TEAM TASK\n2 PLAYERS";
    public override int MinPlayers => 2;

    public override string HudDetail => $"{Mathf.Min(Workers.Value, 2)}/2 AT PANEL";

    // Read one digit from a four-digit value by position
    private static int Digit(int value, int pos) => value / Pow10[pos] % 10;

    // Server creates a shuffled color legend and a random code for each round
    protected override void OnRoundSetup()
    {
        var colors = new List<int> { 0, 1, 2, 3 };
        for (int i = colors.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (colors[i], colors[j]) = (colors[j], colors[i]);
        }
        Legend.Value = colors[0] * 1000 + colors[1] * 100 + colors[2] * 10 + colors[3];

        // The color sequence the operator must enter
        int code = 0;
        for (int i = 0; i < 4; i++) code = code * 10 + Random.Range(0, 4);
        Code.Value = code;
    }

    // First player at the panel reads, second one operates
    protected override void OnWorkersChanged()
    {
        ReaderId.Value = WorkerIds.Count > 0 ? WorkerIds[0] : None;
        OperatorId.Value = WorkerIds.Count > 1 ? WorkerIds[1] : None;
    }

    // panel ui
    private VisualElement readerView, operatorView, legendRow, sequenceRow;
    private Label roleLabel, instruction, entered;
    private readonly List<int> entry = new();
    private bool waiting;
    private int shownRole = -1, shownCode = -1, shownLegend = -1;

    public override void BindPanel(VisualElement root)
    {
        roleLabel = root.Q<Label>("role");
        instruction = root.Q<Label>("instruction");
        readerView = root.Q("reader");
        operatorView = root.Q("operator");
        legendRow = root.Q("legend");
        sequenceRow = root.Q("sequence");
        entered = root.Q<Label>("entered");

        var pads = root.Q("pads");
        pads.Clear();
        for (int c = 0; c < 4; c++)
        {
            int color = c;
            var b = new Button();
            b.AddToClassList("pad");
            b.AddToClassList($"c{c}");
            SymbolIcon.Overlay(b, c, 44);
            b.clicked += () => Press(color);
            pads.Add(b);
        }

        entry.Clear();
        waiting = false;
        shownRole = shownCode = shownLegend = -1;
        Refresh();
    }

    public override void TickPanel() => Refresh();

    // Show the reader or operator view based on the local player's assigned role
    private void Refresh()
    {
        ulong me = NetworkManager.Singleton.LocalClientId;

        int role; // 0 = waiting for partner, 1 = reader, 2 = operator
        if (ReaderId.Value == None || OperatorId.Value == None) role = 0;
        else role = OperatorId.Value == me ? 2 : 1; // Other players see the reader

        if (role == shownRole && Code.Value == shownCode && Legend.Value == shownLegend) return;
        if (role != shownRole) { entry.Clear(); waiting = false; }
        shownRole = role;
        shownCode = Code.Value;
        shownLegend = Legend.Value;

        readerView.style.display = role == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        operatorView.style.display = role == 2 ? DisplayStyle.Flex : DisplayStyle.None;

        switch (role)
        {
            case 0:
                roleLabel.text = "";
                instruction.text = "Waiting for a partner at the panel...";
                break;
            case 1:
                roleLabel.text = "ROLE: READER";
                instruction.text = "Tell your partner which Colors (or shapes) to press, in order. They can't see this.";
                BuildReader();
                break;
            case 2:
                roleLabel.text = "ROLE: OPERATOR";
                instruction.text = "Press the Colors (or shapes) your partner tells you.";
                entered.text = "0/4";
                break;
        }
    }

    // Build the reader's color legend and translate the code into symbols
    private void BuildReader()
    {
        legendRow.Clear();
        sequenceRow.Clear();

        for (int s = 0; s < 4; s++)
        {
            int color = Digit(Legend.Value, s);

            var row = new VisualElement();
            row.AddToClassList("token");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var text = new Label($"{Symbols[s]} = {ColorNames[color]}");
            text.AddToClassList($"t{color}");
            row.Add(text);

            var icon = new SymbolIcon(color, 26);
            icon.style.marginLeft = 10;
            row.Add(icon);

            legendRow.Add(row);
        }

        for (int i = 0; i < 4; i++)
        {
            int color = Digit(Code.Value, i);
            int symbol = 0;
            for (int s = 0; s < 4; s++)
                if (Digit(Legend.Value, s) == color) symbol = s;

            var t = new Label($"{i + 1}. {Symbols[symbol]}");
            t.AddToClassList("token");
            sequenceRow.Add(t);
        }
    }

    // Collect four color inputs and submit the sequence to the server
    private void Press(int color)
    {
        if (waiting || entry.Count >= 4) return;

        entry.Add(color);
        entered.text = $"{entry.Count}/4";
        if (entry.Count < 4) return;

        waiting = true;
        SubmitCodeRpc(entry[0] * 1000 + entry[1] * 100 + entry[2] * 10 + entry[3]);
    }

    protected override void OnCodeResult(bool ok)
    {
        waiting = false;
        entry.Clear();
        if (entered != null) entered.text = "0/4";
        Say(ok ? "Decoded!" : "Wrong sequence! The alarm is loud!", ok, 2f);
    }
}