
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkAudioManager : NetworkBehaviour
{
    public static NetworkAudioManager Instance { get; private set; }

    [Header("Audio Library")]
    [SerializeField] private ScriptableAudio[] audioLibrary;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    // Play a 3D sound at a world position.
    public void PlayNetworked3D(ScriptableAudio audio, Vector3 position)
    {
        SendAudioRequest(audio, position, true);
    }

    // Play a 2D sound for everyone.
    public void PlayNetworked2D(ScriptableAudio audio)
    {
        SendAudioRequest(audio, Vector3.zero, false);
    }

    private void SendAudioRequest(ScriptableAudio audio, Vector3 position, bool is3D)
    {
        if (audio == null || audioLibrary == null) return;

        if (!IsSpawned)
        {
            Debug.LogWarning("NetworkAudioManager is not spawned!");
            return;
        }

        int audioID = System.Array.IndexOf(audioLibrary, audio);

        if (audioID < 0)
        {
            Debug.LogWarning($"Audio {audio.name} is not in the NetworkAudioManager library!");
            return;
        }

        // The server can broadcast directly.
        if (IsServer)
        {
            PlayAudioClientRpc(audioID, position, is3D);
        }
        // Clients send their request to the server.
        else
        {
            PlayAudioServerRpc(audioID, position, is3D);
        }
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void PlayAudioServerRpc(int audioID, Vector3 position, bool is3D)
    {
        PlayAudioClientRpc(audioID, position, is3D);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayAudioClientRpc(int audioID, Vector3 position, bool is3D)
    {
        if (audioID < 0 || audioID >= audioLibrary.Length)
            return;

        ScriptableAudio audio = audioLibrary[audioID];

        if (audio == null || AudioManager.audioManagerInstance == null)
            return;

        if (is3D)
        {
            AudioManager.audioManagerInstance.PlayAt(audio, position);
        }
        else
        {
            AudioManager.audioManagerInstance.PlayLocal(audio);
        }
    }
}
