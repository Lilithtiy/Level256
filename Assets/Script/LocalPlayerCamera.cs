using Unity.Netcode;
using UnityEngine;

public class LocalPlayerCamera : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;

    public override void OnNetworkSpawn()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            return;

        playerCamera.enabled = IsOwner; // Only enable the camera for the player who owns this network object

        if (IsOwner)
        {
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = Color.black;
        }
    }
}