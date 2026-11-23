using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class SessionUI : MonoBehaviour
{
    [Header("Relay")]
    [Tooltip("Empty = automatic")]
    [SerializeField] private string relayRegion = "";

    [Header("Look")]
    [SerializeField] private string gameTitle = "LEVEL256";
    [SerializeField] private string tagline = "UP TO 4 PLAYERS";
    [SerializeField] private Font font;

    [Header("Scenes")]
    [SerializeField] private string bootSceneName = "Bootstrap";
    [SerializeField] private string tutorialSceneName = "Tutorial";

    private static readonly Color Yellow = new Color(1f, 0.92f, 0.016f, 1f);

    private string joinCode = "";
    private string status = "";
    private string playerName = "";
    private ISession session;
    private bool busy;

    public static string JoinCode;

    private void Start()
    {
        playerName = PlayerName.Load();
        NetworkManager.Singleton.OnClientStopped += OnNetworkStopped;
        AudioManager.audioManagerInstance?.PlayMusic(AudioManager.MusicState.MainMenu);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientStopped -= OnNetworkStopped;
    }

    private void OnNetworkStopped(bool wasHost)
    {
        if (session == null) return;
        LeaveSession(wasHost ? "" : "The host left the session.");
        AudioManager.audioManagerInstance?.PlayMusic(AudioManager.MusicState.MainMenu);
    }

    // Session logic
    private async void HostGame()
    {
        if (busy) return;
        busy = true;
        PlayerName.Save(playerName);
        status = "Creating session...";
        float start = Time.realtimeSinceStartup;

        try
        {
            var baseOptions = new SessionOptions { MaxPlayers = 4 };
            var options = string.IsNullOrEmpty(relayRegion) ? baseOptions.WithRelayNetwork() : baseOptions.WithRelayNetwork(new RelayNetworkOptions(RelayProtocol.Default, relayRegion, true));

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            JoinCode = session.Code;
            status = "Loading lobby...";
            GameFlow.Instance.OpenLobby();
            AudioManager.audioManagerInstance?.PlayMusic(AudioManager.MusicState.Lobby);

            GetComponent<ProximityVoice>().StartVoice(session.Code);
            status = $"Hosting. Join code: {session.Code}";
        }
        catch (System.Exception e)
        {
            session = null;
            status = $"Host failed: {e.Message}";
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
        }
    }

    private async void JoinGame()
    {
        if (busy) return;
        busy = true;
        PlayerName.Save(playerName);
        status = "Joining...";
        float start = Time.realtimeSinceStartup;

        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim());
            JoinCode = session.Code;
            Debug.Log($"JoinSession took {Time.realtimeSinceStartup - start:0.0} s");

            GetComponent<ProximityVoice>().StartVoice(session.Code);
            status = "Joined session";
            AudioManager.audioManagerInstance?.PlayMusic(AudioManager.MusicState.Lobby);
        }
        catch (System.Exception e)
        {
            session = null;
            status = $"Join failed: {e.Message}";
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
        }
    }

    public void LeaveSession(string message = "")
    {
        GetComponent<ProximityVoice>().StopVoice();

        var oldSession = session;
        session = null;
        JoinCode = null;
        status = message;

        _ = LeaveAsyncSafe(oldSession);

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) nm.Shutdown();

        Invoke(nameof(GoToBoot), 0.2f);
    }

    private async System.Threading.Tasks.Task LeaveAsyncSafe(ISession s)
    {
        if (s == null) return;
        try { await s.LeaveAsync(); }
        catch (System.Exception e) { Debug.Log($"Leave: {e.Message}"); }
    }

    private void GoToBoot()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (SceneManager.GetActiveScene().name != bootSceneName)
            SceneManager.LoadScene(bootSceneName);
            AudioManager.audioManagerInstance?.PlayMusic(AudioManager.MusicState.MainMenu);
    }

    // UI
    private GUIStyle titleStyle, tagStyle, labelStyle, btnBig, btnSmall, field, statusStyle;

    private GUIStyle Make(TextAnchor align, Color color, FontStyle fs = FontStyle.Normal, bool hover = false)
    {
        var st = new GUIStyle { alignment = align, fontStyle = fs, richText = false };
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
        if (titleStyle != null) return;

        var gray = new Color(1f, 1f, 1f, 0.55f);

        titleStyle = Make(TextAnchor.MiddleCenter, Yellow, FontStyle.BoldAndItalic);
        tagStyle = Make(TextAnchor.MiddleCenter, gray);
        labelStyle = Make(TextAnchor.MiddleLeft, gray);
        btnBig = Make(TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.9f), FontStyle.Normal, true);
        btnSmall = Make(TextAnchor.MiddleCenter, Yellow, FontStyle.Normal, true);
        btnSmall.hover.textColor = Color.white;
        btnSmall.active.textColor = Color.white;
        statusStyle = Make(TextAnchor.MiddleCenter, gray);

        field = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleCenter };
        if (font != null) field.font = font;
    }

    private void ScaleStyles(float s)
    {
        titleStyle.fontSize = Mathf.RoundToInt(150 * s);
        tagStyle.fontSize = Mathf.RoundToInt(26 * s);
        labelStyle.fontSize = Mathf.RoundToInt(22 * s);
        btnBig.fontSize = Mathf.RoundToInt(54 * s);
        btnSmall.fontSize = Mathf.RoundToInt(34 * s);
        field.fontSize = Mathf.RoundToInt(30 * s);
        statusStyle.fontSize = Mathf.RoundToInt(24 * s);
    }

    private static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static bool Btn(Rect r, string label, GUIStyle style)
    {
        if (GUI.enabled && r.Contains(Event.current.mousePosition))
            Fill(r, new Color(1f, 0.6f, 0f, 0.10f));
        return GUI.Button(r, label, style);
    }

    private void OnGUI()
    {
        // Only display this UI in Bootstrap
        if (SceneManager.GetActiveScene().name == tutorialSceneName)
            return;

        if (session != null || SettingsMenu.IsOpen || CreditsMenu.IsOpen) return;

        BuildStyles();
        float s = Screen.height / 1080f;
        ScaleStyles(s);

        // Background
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.04f, 0.06f, 1f));
        Fill(new Rect(0, 0, Screen.width, 6 * s), Yellow);
        Fill(new Rect(0, Screen.height - 6 * s, Screen.width, 6 * s), Yellow);

        float cx = Screen.width / 2f;
        float w = 560 * s;
        float x = cx - w / 2f;
        float y = Screen.height * 0.10f;

        // Title
        GUI.Label(new Rect(0, y, Screen.width, 170 * s), gameTitle.ToUpper(), titleStyle);
        GUI.Label(new Rect(0, y + 165 * s, Screen.width, 34 * s), tagline.ToUpper(), tagStyle);
        y += 290 * s;

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            GUI.Label(new Rect(0, y, Screen.width, 50 * s), "SIGNING IN...", statusStyle);
            return;
        }

        if (busy)
        {
            GUI.Label(new Rect(0, y, Screen.width, 50 * s), status.ToUpper(), statusStyle);
            return;
        }

        // Name
        GUI.Label(new Rect(x, y, w, 28 * s), "YOUR NAME", labelStyle);
        y += 30 * s;
        playerName = GUI.TextField(new Rect(x, y, w, 52 * s), playerName ?? "", PlayerName.MaxLength, field);
        y += 90 * s;

        // Host
        if (Btn(new Rect(x, y, w, 80 * s), "HOST GAME", btnBig)) HostGame();
        y += 100 * s;

        // Divider
        Fill(new Rect(x, y + 10 * s, w * 0.4f, 2 * s), new Color(1f, 1f, 1f, 0.15f));
        Fill(new Rect(x + w * 0.6f, y + 10 * s, w * 0.4f, 2 * s), new Color(1f, 1f, 1f, 0.15f));
        GUI.Label(new Rect(x, y - 6 * s, w, 34 * s), "OR", tagStyle);
        y += 50 * s;

        // Join
        GUI.Label(new Rect(x, y, w, 28 * s), "JOIN CODE", labelStyle);
        y += 30 * s;

        GUI.SetNextControlName("JoinCode");
        joinCode = GUI.TextField(new Rect(x, y, w, 52 * s), joinCode ?? "", 6, field).ToUpper();

        Event e = Event.current;
        bool enter = e.type == EventType.KeyDown
            && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == "JoinCode";
        y += 66 * s;

        GUI.enabled = joinCode.Trim().Length > 0;
        if (Btn(new Rect(x, y, w, 56 * s), "JOIN", btnSmall) || (enter && joinCode.Trim().Length > 0))
        {
            if (enter) e.Use();
            JoinGame();
        }
        GUI.enabled = true;

        y += 80 * s;

        Fill(new Rect(x, y + 10 * s, w * 0.4f, 2 * s), new Color(1f, 1f, 1f, 0.15f));
        Fill(new Rect(x + w * 0.6f, y + 10 * s, w * 0.4f, 2 * s), new Color(1f, 1f, 1f, 0.15f));
        GUI.Label(new Rect(x, y - 6 * s, w, 34 * s), "OR", tagStyle);
        y += 50 * s;

        if (Btn(new Rect(x, y, w, 80 * s), "TUTORIAL", btnBig)) GameFlow.Instance.StartTutorial();

        y += 100 * s;

        if (Btn(new Rect(x, y, w, 56 * s), "SETTINGS", btnSmall))
            SettingsMenu.Instance?.Open();
        
        y += 64 * s;
        if (Btn(new Rect(x, y, w, 56 * s), "CREDITS", btnSmall))
            CreditsMenu.Instance?.Open();

        // Status/errors
        if (!string.IsNullOrEmpty(status))
        {
            bool error = status.Contains("failed");
            statusStyle.normal.textColor = error ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(cx - 600 * s, Screen.height - 90 * s, 1200 * s, 40 * s), status, statusStyle);
        }
    }
}