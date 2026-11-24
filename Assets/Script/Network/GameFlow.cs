using System.Collections.Generic;
using Unity.Netcode;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string gameSceneName = "Level256";
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private GameObject playerPrefab;

    private bool hooked;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        var nm = NetworkManager.Singleton;
        nm.OnServerStarted += Hook;
        nm.OnClientStarted += Hook;
        nm.OnServerStopped += _ => hooked = false;
        nm.OnClientStopped += _ => hooked = false;
    }

    private void Hook()
    {
        var nm = NetworkManager.Singleton;
        if (hooked || !nm.IsServer || nm.SceneManager == null) return;
        hooked = true;

        nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        nm.SceneManager.OnSynchronizeComplete += OnSynchronizeComplete;
    }

    // Called by the host
    public void OpenLobby() => LoadScene(lobbySceneName);
    public void StartTutorial()
    {
        var nm = NetworkManager.Singleton;

        nm.StartHost();

        LoadScene(tutorialSceneName);

        AudioManager.audioManagerInstance?.PlayMusic(AudioManager.MusicState.Ambient);

        Debug.Log("Tried loading tutorial scene");
    }

    public void StartGame() => LoadScene(gameSceneName);
    public void ReturnToLobby() => LoadScene(lobbySceneName);

    private void LoadScene(string sceneName)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        DespawnGameObjects();

        var status = nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
            Debug.LogWarning($"Could not load {sceneName}: {status}");
    }

    private void DespawnGameObjects()
    {
        foreach (var ghost in FindObjectsByType<GhostAI>(FindObjectsSortMode.None))
            if (ghost.NetworkObject.IsSpawned) ghost.NetworkObject.Despawn(true);

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player != null && player.IsSpawned) player.Despawn(true);
        }
    }

    // Spawning players when the game scene is ready
    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != gameSceneName && sceneName != tutorialSceneName) return;

        int index = 0;
        foreach (ulong clientId in clientsCompleted)
            SpawnPlayer(clientId, index++);

        PlayerRole.AssignAll();
        GameManager.Instance.BeginRound();
    }

    // A player who joins in the middle of a round
    private void OnSynchronizeComplete(ulong clientId)
    {
        if (SceneManager.GetActiveScene().name != gameSceneName && SceneManager.GetActiveScene().name != tutorialSceneName) return;
        if (GameManager.Instance == null) return;

        var player = SpawnPlayer(clientId, Random.Range(0, 4));
        PlayerRole.AssignUnused(player.GetComponent<PlayerRole>());
    }

    private GameObject SpawnPlayer(ulong clientId, int index)
    {
        Vector3 pos = GameManager.Instance.GetSpawnPosition(index);
        var player = Instantiate(playerPrefab, pos, Quaternion.identity);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        return player;
    }
}