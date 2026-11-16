using Unity.Netcode;
using UnityEngine;

public class OnPowerDotCollision : NetworkBehaviour
{
    [SerializeField] int scoreValue = 50;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // Get the CollisionHandler component from the collided player
            CollisionHandler collisionHandler = other.gameObject.GetComponentInParent<CollisionHandler>();
            if (collisionHandler != null && collisionHandler.IsOwner)
            {
                // Call the method to handle the dot collision
                collisionHandler.HandlePowerDotCollision(scoreValue, NetworkObject);
            }
        }
    }
}
