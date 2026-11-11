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
        if (!IsOwner) { return; }

        CollectedDotServerRpc(dotValue, scoreValue, new NetworkObjectReference(dotNetworkObject));
    }

    [Rpc(SendTo.Server)]
    private void CollectedDotServerRpc(int dotValue, int scoreValue, NetworkObjectReference dotReference)
    {
        if(dotReference.TryGet(out NetworkObject dotNetworkObject))
        {
            // Basic server-side distance validation.
            if (Vector3.Distance(transform.position, dotNetworkObject.transform.position) >= 3f) { return; }

            gameManager.UpdateElectricityScore(dotValue, dotNetworkObject);
            gameManager.UpdateHighScore(scoreValue, dotNetworkObject);
        }
    }
}
