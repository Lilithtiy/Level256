using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum GameState { Playing, GameOver }

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startingLives = 3;
    [SerializeField] private float safeSeconds = 3f;
    [SerializeField] private Transform[] respawnPoints;

    // Server writes, everyone reads
    public NetworkVariable<int> Lives = new(3, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<GameState> State = new(GameState.Playing, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Sever ONLY 
    public event System.Action OnGameRestarted;

    private readonly Dictionary<ulong, float> safeUntil = new();

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) Lives.Value = startingLives;
    }

    // its Called by ghosts. Server only. And it returns true if the catch counted
    public bool TryCatchPlayer(ulong clientId, Vector3 ghostPosition)
    {
        if (!IsServer || State.Value != GameState.Playing || Lives.Value <= 0) return false;

        if (safeUntil.TryGetValue(clientId, out float until) && Time.time < until)
            return false;

        safeUntil[clientId] = Time.time + safeSeconds;
        Lives.Value--;
        Debug.Log($"Caught client {clientId}. Lives left: {Lives.Value}");

        bool gameOver = Lives.Value <= 0;
        if (gameOver)
        {
            State.Value = GameState.GameOver;
            Debug.Log("GAME OVER");
        }

        var player = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        player.GetComponent<PlayerController>().CaughtRpc(ghostPosition, GetRespawnPosition(), gameOver);
        return true;
    }

    // Host only
    public void Restart()
    {
        if (!IsServer || State.Value != GameState.GameOver) return;

        safeUntil.Clear();
        Lives.Value = startingLives;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            client.PlayerObject.GetComponent<PlayerController>().ResetRpc(GetRespawnPosition());
        }

        GhostAI.ResetAll();
        OnGameRestarted?.Invoke();

        State.Value = GameState.Playing;
    }

    private Vector3 GetRespawnPosition()
    {
        return respawnPoints.Length > 0
            ? respawnPoints[Random.Range(0, respawnPoints.Length)].position
            : Vector3.zero;
    }

    // Placeholder UI just for a little testing
    private void OnGUI()
    {
        if (!IsSpawned) return;

        GUI.Label(new Rect(Screen.width - 160, 10, 150, 30), $"Lives: {Lives.Value}");

        if (State.Value != GameState.GameOver) return;

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        var title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 64,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        title.normal.textColor = Color.red;
        GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 100), "GAME OVER", title);

        if (IsServer)
        {
            if (GUI.Button(new Rect(Screen.width / 2f - 100, Screen.height * 0.5f, 200, 50), "Restart"))
                Restart();
        }
        else
        {
            var sub = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, 40),
                "Waiting for the host to restart...", sub);
        }
    }
}