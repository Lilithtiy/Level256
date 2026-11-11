using System.Globalization;
using UnityEditor.Build.Content;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class CollisionHandler : NetworkBehaviour
{
    private GameManager gameManager = GameManager.gameManager;

    public void HandleDotCollision(int dotValue, int scoreValue, NetworkObject dotNetworkObject)
    {
        if (!IsOwner) { return; } // Only the owner should handle the collision.
        if (dotNetworkObject == null || !dotNetworkObject.IsSpawned) { return; }

        HandleDotCollisionRpc(dotValue, scoreValue, new NetworkObjectReference(dotNetworkObject));
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


        gameManager.UpdateElectricityScore(dotValue, dotReference);
        gameManager.UpdateHighScore(scoreValue, dotReference);

        if (dot != null && dot.IsSpawned)
        {
            dot.Despawn(true);

        }
    }
}
