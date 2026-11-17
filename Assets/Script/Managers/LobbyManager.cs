using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

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

    public bool Equals(LobbyPlayer other) => clientId == other.clientId && ready == other.ready && name.Equals(other.name) && color == other.color && ping == other.ping;
}

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }
    [SerializeField] private Font font;

    private static readonly Color Yellow = new Color(1f, 0.92f, 0.016f, 1f);
    private static readonly Color[] PlayerColors =
    {
        new Color(0.20f, 0.90f, 0.20f), new Color(0.30f, 0.30f, 1.00f),
        new Color(1.00f, 0.25f, 0.25f), new Color(1.00f, 0.85f, 0.10f),
        new Color(0.80f, 0.30f, 0.90f), new Color(0.20f, 0.85f, 0.90f),
        new Color(1.00f, 0.55f, 0.10f), new Color(1.0f, 0.41f, 0.71f)
    };

    private NetworkList<LobbyPlayer> players;

    // chat/name input
    private enum InputMode { None, Chat, Name }
    private struct ChatLine { public string text; public float time; }

    private readonly List<ChatLine> chatLog = new();
    private InputMode mode = InputMode.None;
    private string inputText = "";
    private string nameInput = "";
    private bool focusPending;
    private int openFrame = -1;
    private float copiedUntil;
    private float nextPing;

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

    public override void OnNetworkSpawn()
    {
        mode = InputMode.None;
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
        if (!IsServer || NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= AddPlayer;
        NetworkManager.Singleton.OnClientDisconnectCallback -= RemovePlayer;
    }

    // Server-side
    private void Update()
    {
        // Host measures everyones ping and syncs it through the player list
        if (!IsSpawned || !IsServer || Time.time < nextPing) return;
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

        chatLog.Add(new ChatLine
        {
            text = $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{Clean(who)}</color>: {Clean(message.ToString())}",
            time = Time.time
        });
        if (chatLog.Count > 50) chatLog.RemoveAt(0);
    }

    // The host doesn't need to ready up, but everyone else does
    private bool AllClientsReady()
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].clientId != NetworkManager.ServerClientId && !players[i].ready)
                return false;
        return true;
    }

    // Helpers
    // Stops players from injecting rich-text tags into names/chat
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

    private void OpenInput(InputMode m)
    {
        mode = m;
        inputText = m == InputMode.Name ? nameInput : "";
        focusPending = true;
        openFrame = Time.frameCount;
    }

    private void CloseInput()
    {
        mode = InputMode.None;
        GUI.FocusControl(null);
    }

    private void SubmitInput()
    {
        if (mode == InputMode.Chat)
        {
            string msg = inputText.Trim();
            if (msg.Length > 100) msg = msg.Substring(0, 100);
            if (msg.Length > 0) SendChatRpc(msg);
        }
        else if (mode == InputMode.Name)
        {
            nameInput = inputText;
            ApplyName();
        }
        CloseInput();
    }

    // UI
    private GUIStyle title, subtitle, subtitleRight, rowName, rowPing, rowStatus;
    private GUIStyle btnBig, btnBigRight, btnBigCenter, btnSmall, btnSmallRight, chatStyle, field;

    private GUIStyle Make(TextAnchor align, Color color, FontStyle fs = FontStyle.Normal, bool hover = false)
    {
        var st = new GUIStyle { alignment = align, fontStyle = fs, richText = true };
        if (font != null) st.font = font;
        st.normal.textColor = color;
        if (hover)
        {
            st.hover.textColor = Yellow;
            st.active.textColor = Yellow;
        }
        return st;
    }

    private void BuildStyles()
    {
        if (title != null) return;

        var gray = new Color(1f, 1f, 1f, 0.55f);
        var soft = new Color(1f, 1f, 1f, 0.85f);

        title = Make(TextAnchor.MiddleLeft, Yellow, FontStyle.BoldAndItalic);
        subtitle = Make(TextAnchor.MiddleLeft, gray);
        subtitleRight = Make(TextAnchor.MiddleRight, gray);
        rowName = Make(TextAnchor.MiddleRight, soft);
        rowPing = Make(TextAnchor.MiddleLeft, Color.white);
        rowStatus = Make(TextAnchor.MiddleLeft, gray);
        btnBig = Make(TextAnchor.MiddleLeft, soft, FontStyle.Normal, true);
        btnBigRight = Make(TextAnchor.MiddleRight, soft, FontStyle.Normal, true);
        btnSmall = Make(TextAnchor.MiddleLeft, Yellow, FontStyle.Normal, true);
        btnSmall.hover.textColor = Color.white;
        btnSmall.active.textColor = Color.white;
        btnBigCenter = Make(TextAnchor.MiddleCenter, soft, FontStyle.Normal, true);
        btnSmallRight = Make(TextAnchor.MiddleRight, Yellow, FontStyle.Normal, true);
        btnSmallRight.hover.textColor = Color.white;
        btnSmallRight.active.textColor = Color.white;
        chatStyle = Make(TextAnchor.UpperLeft, Color.white);
        chatStyle.wordWrap = true;

        field = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft };
        if (font != null) field.font = font;
    }

    private void ScaleStyles(float s)
    {
        title.fontSize = Mathf.RoundToInt(120 * s);
        subtitle.fontSize = subtitleRight.fontSize = Mathf.RoundToInt(24 * s);
        rowName.fontSize = Mathf.RoundToInt(34 * s);
        rowPing.fontSize = Mathf.RoundToInt(26 * s);
        rowStatus.fontSize = Mathf.RoundToInt(20 * s);
        btnBig.fontSize = btnBigRight.fontSize = btnBigCenter.fontSize = Mathf.RoundToInt(46 * s);
        btnSmall.fontSize = btnSmallRight.fontSize = Mathf.RoundToInt(26 * s);
        chatStyle.fontSize = Mathf.RoundToInt(26 * s);
        field.fontSize = Mathf.RoundToInt(26 * s);
    }

    private static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static void DrawAvatar(Rect r, Color c)
    {
        Color old = GUI.color;

        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        float ew = r.width * 0.28f, eh = r.height * 0.42f, ey = r.y + r.height * 0.22f;
        var left = new Rect(r.x + r.width * 0.14f, ey, ew, eh);
        var right = new Rect(r.xMax - r.width * 0.14f - ew, ey, ew, eh);

        GUI.color = Color.white;
        GUI.DrawTexture(left, Texture2D.whiteTexture);
        GUI.DrawTexture(right, Texture2D.whiteTexture);

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(left.x + ew * 0.3f, left.y + eh * 0.3f, ew * 0.45f, eh * 0.45f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(right.x + ew * 0.3f, right.y + eh * 0.3f, ew * 0.45f, eh * 0.45f), Texture2D.whiteTexture);

        GUI.color = old;
    }

    private void HandleKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        if (mode == InputMode.None)
        {
            if (e.keyCode == KeyCode.T) { OpenInput(InputMode.Chat); e.Use(); }
        }
        else if (e.keyCode == KeyCode.Escape) { CloseInput(); e.Use(); }
        else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { SubmitInput(); e.Use(); }
        else if (e.keyCode == KeyCode.None && Time.frameCount == openFrame) e.Use();
    }

    private void OnGUI()
    {
        if (!IsSpawned) return;

        BuildStyles();
        float s = Screen.height / 1080f;
        ScaleStyles(s);
        HandleKeys();

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f));
        float margin = 60 * s;
        var panel = new Rect(margin, margin, Screen.width - margin * 2, Screen.height - margin * 2);
        Fill(panel, new Color(0f, 0f, 0f, 0.6f));

        float pad = 40 * s;
        float px = panel.x + pad;
        float iw = panel.width - pad * 2;
        float gap = 60 * s;
        float colW = (iw - gap) / 2f;
        float rightColX = px + colW + gap;

        // Header
        GUI.Label(new Rect(px, panel.y + 10 * s, colW, 120 * s), "LOBBY", title);
        if (!string.IsNullOrEmpty(SessionUI.JoinCode))
            GUI.Label(new Rect(px, panel.y + 40 * s, iw, 36 * s), $"CODE: {SessionUI.JoinCode}", subtitleRight);
        GUI.Label(new Rect(px, panel.y + 80 * s, iw, 30 * s), "PRESS T TO CHAT", subtitleRight);
        if (IsServer && !AllClientsReady())
            GUI.Label(new Rect(px, panel.y + 112 * s, iw, 30 * s), "WAITING FOR PLAYERS TO READY UP", subtitleRight);

        // Layout anchors
        float ry = panel.y + 190 * s;
        float rowH = 64 * s;
        float row2Y = panel.yMax - 20 * s - 56 * s;
        float stackY = row2Y - 3 * 36 * s - 6 * s;
        float inputY = stackY - 56 * s;

        // Player rows
        bool localReady = false;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            bool isHost = p.clientId == NetworkManager.ServerClientId;
            bool isMe = p.clientId == NetworkManager.Singleton.LocalClientId;
            if (isMe) localReady = p.ready;

            float y = ry + i * rowH;
            var row = new Rect(px - 10 * s, y, colW + 20 * s, rowH - 8 * s);
            if (isMe) Fill(row, new Color(1f, 1f, 1f, 0.06f));

            if (isHost)
            {
                rowPing.normal.textColor = new Color(1f, 1f, 1f, 0.4f);
                GUI.Label(new Rect(px, y, 120 * s, row.height), "—", rowPing);
            }
            else
            {
                rowPing.normal.textColor = p.ping < 80 ? new Color(0.5f, 1f, 0.4f) : p.ping < 150 ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.35f, 0.3f);
                GUI.Label(new Rect(px, y, 120 * s, row.height), $"{p.ping} ms", rowPing);

                rowStatus.normal.textColor = p.ready ? new Color(0.5f, 1f, 0.4f) : new Color(1f, 1f, 1f, 0.4f);
                GUI.Label(new Rect(px + 125 * s, y, 140 * s, row.height), p.ready ? "READY" : "NOT READY", rowStatus);
            }

            string nm = Clean(PlainName(i)) + (isHost ? "  <color=#FF9900>[HOST]</color>" : "");
            GUI.Label(new Rect(px + 250 * s, y, colW - 250 * s - 72 * s, row.height), nm, rowName);
            DrawAvatar(new Rect(px + colW - 60 * s, y + 4 * s, 56 * s, row.height - 8 * s), PlayerColors[p.color % PlayerColors.Length]);
        }

        // Chat box
        var chatBox = new Rect(rightColX, ry, colW, inputY - 10 * s - ry);
        if (chatBox.height > 20 * s)
        {
            Fill(chatBox, new Color(1f, 1f, 1f, 0.04f));
            var inner = new Rect(chatBox.x + 12 * s, chatBox.y + 8 * s, chatBox.width - 24 * s, chatBox.height - 16 * s);

            float yy = inner.yMax;
            for (int i = chatLog.Count - 1; i >= 0; i--)
            {
                var content = new GUIContent(chatLog[i].text);
                float h = chatStyle.CalcHeight(content, inner.width);
                yy -= h;
                if (yy < inner.y) break;
                GUI.Label(new Rect(inner.x, yy, inner.width, h), content, chatStyle);
            }
        }

        // Input row
        if (mode != InputMode.None)
        {
            GUI.Label(new Rect(rightColX, inputY, 100 * s, 44 * s), mode == InputMode.Chat ? "SAY:" : "NAME:", btnSmall);

            GUI.SetNextControlName("Input");
            int max = mode == InputMode.Name ? PlayerName.MaxLength : 100;
            inputText = GUI.TextField(new Rect(rightColX + 100 * s, inputY, colW - 100 * s, 44 * s), inputText ?? "", max, field);

            if (focusPending && Time.frameCount > openFrame)
            {
                GUI.FocusControl("Input");
                focusPending = false;
            }
        }

        // Small buttons
        float stackX = px + iw - 300 * s;

        if (GUI.Button(new Rect(stackX, stackY, 300 * s, 34 * s), "CHANGE COLOR", btnSmallRight))
            ChangeColorRpc();

        if (GUI.Button(new Rect(stackX, stackY + 36 * s, 300 * s, 34 * s), "CHANGE NAME", btnSmallRight))
            OpenInput(InputMode.Name);

        var v = Voice;
        if (v != null && GUI.Button(new Rect(stackX, stackY + 72 * s, 300 * s, 34 * s), v.IsMuted ? "UNMUTE MIC" : "MUTE MIC", btnSmallRight))
            v.SetMuted(!v.IsMuted);

        // Bottom row
        if (GUI.Button(new Rect(px, row2Y, 240 * s, 56 * s), "LEAVE", btnBig))
        {
            var ui = FindFirstObjectByType<SessionUI>();
            if (ui != null) ui.LeaveSession();
        }

        if (!string.IsNullOrEmpty(SessionUI.JoinCode))
        {
            string inviteLabel = Time.time < copiedUntil ? "COPIED!" : "CODY CODE";
            if (GUI.Button(new Rect(px + iw / 2f - 150 * s, row2Y, 300 * s, 56 * s), inviteLabel, btnBigCenter))
            {
                GUIUtility.systemCopyBuffer = SessionUI.JoinCode;
                copiedUntil = Time.time + 2f;
            }
        }

        var bigRight = new Rect(px + iw - 400 * s, row2Y, 400 * s, 56 * s);
        if (IsServer)
        {
            GUI.enabled = AllClientsReady();
            if (GUI.Button(bigRight, "START GAME", btnBigRight))
                GameFlow.Instance.StartGame();
            GUI.enabled = true;
        }
        else
        {
            if (GUI.Button(bigRight, localReady ? "NOT READY" : "READY", btnBigRight))
                SetReadyRpc(!localReady);
        }
    }
}