using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum GameState { Playing, GameOver }
public enum GhostState { Normal, Exposed }

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startingLives = 3;
    [SerializeField] private float safeSeconds = 5f;
    [SerializeField] private Transform[] respawnPoints;

    public NetworkVariable<int> Lives = new(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<GameState> State = new(
        GameState.Playing,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<GhostState> ghostState = new(
        GhostState.Normal,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private float ghostExposedDuration = 10f;
    private float ghostExposedTimer = 0f;

    // SERVER ONLY. its fires when the round starts (and the ghost spawner listens to this).
    public event System.Action OnRoundStarted;

    private readonly Dictionary<ulong, float> safeUntil = new();

    private void Awake()
    {
        Instance = this;
    }

    // its called by GameFlow once the players have been spawned
    public void BeginRound()
    {
        if (!IsServer) return;

        safeUntil.Clear();
        Lives.Value = startingLives;
        State.Value = GameState.Playing;
        ghostState.Value = GhostState.Normal;

        if (GhostSpawner.Instance != null)
            GhostSpawner.Instance.SpawnGhosts();
        else
            Debug.LogWarning("GameManager: there is no GhostSpawner in the scene, so NO ghosts will spawn.");

        OnRoundStarted?.Invoke();   // here for other systems
    }

    // its called by ghosts. Server only. and returns true if the catch counted.
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
        player.GetComponent<PlayerController>().CaughtRpc(ghostPosition, GetRandomRespawn(), gameOver);
        return true;
    }

    public Vector3 GetSpawnPosition(int index)
    {
        return respawnPoints.Length > 0
            ? respawnPoints[index % respawnPoints.Length].position
            : Vector3.zero;
    }

    private Vector3 GetRandomRespawn()
    {
        return respawnPoints.Length > 0
            ? respawnPoints[Random.Range(0, respawnPoints.Length)].position
            : Vector3.zero;
    }

    // UI (Placeholder)

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
            if (GUI.Button(new Rect(Screen.width / 2f - 100, Screen.height * 0.5f, 200, 50), "Back to lobby"))
                GameFlow.Instance.ReturnToLobby();
        }
        else
        {
            var sub = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, 40),
                "Waiting for the host...", sub);
        }
    }

    public void ActivateGhostExposedState()
    {
        if (!IsServer) return;
        ghostState.Value = GhostState.Exposed;
        Debug.Log("Ghosts are now exposed!");
        ghostExposedTimer = ghostExposedDuration;
    }

    private void FixedUpdate()
    {
        if (ghostExposedTimer > 0 && ghostState.Value == GhostState.Exposed)
        {
            ghostExposedTimer -= Time.fixedDeltaTime;
            if (ghostExposedTimer <= 0)
            {
                ghostState.Value = GhostState.Normal;
                ghostExposedTimer = 0;
                Debug.Log("Ghosts are no longer exposed!");
            }
        }
    }
}