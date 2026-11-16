using System.Collections.Generic;
using UnityEngine;

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

    public override void OnPanelOpened()
    {
        pulled.Clear();
        ShowMessage("");
    }

    public override void DrawPanel(Rect area)
    {
        var label = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 26 };

        GUILayout.BeginArea(area);

        GUILayout.Label("Pull the levers in the order shown on the signal board", label);
        GUILayout.Space(30);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        for (int i = 0; i < 4; i++)
        {
            bool isPulled = pulled.Contains(i);
            GUI.backgroundColor = isPulled ? Color.green : Color.white;

            string text = isPulled ? $"{Letter(i)}\n({pulled.IndexOf(i) + 1})" : Letter(i).ToString();
            if (GUILayout.Button(text, button, GUILayout.Width(90), GUILayout.Height(110)) && !isPulled)
            {
                pulled.Add(i);
                if (pulled.Count == 4) Submit();
            }
        }
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(20);
        GUILayout.Label(CurrentMessage, label);

        GUILayout.EndArea();
    }

    private void Submit()
    {
        int code = pulled[0] * 1000 + pulled[1] * 100 + pulled[2] * 10 + pulled[3];

        SubmitCodeRpc(code);
        ShowMessage(code == Code.Value ? "Signals aligned!" : "Wrong order! The alarm is loud!");
        pulled.Clear();
    }
}