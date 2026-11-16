using System;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Netcode;

public class OnDotCollision : NetworkBehaviour
{
    [SerializeField] int dotValue = 5;
    [SerializeField] int scoreValue = 10;

    private void OnTriggerEnter(Collider other)
    {   
        if (other.gameObject.CompareTag("Player"))
        {
            // Get the CollisionHandler component from the collided player
            CollisionHandler collisionHandler = other.gameObject.GetComponentInParent<CollisionHandler>();
            if (collisionHandler != null && collisionHandler.IsOwner)
            {
                // Call the method to handle the dot collision
                collisionHandler.HandleDotCollision(dotValue, scoreValue, NetworkObject);
            }
        }
    }
}
