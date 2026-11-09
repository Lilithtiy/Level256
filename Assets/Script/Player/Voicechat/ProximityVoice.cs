using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

public class ProximityVoice : MonoBehaviour
{
    private string channelName;
    private GameObject localPlayer;
    private bool inChannel;
    private float nextPosUpdate;

    public async void StartVoice(string sessionCode)
    {
        try
        {
            channelName = "voice_" + sessionCode;

            if (VivoxService.Instance == null) return;
            await VivoxService.Instance.InitializeAsync();

            if (!VivoxService.Instance.IsLoggedIn)
                await VivoxService.Instance.LoginAsync(new LoginOptions());

            // Wait until the local player has spawned
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null)
                await Awaitable.NextFrameAsync();
            localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;

            var props = new Channel3DProperties();
            await VivoxService.Instance.JoinPositionalChannelAsync(
                channelName, ChatCapability.AudioOnly, props);

            inChannel = true;
            Debug.Log($"Joined voice channel {channelName}");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    public async void StopVoice()
    {
        inChannel = false;
        try { await VivoxService.Instance.LeaveAllChannelsAsync(); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    private void Update()
    {
        if (!inChannel || localPlayer == null || Time.time < nextPosUpdate) return;

        // Throttle position updates to about 3 per second
        nextPosUpdate = Time.time + 0.3f;
        VivoxService.Instance.Set3DPosition(localPlayer, channelName);
    }
}