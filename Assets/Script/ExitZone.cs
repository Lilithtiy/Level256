using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExitZone : NetworkBehaviour
{
    [SerializeField] private GameObject barrier;
    [SerializeField] private float holdSeconds = 2f;

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
        bool open = IsOpen;
        if (barrier != null && barrier.activeSelf == open)
            barrier.SetActive(!open);

        if (!IsServer) return;

        var gm = GameManager.Instance;
        if (!open || gm == null || gm.State.Value != GameState.Playing)
        {
            timer = 0f;
            return;
        }

        int total = 0, inside = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            total++;
            if (zone.bounds.Contains(client.PlayerObject.transform.position)) inside++;
        }
        PlayersInside.Value = inside;
        PlayersTotal.Value = total;

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