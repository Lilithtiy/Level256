using UnityEngine;
using Unity.Netcode;
using UnityEngine.Rendering;

public class VolumeController : NetworkBehaviour
{
    private GameManager gameManager;
    private Volume volume;
    [SerializeField] private VolumeProfile defaultVolume;
    [SerializeField] private VolumeProfile exposedVolume;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        gameManager = GameManager.Instance;

        if (volume == null)
        {
            volume = GetComponent<Volume>();
            if (volume == null)
            {
                Debug.LogError("VolumeController: No Volume component found on the GameObject.");
                return;
            }
        }
    }

    private void Update()
    {
        if(gameManager.ghostState.Value == GhostState.Exposed)
        {
            volume.profile = exposedVolume;
        }
        else
        {
            volume.profile = defaultVolume;
        }
    }
}
