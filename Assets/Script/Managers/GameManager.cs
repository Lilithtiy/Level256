using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum GameState { Playing, GameOver, Won }
public enum GhostState { Normal, Exposed }

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startingLives = 3;
    [SerializeField] private float safeSeconds = 5f;
    [SerializeField] private Transform[] respawnPoints;
    [SerializeField] private float spawnHeight = 1.3f;

    // Server-owned round state, synchronized to all clients
    public NetworkVariable<int> Lives = new(3, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<GameState> State = new(GameState.Playing, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<GhostState> ghostState = new(GhostState.Normal, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private float ghostExposedDuration = 10f;
    public NetworkVariable<float> ghostExposedTimer = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event System.Action OnRoundStarted;
    public event System.Action<ulong> OnPlayerCaught;

    // Tracks when each caught player can next be caught
    private readonly Dictionary<ulong, float> safeUntil = new();

    private void Awake()
    {
        Instance = this;
    }

    // Reset round state and start the rounds ghosts and tasks
    public void BeginRound()
    {
        if (!IsServer) return;

        safeUntil.Clear();
        Lives.Value = startingLives;
        State.Value = GameState.Playing;
        ghostState.Value = GhostState.Normal;

        if (GhostSpawner.Instance != null)
            GhostSpawner.Instance.StartRound();
        else
            Debug.LogWarning("GameManager: there is no GhostSpawner in the scene, so NO ghosts will spawn.");

        OnRoundStarted?.Invoke();

        if (TaskManager.Instance != null)
             TaskManager.Instance.SetupRound();
    }

    // Handle a catch on the server, update lives, and tell the player to respawn
    public bool TryCatchPlayer(ulong clientId, Vector3 ghostPosition)
    {
        if (!IsServer || State.Value != GameState.Playing || Lives.Value <= 0) return false;

        if (safeUntil.TryGetValue(clientId, out float until) && Time.time < until)
            return false;

        safeUntil[clientId] = Time.time + safeSeconds;
        Lives.Value--;
        OnPlayerCaught?.Invoke(clientId);
        Debug.Log($"Caught client {clientId}. Lives left: {Lives.Value}");

        bool gameOver = Lives.Value <= 0;
        if (gameOver)
        {
            State.Value = GameState.GameOver;
            Debug.Log("GAME OVER");
        }

        var player = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        player.GetComponent<PlayerController>().CaughtRpc(ghostPosition, GetRandomRespawn(), gameOver);
        return true;
    }

    // Select a spawn position using the requested index
    public Vector3 GetSpawnPosition(int index)
    {
        return respawnPoints.Length > 0 ? respawnPoints[index % respawnPoints.Length].position + Vector3.up * spawnHeight : Vector3.zero;
    }

    private Vector3 GetRandomRespawn()
    {
        return respawnPoints.Length > 0 ? respawnPoints[Random.Range(0, respawnPoints.Length)].position + Vector3.up * spawnHeight : Vector3.zero;
    }

    // The server ends the round when the exit objective is completed
    public void Win()
    {
        if (!IsServer || State.Value != GameState.Playing) return;

        State.Value = GameState.Won;
        Debug.Log("YOU ESCAPED!");
    }

    // UI (Placeholder)
    private void OnGUI()
    {
        if (SettingsMenu.IsOpen) return;
        if (!IsSpawned) return;

        if (State.Value == GameState.GameOver) DrawEndScreen("GAME OVER", Color.red);
        else if (State.Value == GameState.Won) DrawEndScreen("YOU ESCAPED!", Color.green);
    }

    private void DrawEndScreen(string headline, Color color)
    {
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
        title.normal.textColor = color;
        GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 100), headline, title);

        if (IsServer)
        {
            if (GUI.Button(new Rect(Screen.width / 2f - 100, Screen.height * 0.5f, 200, 50), "Back to lobby"))
                GameFlow.Instance.ReturnToLobby();
        }
        else
        {
            var sub = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, 40), "Waiting for the host...", sub);
        }
        AudioManager.audioManagerInstance?.PlayMusic(AudioManager.MusicState.Lobby);
    }

    // Put ghosts into the exposed state and start its countdown
    public void ActivateGhostExposedState()
    {
        if (!IsServer) return;
        ghostState.Value = GhostState.Exposed;
        Debug.Log("Ghosts are now exposed!");
        ghostExposedTimer.Value = ghostExposedDuration;
    }

    // Count down the exposed period on the server, then restore normal ghost behavior
    private void FixedUpdate()
    {
        if (ghostExposedTimer.Value > 0 && ghostState.Value == GhostState.Exposed)
        {
            ghostExposedTimer.Value -= Time.fixedDeltaTime;
            if (ghostExposedTimer.Value <= 0)
            {
                ghostState.Value = GhostState.Normal;
                ghostExposedTimer.Value = 0;
                Debug.Log("Ghosts are no longer exposed!");
            }
        }
    }
}