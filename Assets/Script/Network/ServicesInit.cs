using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class ServicesInit : MonoBehaviour
{
    private async void Start()
    {
        // Initialize Unity Services before using authentication
        await UnityServices.InitializeAsync();

        // Sign in anonymously if the player isn't already authenticated
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log($"Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");
    }
}