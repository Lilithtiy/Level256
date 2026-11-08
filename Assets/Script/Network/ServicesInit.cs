using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class ServicesInit : MonoBehaviour
{
    private async void Start()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log($"Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");
    }
}