using Unity.VisualScripting;
using UnityEngine;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    private static GameManager gameManagerInstance;

    public static GameManager gameManager
    {
        get
        {
            if (gameManagerInstance != null)
            {
                return gameManagerInstance;
            }
            return null;
        }
    }

    private void Awake()
    {
        if (gameManagerInstance != null && gameManagerInstance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            gameManagerInstance = this;
        }
    }

    public NetworkVariable<int> electricityScore { get; private set; } = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> highScore { get; private set; } = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public void UpdateElectricityScore(int value)
    {
        if(!IsServer) { return; }

        electricityScore.Value = Mathf.Clamp(electricityScore.Value + value, 0, 100);
        Debug.Log("Electricity Score: " + electricityScore.Value);
    }

    public void UpdateHighScore(int value)
    {
        if(!IsServer) { return; }

        highScore.Value = Mathf.Clamp(highScore.Value + value.ConvertTo<float>(), 0, float.MaxValue);
        Debug.Log("High Score: " + highScore.Value);
    }
}
