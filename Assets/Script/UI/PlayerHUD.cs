using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHUD : MonoBehaviour
{
    VisualElement uiDocument;
    private ProgressBar electricityBar;
    private Label highScoreText;

    private ScoreManager gameManager;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>().rootVisualElement;

        electricityBar = uiDocument.Q<ProgressBar>("ElectricityBar");
        highScoreText = uiDocument.Q<Label>("HighScoreText");

        electricityBar.lowValue = 0;
        electricityBar.highValue = 100;
        electricityBar.value = 0;
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.electricityScore.OnValueChanged -= OnElectricityScoreChanged;
            gameManager.highScore.OnValueChanged -= OnHighScoreChanged;

            gameManager = null;
        }
    }

    private void Update()
    {
        if(gameManager == null)
        {
            ScoreManager potentialGameManager = ScoreManager.gameManager;

            if(potentialGameManager == null || !potentialGameManager.IsSpawned) { return; }
            gameManager = potentialGameManager;

            gameManager.electricityScore.OnValueChanged += OnElectricityScoreChanged;
            gameManager.highScore.OnValueChanged += OnHighScoreChanged;

            // Initialize the UI with the current values
            OnElectricityScoreChanged(0, gameManager.electricityScore.Value);
            OnHighScoreChanged(0, gameManager.highScore.Value);
        }
    }

    private void OnElectricityScoreChanged(int previousValue, int newValue)
    {
        electricityBar.value = newValue;
        electricityBar.title = $"Electricity: {newValue}/100";
    }

    private void OnHighScoreChanged(float previousValue, float newValue)
    {
        highScoreText.text = $"High Score: {newValue}";
    }

}
