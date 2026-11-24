using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public struct LobbyPlayer : INetworkSerializable, System.IEquatable<LobbyPlayer>
{
    public ulong clientId;
    public bool ready;
    public FixedString32Bytes name;
    public byte color;
    public ushort ping;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref ready);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref color);
        serializer.SerializeValue(ref ping);
    }

    public bool Equals(LobbyPlayer other) =>
        clientId == other.clientId && ready == other.ready && name.Equals(other.name)
        && color == other.color && ping == other.ping;
}

[RequireComponent(typeof(UIDocument))]
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [SerializeField] private ScriptableAudio menuSelectAudio;
    [SerializeField] private ScriptableAudio typingAudio;
    [SerializeField] private ScriptableAudio sendMessageAudio;

    public static readonly Color[] PlayerColors =
    {
        new Color(0.20f, 0.90f, 0.20f), new Color(0.30f, 0.30f, 1.00f),
        new Color(1.00f, 0.25f, 0.25f), new Color(1.00f, 0.85f, 0.10f),
        new Color(0.80f, 0.30f, 0.90f), new Color(0.20f, 0.85f, 0.90f),
        new Color(1.00f, 0.55f, 0.10f), new Color(1.00f, 0.41f, 0.71f)
    };
    public static readonly Dictionary<ulong, (string name, byte color)> Profiles = new();

    private static readonly Color PingGood = new Color(0.5f, 1f, 0.4f);
    private static readonly Color PingOk = new Color(1f, 0.85f, 0.2f);
    private static readonly Color PingBad = new Color(1f, 0.35f, 0.3f);
    private static readonly Color Dim = new Color(1f, 1f, 1f, 0.4f);

    private NetworkList<LobbyPlayer> players;

    // chat/name input
    private enum InputMode { None, Chat, Name }
    private InputMode mode = InputMode.None;
    private string nameInput = "";
    private bool inputFocused;
    private bool localReady;
    private float copiedUntil;
    private float nextPing;

    // UI
    private class Row
    {
        public VisualElement root, avatar;
        public Label ping, status, name, host;
    }

    private readonly List<Row> rows = new();
    private VisualElement root, playerList, inputRow;
    private Label codeLabel, waitingLabel, inputLabel;
    private ScrollView chatScroll;
    private TextField inputField;
    private Button colorBtn, nameBtn, muteBtn, leaveBtn, copyBtn, startBtn;

    private ProximityVoice voice;
    private ProximityVoice Voice
    {
        get
        {
            if (voice == null) voice = FindFirstObjectByType<ProximityVoice>();
            return voice;
        }
    }

    private void Awake()
    {
        Instance = this;
        players = new NetworkList<LobbyPlayer>();
    }

    private void OnEnable() => TryBind();

    public override void OnNetworkSpawn()
    {
        TryBind();
        if (root != null) root.style.display = DisplayStyle.Flex;
        if (chatScroll != null) chatScroll.contentContainer.Clear();
        CloseInput(false);

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        nameInput = PlayerName.Load();
        ApplyName();

        if (!IsServer) return;

        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            AddPlayer(id);

        NetworkManager.Singleton.OnClientConnectedCallback += AddPlayer;
        NetworkManager.Singleton.OnClientDisconnectCallback += RemovePlayer;
    }

    public override void OnNetworkDespawn()
    {
        if (root != null) root.style.display = DisplayStyle.None;

        if (!IsServer || NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= AddPlayer;
        NetworkManager.Singleton.OnClientDisconnectCallback -= RemovePlayer;
    }

    // Server-side
    private void ServerPing()
    {
        // Host measures everyone's ping and syncs it through the player list
        if (Time.time < nextPing) return;
        nextPing = Time.time + 2f;

        var utp = NetworkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if (utp == null) return;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p.clientId == NetworkManager.ServerClientId) continue;

            ushort rtt = (ushort)Mathf.Min((int)utp.GetCurrentRtt(p.clientId), 999);
            if (Mathf.Abs(rtt - p.ping) < 10) continue; // avoid spamming updates

            p.ping = rtt;
            players[i] = p;
        }
    }

    private int FindIndex(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].clientId == clientId) return i;
        return -1;
    }

    private bool ColorUsed(byte c)
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].color == c) return true;
        return false;
    }

    private byte FirstFreeColor()
    {
        for (byte c = 0; c < PlayerColors.Length; c++)
            if (!ColorUsed(c)) return c;
        return 0;
    }

    private void AddPlayer(ulong clientId)
    {
        if (FindIndex(clientId) >= 0) return;
        players.Add(new LobbyPlayer { clientId = clientId, ready = false, color = FirstFreeColor() });
    }

    private void RemovePlayer(ulong clientId)
    {
        int i = FindIndex(clientId);
        if (i >= 0) players.RemoveAt(i);
    }

    [Rpc(SendTo.Server)]
    private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        int i = FindIndex(rpcParams.Receive.SenderClientId);
        if (i < 0) return;
        var p = players[i];
        p.ready = ready;
        players[i] = p;
    }

    [Rpc(SendTo.Server)]
    private void SetNameRpc(FixedString32Bytes rawName, RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        FixedString32Bytes clean = default;
        clean.CopyFromTruncated(PlayerName.Sanitize(rawName.ToString()));

        int i = FindIndex(id);
        if (i >= 0)
        {
            var p = players[i];
            p.name = clean;
            players[i] = p;
            return;
        }

        players.Add(new LobbyPlayer { clientId = id, ready = false, name = clean, color = FirstFreeColor() });
    }

    [Rpc(SendTo.Server)]
    private void ChangeColorRpc(RpcParams rpcParams = default)
    {
        int i = FindIndex(rpcParams.Receive.SenderClientId);
        if (i < 0) return;

        var p = players[i];
        int n = PlayerColors.Length;
        for (int step = 1; step < n; step++)
        {
            byte c = (byte)((p.color + step) % n);
            if (ColorUsed(c)) continue;
            p.color = c;
            players[i] = p;
            return;
        }
    }

    [Rpc(SendTo.Server)]
    private void SendChatRpc(FixedString128Bytes message, RpcParams rpcParams = default)
    {
        ReceiveChatRpc(rpcParams.Receive.SenderClientId, message);
    }

    [Rpc(SendTo.Everyone)]
    private void ReceiveChatRpc(ulong senderId, FixedString128Bytes message)
    {
        int i = FindIndex(senderId);
        string who = i >= 0 ? PlainName(i) : "???";
        Color c = i >= 0 ? PlayerColors[players[i].color % PlayerColors.Length] : Color.white;

        AddChatLine($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{Clean(who)}</color>: {Clean(message.ToString())}");
    }

    // The host doesn't need to ready up, but everyone else does
    private bool AllClientsReady()
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].clientId != NetworkManager.ServerClientId && !players[i].ready)
                return false;
        return true;
    }

    private void SnapshotProfiles()
    {
        Profiles.Clear();
        for (int i = 0; i < players.Count; i++)
            Profiles[players[i].clientId] = (PlainName(i), players[i].color);
    }

    // Helpers
    private static string Clean(string s) => s.Replace("<", "‹");

    private string PlainName(int index)
    {
        var p = players[index];
        return p.name.IsEmpty ? $"Player {index + 1}" : p.name.ToString();
    }

    private void ApplyName()
    {
        string clean = PlayerName.Sanitize(nameInput);
        PlayerName.Save(clean);

        FixedString32Bytes n = default;
        n.CopyFromTruncated(clean);
        SetNameRpc(n);
    }

    // UI
    private bool TryBind()
    {
        if (root != null && root.panel != null) return true;

        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return false;


        var r = doc.rootVisualElement.Q("lobby-root");
        if (r == null) return false;
        root = r;
        rows.Clear();

        playerList = root.Q("player-list");
        codeLabel = root.Q<Label>("code");
        waitingLabel = root.Q<Label>("waiting");
        chatScroll = root.Q<ScrollView>("chat");
        inputRow = root.Q("input-row");
        inputLabel = root.Q<Label>("input-label");
        inputField = root.Q<TextField>("input-field");

        colorBtn = root.Q<Button>("color-btn");
        nameBtn = root.Q<Button>("name-btn");
        muteBtn = root.Q<Button>("mute-btn");
        leaveBtn = root.Q<Button>("leave-btn");
        copyBtn = root.Q<Button>("copy-btn");
        startBtn = root.Q<Button>("start-btn");

        colorBtn.clicked += () => ChangeColorRpc(); 
        nameBtn.clicked += () => OpenInput(InputMode.Name);
        muteBtn.clicked += () => 
        {
            var v = Voice;
            if (v != null) v.SetMuted(!v.IsMuted);
            AudioManager.audioManagerInstance.PlayLocal(menuSelectAudio);
        };
        leaveBtn.clicked += () =>
        {
            var ui = FindFirstObjectByType<SessionUI>();
            if (ui != null) ui.LeaveSession();
            AudioManager.audioManagerInstance.PlayLocal(menuSelectAudio);
            AudioManager.audioManagerInstance.PlayMusic(AudioManager.MusicState.MainMenu);
        };
        copyBtn.clicked += () =>
        {
            GUIUtility.systemCopyBuffer = SessionUI.JoinCode;
            copiedUntil = Time.time + 2f;
            AudioManager.audioManagerInstance.PlayLocal(menuSelectAudio);
        };
        startBtn.clicked += OnStartClicked;

        // Clicking away from the text box cancels it
        inputField.RegisterCallback<FocusInEvent>(_ => inputFocused = true);
        inputField.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (inputFocused && mode != InputMode.None) CloseInput(false);
            inputFocused = false;
        });

        // Play typing sound when text is added
        inputField.RegisterValueChangedCallback(changeEvent =>
        {
            if (!inputFocused) return;

            if (changeEvent.newValue.Length > changeEvent.previousValue.Length)
            {
                AudioManager.audioManagerInstance.PlayLocal(typingAudio);
            }
        });

        var settingsBtn = root.Q<Button>("settings-btn");
        if (settingsBtn != null) settingsBtn.clicked += () => SettingsMenu.Instance?.Open();

        root.style.display = IsSpawned ? DisplayStyle.Flex : DisplayStyle.None;
        return true;
    }

    private void OnStartClicked()
    {
        AudioManager.audioManagerInstance.PlayLocal(menuSelectAudio);

        if (IsServer)
        {
            if (AllClientsReady())
            {
                SnapshotProfiles();
                GameFlow.Instance.StartGame();
            }
        }
        else
        {
            SetReadyRpc(!localReady);
        }
    }

    private void OpenInput(InputMode m)
    {
        mode = m;
        inputLabel.text = m == InputMode.Chat ? "SAY:" : "NAME:";
        inputField.maxLength = m == InputMode.Name ? PlayerName.MaxLength : 100;
        inputRow.style.display = DisplayStyle.Flex;

        // Wait a moment before focusing, so the "t" that opened the chat doesn't get typed into it
        inputField.schedule.Execute(() =>
        {
            if (mode != m) return; // closed in the meantime
            inputField.value = m == InputMode.Name ? nameInput : "";
            inputField.Focus();
        }).StartingIn(40);
    }

    private void CloseInput(bool blur = true)
    {
        mode = InputMode.None;
        inputFocused = false;
        if (inputRow != null) inputRow.style.display = DisplayStyle.None;
        if (blur && inputField != null) inputField.Blur();
    }

    private void SubmitInput()
    {
        string text = inputField.value;

        if (mode == InputMode.Chat)
        {
            string msg = text.Trim();
            if (msg.Length > 100) msg = msg.Substring(0, 100);
            if (msg.Length > 0) SendChatRpc(msg);
        }
        else if (mode == InputMode.Name)
        {
            nameInput = text;
            ApplyName();
        }

        CloseInput();
    }

    private void AddChatLine(string richText)
    {
        if (chatScroll == null) return;

        var line = new Label(richText) { enableRichText = true };
        line.AddToClassList("chat-line");
        chatScroll.contentContainer.Add(line);

        while (chatScroll.contentContainer.childCount > 50)
            chatScroll.contentContainer.RemoveAt(0);

        // scroll down once the layout has caught up
        chatScroll.schedule.Execute(() =>
            chatScroll.verticalScroller.value = chatScroll.verticalScroller.highValue).StartingIn(50);
    }

    private Row MakeRow()
    {
        var row = new Row();

        row.root = new VisualElement();
        row.root.AddToClassList("player-row");

        row.ping = new Label();
        row.ping.AddToClassList("player-ping");
        row.status = new Label();
        row.status.AddToClassList("player-status");
        row.name = new Label();
        row.name.AddToClassList("player-name");
        row.host = new Label("[HOST]");
        row.host.AddToClassList("player-host");

        row.avatar = new VisualElement();
        row.avatar.AddToClassList("avatar");
        for (int i = 0; i < 2; i++)
        {
            var eye = new VisualElement();
            eye.AddToClassList("avatar__eye");
            var pupil = new VisualElement();
            pupil.AddToClassList("avatar__pupil");
            eye.Add(pupil);
            row.avatar.Add(eye);
        }

        row.root.Add(row.ping);
        row.root.Add(row.status);
        row.root.Add(row.name);
        row.root.Add(row.host);
        row.root.Add(row.avatar);

        playerList.Add(row.root);
        return row;
    }

    private void RefreshPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        while (rows.Count < players.Count) rows.Add(MakeRow());
        while (rows.Count > players.Count)
        {
            rows[rows.Count - 1].root.RemoveFromHierarchy();
            rows.RemoveAt(rows.Count - 1);
        }

        localReady = false;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            var r = rows[i];
            bool isHost = p.clientId == NetworkManager.ServerClientId;
            bool isMe = p.clientId == nm.LocalClientId;
            if (isMe) localReady = p.ready;

            r.root.EnableInClassList("player-row--me", isMe);
            r.name.text = PlainName(i);
            r.host.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            r.avatar.style.backgroundColor = PlayerColors[p.color % PlayerColors.Length];

            if (isHost)
            {
                r.ping.text = "—";
                r.ping.style.color = Dim;
                r.status.text = "";
            }
            else
            {
                r.ping.text = $"{p.ping} ms";
                r.ping.style.color = p.ping < 80 ? PingGood : p.ping < 150 ? PingOk : PingBad;
                r.status.text = p.ready ? "READY" : "NOT READY";
                r.status.style.color = p.ready ? PingGood : Dim;
            }
        }
    }

    private void HandleKeys()
    {
        if (SettingsMenu.IsOpen) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (mode == InputMode.None)
        {
            if (kb.tKey.wasPressedThisFrame) OpenInput(InputMode.Chat);
        }
        else if (kb.escapeKey.wasPressedThisFrame)
        {
            CloseInput();
        }
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            SubmitInput();
            AudioManager.audioManagerInstance.PlayLocal(sendMessageAudio);
        }
    }

    private void Update()
    {
        if (IsSpawned)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        if (!IsSpawned || !TryBind()) return;

        HandleKeys();

        // Header
        bool hasCode = !string.IsNullOrEmpty(SessionUI.JoinCode);
        codeLabel.text = hasCode ? $"CODE: {SessionUI.JoinCode}" : "";
        waitingLabel.text = IsServer && !AllClientsReady() ? "WAITING FOR PLAYERS TO READY UP" : "";

        RefreshPlayers();

        // Buttons
        var v = Voice;
        muteBtn.style.display = v != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (v != null) muteBtn.text = v.IsMuted ? "UNMUTE MIC" : "MUTE MIC";

        copyBtn.style.visibility = hasCode ? Visibility.Visible : Visibility.Hidden;
        copyBtn.text = Time.time < copiedUntil ? "COPIED!" : "COPY CODE";

        if (IsServer)
        {
            startBtn.text = "START GAME";
            startBtn.SetEnabled(AllClientsReady());
        }
        else
        {
            startBtn.text = localReady ? "NOT READY" : "READY";
            startBtn.SetEnabled(true);
        }

        if (IsServer) ServerPing();
    }
}