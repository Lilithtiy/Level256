using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionUI : MonoBehaviour
{
    private string joinCode = "";
    private string status = "Not connected";
    private ISession session;

    private async void HostGame()
    {
        try
        {
            var options = new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            GetComponent<ProximityVoice>().StartVoice(session.Code);
            status = $"Hosting. Join code: {session.Code}";
            Debug.Log($"Join code: {session.Code}");
        }
        catch (System.Exception e)
        {
            status = $"Host failed: {e.Message}";
            Debug.LogException(e);
        }
    }

    private async void JoinGame()
    {
        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim());
            GetComponent<ProximityVoice>().StartVoice(session.Code);
            status = "Joined session";
        }
        catch (System.Exception e)
        {
            status = $"Join failed: {e.Message}";
            Debug.LogException(e);
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 120, 300, 200));

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            GUILayout.Label("Signing in...");
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
            status = "Left session";
        }

        GUILayout.Label(status);
        GUILayout.EndArea();
    }
}