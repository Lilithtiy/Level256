using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class WireTask : StationTask
{
    private static readonly Color[] WireColors =
    {
        new Color32(230, 60, 60, 255), new Color32(70, 110, 255, 255), new Color32(60, 200, 90, 255),
        new Color32(245, 210, 40, 255), new Color32(170, 80, 230, 255)
    };

    [Tooltip("Wires per panel (2 to 5)")]
    [SerializeField] private int wireCount = 4;
    [SerializeField] private int rounds = 2;

    public override TaskMode Mode => TaskMode.Panel;
    public override string Prompt => "Press E to use the wire panel";

    private VisualElement board, leftCol, rightCol;
    private Label roundLabel;
    private WireCanvas canvas;
    private Button[] left, right;
    private int[] leftColor, rightColor;
    private bool[] leftDone, rightDone;
    private int selected = -1, connected, roundNo;
    private bool locked;

    public override void BindPanel(VisualElement root)
    {
        board = root.Q("board");
        leftCol = root.Q("left");
        rightCol = root.Q("right");
        roundLabel = root.Q<Label>("round");

        canvas = new WireCanvas();
        board.Insert(0, canvas);

        roundNo = Mathf.Min(rounds - 1, Mathf.RoundToInt(Progress.Value * rounds));
        NewRound();
    }

    public override void TickPanel() => canvas?.MarkDirtyRepaint();

    private void NewRound()
    {
        int n = Mathf.Clamp(wireCount, 2, WireColors.Length);

        var order = new List<int> { 0, 1, 2, 3, 4 };
        Shuffle(order);

        leftColor = new int[n];
        rightColor = new int[n];
        for (int i = 0; i < n; i++) leftColor[i] = rightColor[i] = order[i];

        do { Shuffle(rightColor); } while (SameOrder(leftColor, rightColor));

        leftCol.Clear();
        rightCol.Clear();
        canvas.Wires.Clear();

        left = new Button[n];
        right = new Button[n];
        leftDone = new bool[n];
        rightDone = new bool[n];
        selected = -1;
        connected = 0;
        locked = false;

        for (int i = 0; i < n; i++)
        {
            int index = i;
            left[i] = MakeNode(leftColor[i], leftCol);
            left[i].clicked += () => ClickLeft(index);
            right[i] = MakeNode(rightColor[i], rightCol);
            right[i].clicked += () => ClickRight(index);
        }

        roundLabel.text = $"Panel {roundNo + 1}/{rounds}";
    }

    private static Button MakeNode(int color, VisualElement parent)
    {
        var b = new Button();
        b.AddToClassList("wire-node");
        b.AddToClassList($"c{color}");
        SymbolIcon.Overlay(b, color, 28);
        parent.Add(b);
        return b;
    }

    private void ClickLeft(int i)
    {
        if (locked || leftDone[i]) return;

        if (selected >= 0) left[selected].RemoveFromClassList("wire-node--selected");
        selected = i;
        left[i].AddToClassList("wire-node--selected");
    }

    private void ClickRight(int j)
    {
        if (locked || rightDone[j] || selected < 0) return;

        if (rightColor[j] == leftColor[selected])
        {
            leftDone[selected] = true;
            rightDone[j] = true;
            left[selected].RemoveFromClassList("wire-node--selected");
            left[selected].AddToClassList("wire-node--done");
            right[j].AddToClassList("wire-node--done");
            canvas.Wires.Add((left[selected], right[j], WireColors[leftColor[selected]]));

            selected = -1;
            connected++;
            if (connected == left.Length) RoundComplete();
        }
        else
        {
            ReportMistakeRpc();
            Say("Wrong wire! The alarm is loud!", false, 2f);

            var wrong = right[j];
            wrong.AddToClassList("wire-node--wrong");
            wrong.schedule.Execute(() => wrong.RemoveFromClassList("wire-node--wrong")).StartingIn(350);

            left[selected].RemoveFromClassList("wire-node--selected");
            selected = -1;
        }
    }

    private void RoundComplete()
    {
        locked = true;
        AddProgressRpc(1f / rounds);
        roundNo++;

        if (roundNo >= rounds)
        {
            Say("Wiring restored!", true, 2f);
            return;
        }

        Say("Connected! Next panel...", true, 1f);
        board.schedule.Execute(NewRound).StartingIn(900);
    }

    private static bool SameOrder(int[] a, int[] b)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}