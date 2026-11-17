using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SequenceTask : StationTask
{
    [SerializeField] private int rounds = 3;
    [SerializeField] private int startLength = 3;

    public override TaskMode Mode => TaskMode.Panel;
    public override string Prompt => "Press E to use the memory console";

    private enum Phase { Show, Input, Done }

    private readonly List<int> sequence = new();
    private readonly Button[] pads = new Button[4];
    private Label info;
    private Phase phase;
    private int roundNo, showIndex, lit = -1, inputIndex;
    private float nextStep;

    public override void BindPanel(VisualElement root)
    {
        info = root.Q<Label>("info");
        var row = root.Q("pads");
        row.Clear();

        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var b = new Button();
            b.AddToClassList("pad");
            b.AddToClassList($"c{i}");
            b.AddToClassList("simon");
            SymbolIcon.Overlay(b, i, 44);
            b.clicked += () => Press(index);
            pads[i] = b;
            row.Add(b);
        }

        roundNo = Mathf.Min(rounds - 1, Mathf.RoundToInt(Progress.Value * rounds));
        Begin(1f, true);
    }

    private void Begin(float delay, bool newSequence)
    {
        if (newSequence)
        {
            sequence.Clear();
            for (int i = 0; i < startLength + roundNo; i++) sequence.Add(Random.Range(0, 4));
        }

        showIndex = -1;
        lit = -1;
        phase = Phase.Show;
        SetPadsInteractive(false);
        nextStep = Time.time + delay;
        info.text = $"ROUND {roundNo + 1}/{rounds}  -  WATCH";
    }

    public override void TickPanel()
    {
        if (phase != Phase.Show || Time.time < nextStep) return;

        if (lit >= 0)
        {
            SetLit(lit, false);
            lit = -1;
            nextStep = Time.time + 0.3f;

            if (showIndex >= sequence.Count - 1)
            {
                phase = Phase.Input;
                SetPadsInteractive(true);
                inputIndex = 0;
                info.text = $"ROUND {roundNo + 1}/{rounds}  -  YOUR TURN";
            }
            return;
        }

        showIndex++;
        lit = sequence[showIndex];
        SetLit(lit, true);
        nextStep = Time.time + 0.65f;
    }

    private void SetLit(int i, bool on) => pads[i].EnableInClassList("pad--lit", on);

    private void Press(int i)
    {
        if (phase != Phase.Input) return;

        SetLit(i, true);
        pads[i].schedule.Execute(() => SetLit(i, false)).StartingIn(150);

        if (i != sequence[inputIndex])
        {
            ReportMistakeRpc();
            Say("Wrong! The alarm is loud!", false, 2f);
            Begin(1.2f, false);
            return;
        }

        inputIndex++;
        if (inputIndex < sequence.Count) return;

        AddProgressRpc(1f / rounds);
        roundNo++;
        Say("Sequence accepted", true);

        if (roundNo >= rounds)
        {
            phase = Phase.Done;
            info.text = "COMPLETE";
            return;
        }
        Begin(1.2f, true);
    }

    private void SetPadsInteractive(bool on)
    {
        foreach (var p in pads)
            if (p != null) p.pickingMode = on ? PickingMode.Position : PickingMode.Ignore;
    }
}