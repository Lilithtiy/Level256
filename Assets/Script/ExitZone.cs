using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExitZone : NetworkBehaviour
{
    [SerializeField] private GameObject barrier;
    [SerializeField] private float holdSeconds = 2f;

    // Server-updated counts displayed by clients
    public NetworkVariable<int> PlayersInside = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> PlayersTotal = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Collider zone;
    private float timer;

    private void Awake()
    {
        zone = GetComponent<Collider>();
    }

    private bool IsOpen => ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsSpawned && ObjectiveManager.Instance.ExitOpen.Value;

    private void Update()
    {
        // Hide the barrier when the exit opens
        bool open = IsOpen;
        if (barrier != null && barrier.activeSelf == open)
            barrier.SetActive(!open);

        if (!IsServer) return; // Only the server tracks players and determines whether the team wins

        var gm = GameManager.Instance;
        if (!open || gm == null || gm.State.Value != GameState.Playing)
        {
            timer = 0f;
            return;
        }

        // Count connected players inside the exit zone
        int total = 0, inside = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            total++;
            if (zone.bounds.Contains(client.PlayerObject.transform.position)) inside++;
        }
        PlayersInside.Value = inside;
        PlayersTotal.Value = total;

        // Win only after every player stays inside for the required duration
        if (total > 0 && inside == total)
        {
            timer += Time.deltaTime;
            if (timer >= holdSeconds) gm.Win();
        }
        else
        {
            timer = 0f;
        }
    }
}