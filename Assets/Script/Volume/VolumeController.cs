using UnityEngine;
using Unity.Netcode;
using UnityEngine.Rendering;

public class VolumeController : NetworkBehaviour
{
    private GameManager gameManager;
    private Volume volume;
    [SerializeField] private VolumeProfile defaultVolume;
    [SerializeField] private VolumeProfile exposedVolume;

    //why cant I see this in the inspector? I want to be able to tweak these values in the editor
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float minPulseWeight = 0.5f;
    [SerializeField] private float maxPulseWeight = 1f;

    public override void OnNetworkSpawn()
    {
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

            if (gameManager.ghostExposedTimer.Value <= 5f)
            {
                PulseVolume();
            }
            else
            {
                volume.weight = maxPulseWeight;
            }
        }
        else
        {
            volume.profile = defaultVolume;
            volume.weight = maxPulseWeight;
        }
    }

    private void PulseVolume()
    {
        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;

        volume.weight = Mathf.Lerp(minPulseWeight, maxPulseWeight, pulse);
    }
}
