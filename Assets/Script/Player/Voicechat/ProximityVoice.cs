using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

public enum VoiceMode { Lobby, Game }

public class ProximityVoice : MonoBehaviour
{
    private string lobbyChannel, gameChannel;
    private GameObject localPlayer;
    private float nextPosUpdate, nextModeCheck, retryAt;

    private bool loggedIn, applying;
    private VoiceMode? applied;

    public bool IsMuted { get; private set; }

    public async void StartVoice(string sessionCode)
    {
        try
        {
            lobbyChannel = "lobby_" + sessionCode;
            gameChannel = "voice_" + sessionCode;
            applied = null;
            localPlayer = null;

            await VivoxService.Instance.InitializeAsync();
            if (!VivoxService.Instance.IsLoggedIn)
            {
                var options = new LoginOptions
                {
                    ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.TenPerSecond
                };
                await VivoxService.Instance.LoginAsync(options);
            }

            loggedIn = true;
        }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    public async void StopVoice()
    {
        loggedIn = false;
        applied = null;
        localPlayer = null;
        try { await VivoxService.Instance.LeaveAllChannelsAsync(); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        if (muted) VivoxService.Instance.MuteInputDevice();
        else VivoxService.Instance.UnmuteInputDevice();
    }

    private VoiceMode Wanted()
    {
        var lobby = LobbyManager.Instance;
        if (lobby != null && lobby.IsSpawned) return VoiceMode.Lobby;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && nm.LocalClient?.PlayerObject != null)
            return VoiceMode.Game;

        return applied ?? VoiceMode.Lobby;
    }

    private async Task Apply(VoiceMode target)
    {
        applying = true;
        try
        {
            if (applied.HasValue)
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
                applied = null;
            }
            localPlayer = null;

            if (target == VoiceMode.Lobby)
                await VivoxService.Instance.JoinGroupChannelAsync(lobbyChannel, ChatCapability.AudioOnly);
            else
                await VivoxService.Instance.JoinPositionalChannelAsync(
                    gameChannel, ChatCapability.AudioOnly, new Channel3DProperties());

            applied = target;
            Debug.Log($"Voice mode: {target}");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            applied = null;
            retryAt = Time.time + 2f;
        }
        finally { applying = false; }
    }

    private void Update()
    {
        if (!loggedIn) return;

        if (!applying && Time.time >= retryAt && Time.time >= nextModeCheck)
        {
            nextModeCheck = Time.time + 0.5f;
            var wanted = Wanted();
            if (applied != wanted) _ = Apply(wanted);
        }

        if (applied != VoiceMode.Game || Time.time < nextPosUpdate) return;
        nextPosUpdate = Time.time + 0.3f;

        if (localPlayer == null)
        {
            var nm = NetworkManager.Singleton;
            var obj = nm != null && nm.IsClient ? nm.LocalClient?.PlayerObject : null;
            if (obj == null) return;
            localPlayer = obj.gameObject;
        }

        VivoxService.Instance.Set3DPosition(localPlayer, gameChannel);
    }
}