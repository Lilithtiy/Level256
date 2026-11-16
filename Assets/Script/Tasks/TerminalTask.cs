using UnityEngine;

public class TerminalTask : CodeTask
{
    public override string Prompt => "Press E to use the terminal";
    public override string BoardText => $"CODE\n{Code.Value}";

    private string entry = "";

    protected override void OnRoundSetup()
    {
        Code.Value = Random.Range(1000, 10000);
    }

    public override void OnPanelOpened()
    {
        entry = "";
        ShowMessage("");
    }

    public override void DrawPanel(Rect area)
    {
        var label = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        var big = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 22 };

        GUILayout.BeginArea(area);

        GUILayout.Label("Enter the code from the board", label);

        string shown = string.Join(" ", entry.PadRight(4, '_').ToCharArray());
        GUILayout.Label(shown, big);

        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
        for (int row = 0; row < 4; row++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int col = 0; col < 3; col++)
            {
                string key = keys[row * 3 + col];
                if (GUILayout.Button(key, button, GUILayout.Width(80), GUILayout.Height(38)))
                    PressKey(key);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        GUILayout.Label(CurrentMessage, label);
        GUILayout.EndArea();
    }

    private void PressKey(string key)
    {
        if (key == "C")
        {
            entry = "";
            return;
        }

        if (key == "OK")
        {
            if (entry.Length == 4 && int.TryParse(entry, out int code))
            {
                SubmitCodeRpc(code);
                ShowMessage(code == Code.Value ? "Accepted" : "WRONG CODE! The alarm is loud!");
            }
            entry = "";
            return;
        }

        if (entry.Length < 4) entry += key;
    }
}