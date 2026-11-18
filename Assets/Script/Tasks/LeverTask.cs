using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class LeverTask : CodeTask
{
    public override string Prompt => "Press E to use the signal levers";

    public override string BoardText
    {
        get
        {
            int c = Code.Value;
            return $"ORDER\n{Letter(c / 1000 % 10)} {Letter(c / 100 % 10)} {Letter(c / 10 % 10)} {Letter(c % 10)}";
        }
    }

    private readonly List<int> pulled = new();
    private readonly Button[] buttons = new Button[4];
    private bool waiting;

    private static char Letter(int index) => (char)('A' + index);

    protected override void OnRoundSetup()
    {
        var order = new List<int> { 0, 1, 2, 3 };
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        Code.Value = order[0] * 1000 + order[1] * 100 + order[2] * 10 + order[3];
    }

    public override void BindPanel(VisualElement root)
    {
        var row = root.Q("levers");
        row.Clear();
        pulled.Clear();
        waiting = false;

        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var b = new Button { text = Letter(i).ToString() };
            b.AddToClassList("lever");
            b.clicked += () => Pull(index);
            buttons[i] = b;
            row.Add(b);
        }
    }

    private void Pull(int index)
    {
        if (waiting || pulled.Contains(index)) return;

        pulled.Add(index);
        buttons[index].AddToClassList("lever--pulled");
        buttons[index].text = $"{Letter(index)}\n({pulled.Count})";

        if (pulled.Count < 4) return;

        waiting = true;
        SubmitCodeRpc(pulled[0] * 1000 + pulled[1] * 100 + pulled[2] * 10 + pulled[3]);
    }

    protected override void OnCodeResult(bool ok)
    {
        waiting = false;
        Say(ok ? "Signals aligned!" : "Wrong order! The alarm is loud!", ok, 2f);
        if (ok) return;

        pulled.Clear();
        for (int i = 0; i < 4; i++)
        {
            buttons[i].RemoveFromClassList("lever--pulled");
            buttons[i].text = Letter(i).ToString();
        }
    }
}