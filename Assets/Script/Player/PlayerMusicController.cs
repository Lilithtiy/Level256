using Unity.Netcode;
using UnityEngine;
using static AudioManager;

public class PlayerMusicController : NetworkBehaviour
{
    [Header("Ghost Detection")]
    [SerializeField] private float maxThreatDistance = 20f;
    [SerializeField] private float minThreatDistance = 3f;
    [SerializeField] private float detectionInterval = 0.25f;

    private float nextDetectionTime;

    private float currentIntensity;

    private GhostAI[] ghosts;

    private void Update()
    {
        if (!IsOwner) return;

        if (Time.time < nextDetectionTime) return;

        nextDetectionTime = Time.time + detectionInterval;

        UpdateMusicIntensity();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        AudioManager.audioManagerInstance?.PlayMusic(
            AudioManager.MusicState.Ambient
        );
    }

    private void UpdateMusicIntensity()
    {
        // Don't play chase music when gameplay isn't active.
        GameManager gm = GameManager.Instance;

        if (gm == null || gm.State.Value != GameState.Playing || gm.ghostState.Value != GhostState.Normal)
        {
            currentIntensity = 0f;
            AudioManager.audioManagerInstance.SetMusicIntensity(0f);
            return;
        }

        // Refresh the ghost list periodically.
        // This also catches newly spawned/respawned ghosts.
        ghosts = FindObjectsByType<GhostAI>(FindObjectsSortMode.None);

        float closestDistance = maxThreatDistance;

        foreach (GhostAI ghost in ghosts)
        {
            if (ghost == null) continue;

            Vector3 offset = ghost.transform.position - transform.position;
            offset.y = 0f;

            float distance = offset.magnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        // Convert distance into an intensity between 0 and 1.
        currentIntensity = Mathf.InverseLerp(maxThreatDistance, minThreatDistance, closestDistance);

        AudioManager.audioManagerInstance.SetMusicIntensity(currentIntensity);
    }
}