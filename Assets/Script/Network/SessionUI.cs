using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionUI : MonoBehaviour
{
    [Header("Relay")]
    [Tooltip("Empty = automatic")]
    [SerializeField] private string relayRegion = "";

    private string joinCode = "";
    private string status = "Not connected";
    private ISession session;
    private bool busy;

    public static string JoinCode;

    private async void HostGame()
    {
        if (busy) return;
        busy = true;
        status = "Creating session...";
        float start = Time.realtimeSinceStartup;

        try
        {
            var baseOptions = new SessionOptions { MaxPlayers = 4 };
            var options = string.IsNullOrEmpty(relayRegion)
                ? baseOptions.WithRelayNetwork()
                : baseOptions.WithRelayNetwork(new RelayNetworkOptions(RelayProtocol.Default, relayRegion, true));

            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"CreateSession took {Time.realtimeSinceStartup - start:0.0} s");

            JoinCode = session.Code;
            status = "Loading lobby...";
            GameFlow.Instance.OpenLobby();

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
        status = "Joining...";
        float start = Time.realtimeSinceStartup;

        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim());
            Debug.Log($"JoinSession took {Time.realtimeSinceStartup - start:0.0} s");

            GetComponent<ProximityVoice>().StartVoice(session.Code);
            status = "Joined session";
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

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 120, 300, 200));

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            GUILayout.Label("Signing in...");
        }
        else if (busy)
        {
            // Just show the status while we wait
        }
        else if (session == null)
        {
            if (GUILayout.Button("Host (Relay)")) HostGame();
            joinCode = GUILayout.TextField(joinCode, 6).ToUpper();
            if (GUILayout.Button("Join with code")) JoinGame();
        }
        else if (GUILayout.Button("Leave"))
        {
            GetComponent<ProximityVoice>().StopVoice();
            session.LeaveAsync();
            session = null;
            JoinCode = null;
            status = "Left session";
        }

        GUILayout.Label(status);
        GUILayout.EndArea();
    }
}