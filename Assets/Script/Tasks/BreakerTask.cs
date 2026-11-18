using UnityEngine;
using UnityEngine.UIElements;

public class BreakerTask : StationTask
{
    [SerializeField] private int switchCount = 6;
    [SerializeField] private int rounds = 2;

    public override TaskMode Mode => TaskMode.Panel;
    public override string Prompt => "Press E to use the breaker panel";

    private bool[] switches, pattern;
    private VisualElement patternRow, switchRow;
    private Label roundLabel;

    public override void BindPanel(VisualElement root)
    {
        patternRow = root.Q("pattern");
        switchRow = root.Q("switches");
        roundLabel = root.Q<Label>("round");
        NewPuzzle();
    }

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

        patternRow.Clear();
        switchRow.Clear();

        for (int i = 0; i < switchCount; i++)
        {
            var lamp = new Label(pattern[i] ? "ON" : "OFF");
            lamp.AddToClassList("lamp");
            lamp.EnableInClassList("lamp--on", pattern[i]);
            patternRow.Add(lamp);

            int index = i;
            var sw = new Button { text = "OFF" };
            sw.AddToClassList("switch");
            sw.clicked += () => Toggle(index, sw);
            switchRow.Add(sw);
        }

        int round = Mathf.Min(rounds, Mathf.RoundToInt(Progress.Value * rounds) + 1);
        roundLabel.text = $"Round {round}/{rounds}";
    }

    private void Toggle(int index, Button sw)
    {
        switches[index] = !switches[index];
        sw.text = switches[index] ? "ON" : "OFF";
        sw.EnableInClassList("switch--on", switches[index]);

        for (int i = 0; i < switchCount; i++)
            if (switches[i] != pattern[i]) return;

        AddProgressRpc(1f / rounds);
        Say("Solved!", true, 1f);
        NewPuzzle();
    }
}