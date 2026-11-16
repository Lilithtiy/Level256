using UnityEngine;

public class BreakerTask : StationTask
{
    [SerializeField] private int switchCount = 6;
    [SerializeField] private int rounds = 2;

    public override TaskMode Mode => TaskMode.Panel;
    public override string Prompt => "Press E to use the breaker panel";

    private bool[] switches;
    private bool[] pattern;
    private float solvedFlashUntil;

    public override void OnPanelOpened() => NewPuzzle();

    private void NewPuzzle()
    {
        switches = new bool[switchCount];
        pattern = new bool[switchCount];

        bool anyOn = false;
        for (int i = 0; i < switchCount; i++)
        {
            pattern[i] = Random.value > 0.5f;
            anyOn |= pattern[i];
        }
        if (!anyOn) pattern[Random.Range(0, switchCount)] = true;
    }

    public override void DrawPanel(Rect area)
    {
        if (switches == null) NewPuzzle();

        var label = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        var small = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 22 };

        GUILayout.BeginArea(area);

        GUILayout.Label("Match the pattern", label);
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        for (int i = 0; i < switchCount; i++)
        {
            small.normal.textColor = pattern[i] ? Color.green : Color.red;
            GUILayout.Label(pattern[i] ? "ON" : "OFF", small, GUILayout.Width(80));
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        for (int i = 0; i < switchCount; i++)
        {
            GUI.backgroundColor = switches[i] ? Color.green : Color.red;
            if (GUILayout.Button(switches[i] ? "ON" : "OFF", button, GUILayout.Width(80), GUILayout.Height(70)))
            {
                switches[i] = !switches[i];
                CheckSolved();
            }
        }
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label(Time.time < solvedFlashUntil ? "Solved!" : " ", label);

        GUILayout.EndArea();
    }

    private void CheckSolved()
    {
        for (int i = 0; i < switchCount; i++)
            if (switches[i] != pattern[i]) return;

        AddProgressRpc(1f / rounds);
        solvedFlashUntil = Time.time + 1f;
        NewPuzzle();
    }
}