using System.Globalization;
using UnityEditor.Build.Content;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class CollisionHandler : NetworkBehaviour
{
    private ScoreManager scoreManager;
    private GameManager gameManager;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        scoreManager = ScoreManager.scoreManager;
        gameManager = GameManager.Instance;
    }

    public void HandleDotCollision(int dotValue, int scoreValue, NetworkObject dotNetworkObject)
    {
        if (!IsOwner) { return; } // Only the owner should handle the collision.
        if (dotNetworkObject == null || !dotNetworkObject.IsSpawned) { return; }

        HandleDotCollisionRpc(dotValue, scoreValue, new NetworkObjectReference(dotNetworkObject));
    }

    public void HandlePowerDotCollision(int scoreValue, NetworkObject powerDotNetworkObject)
    {
        if (!IsOwner) { return; } // Only the owner should handle the collision.
        if (powerDotNetworkObject == null || !powerDotNetworkObject.IsSpawned) { return; }

        HandlePowerDotCollisionRpc(scoreValue, new NetworkObjectReference(powerDotNetworkObject));
    }

    [Rpc(SendTo.Server)]
    private void HandleDotCollisionRpc(int dotValue, int scoreValue, NetworkObjectReference dotReference, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!dotReference.TryGet(out NetworkObject dot))
            return;

        // Make sure the dot hasn't already been collected.
        if (!dot.IsSpawned)
            return;

        // Basic server-side distance validation.
        if (Vector3.Distance(transform.position, dot.transform.position) >= 3f) { return; }


        scoreManager.UpdateElectricityScore(dotValue);
        scoreManager.UpdateHighScore(scoreValue);

        
        dot.Despawn(true);
    }

    [Rpc(SendTo.Server)]
    private void HandlePowerDotCollisionRpc(int scoreValue, NetworkObjectReference powerDotReference, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!powerDotReference.TryGet(out NetworkObject powerDot))
            return;

        // Make sure the dot hasn't already been collected.
        if (!powerDot.IsSpawned)
            return;

        // Basic server-side distance validation.
        if (Vector3.Distance(transform.position, powerDot.transform.position) >= 3f) { return; }

        scoreManager.UpdateHighScore(scoreValue);
        gameManager.ActivateGhostExposedState();

        powerDot.Despawn(true);
    }
}
