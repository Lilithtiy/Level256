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

            await VivoxService.Instance.InitializeAsync();

            if (!VivoxService.Instance.IsLoggedIn)
                await VivoxService.Instance.LoginAsync(new LoginOptions());

            await VivoxService.Instance.JoinPositionalChannelAsync(
                channelName, ChatCapability.AudioOnly, new Channel3DProperties());

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
        if (!inChannel || Time.time < nextPosUpdate) return;
        nextPosUpdate = Time.time + 0.3f;

        if (localPlayer == null)
        {
            var nm = NetworkManager.Singleton;
            var obj = nm != null && nm.IsClient ? nm.LocalClient?.PlayerObject : null;
            if (obj == null) return;
            localPlayer = obj.gameObject;
        }

        VivoxService.Instance.Set3DPosition(localPlayer, channelName);
    }
}