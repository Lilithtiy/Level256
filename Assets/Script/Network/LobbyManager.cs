using Unity.Netcode;
using UnityEngine;

public struct LobbyPlayer : INetworkSerializable, System.IEquatable<LobbyPlayer>
{
    public ulong clientId;
    public bool ready;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref ready);
    }

    public bool Equals(LobbyPlayer other) => clientId == other.clientId && ready == other.ready;
}

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    private NetworkList<LobbyPlayer> players;

    private void Awake()
    {
        Instance = this;
        players = new NetworkList<LobbyPlayer>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            AddPlayer(id);

        NetworkManager.Singleton.OnClientConnectedCallback += AddPlayer;
        NetworkManager.Singleton.OnClientDisconnectCallback += RemovePlayer;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= AddPlayer;
        NetworkManager.Singleton.OnClientDisconnectCallback -= RemovePlayer;
    }

    // Server-side

    private void AddPlayer(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].clientId == clientId) return;

        players.Add(new LobbyPlayer { clientId = clientId, ready = false });
    }

    private void RemovePlayer(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId == clientId)
            {
                players.RemoveAt(i);
                return;
            }
        }
    }

    private void SetReady(ulong clientId, bool ready)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId == clientId)
            {
                var p = players[i];
                p.ready = ready;
                players[i] = p;
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        SetReady(rpcParams.Receive.SenderClientId, ready);
    }

    // The host doesn't need to ready up, but everyone else does
    private bool AllClientsReady()
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].clientId != NetworkManager.ServerClientId && !players[i].ready)
                return false;
        return true;
    }

    //  UI (placeholder)

    private void OnGUI()
    {
        if (!IsSpawned) return;

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        var title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        var text = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 24 };

        float w = 520f;
        GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, Screen.height * 0.15f, w, Screen.height * 0.7f));

        GUILayout.Label("LOBBY", title);

        if (IsServer && !string.IsNullOrEmpty(SessionUI.JoinCode))
            GUILayout.Label($"Join code: {SessionUI.JoinCode}", text);

        GUILayout.Space(10);

        bool localReady = false;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            bool isHost = p.clientId == NetworkManager.ServerClientId;
            bool isMe = p.clientId == NetworkManager.Singleton.LocalClientId;
            if (isMe) localReady = p.ready;

            string label = $"Player {i + 1}" + (isHost ? " (host)" : "") + (isMe ? " (you)" : "");
            string status = isHost ? "" : (p.ready ? "READY" : "not ready");
            GUILayout.Label($"{label}   {status}", text);
        }

        GUILayout.FlexibleSpace();

        if (IsServer)
        {
            bool everyoneReady = AllClientsReady();

            GUI.enabled = everyoneReady;
            if (GUILayout.Button("Start game", button, GUILayout.Height(50)))
                GameFlow.Instance.StartGame();
            GUI.enabled = true;

            if (!everyoneReady)
                GUILayout.Label("Waiting for everyone to get ready...", text);
        }
        else
        {
            if (GUILayout.Button(localReady ? "Not ready" : "Ready", button, GUILayout.Height(50)))
                SetReadyRpc(!localReady);
        }

        GUILayout.EndArea();
    }
}