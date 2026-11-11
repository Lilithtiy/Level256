using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startingLives = 3;
    [SerializeField] private float safeSeconds = 3f;
    [SerializeField] private Transform[] respawnPoints;

    // Server writes, everyone reads
    public NetworkVariable<int> Lives = new(3, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
    public bool TryCatchPlayer(ulong clientId)
    {
        if (!IsServer || Lives.Value <= 0) return false;

        if (safeUntil.TryGetValue(clientId, out float until) && Time.time < until)
            return false;

        safeUntil[clientId] = Time.time + safeSeconds;
        Lives.Value--;

        if (Lives.Value <= 0)
        {
            Debug.Log("GAME OVER"); // placeholder
            return true;
        }

        var player = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        Vector3 pos = respawnPoints.Length > 0 ? respawnPoints[Random.Range(0, respawnPoints.Length)].position : Vector3.zero;

        player.GetComponent<PlayerController>().RespawnRpc(pos);
        return true;
    }

    // Placeholder UI just for a little testing
    private void OnGUI()
    {
        if (!IsSpawned) return;
        GUI.Label(new Rect(Screen.width - 160, 10, 150, 30), $"Lives: {Lives.Value}");
    }
}