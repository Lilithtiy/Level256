using Unity.Netcode;
using UnityEngine;

public class FuseTask : StationTask
{
    private const ulong None = ulong.MaxValue;

    [Header("Fuse")]
    [SerializeField] private GameObject fuse;
    [SerializeField] private Transform[] fuseSpots;
    [SerializeField] private Transform fuseBox;
    [SerializeField] private Color fuseColor = new Color(1f, 0.9f, 0.2f);

    [Header("Carrying")]
    [SerializeField] private float carryNoiseRadius = 14f;
    [Tooltip("Empty = a copy of the fuse")]
    [SerializeField] private GameObject heldPrefab;
    [SerializeField] private Vector3 heldOffset = new Vector3(0.3f, 1.1f, 0.5f);

    public NetworkVariable<ulong> CarrierId = new(None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> SpotIndex = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override TaskMode Mode => TaskMode.Press;
    public override string Prompt => "Press E to use the fuse box";
    public override string BoardText => "FUSE BOX\nNEEDS FUSE";

    public override string HudDetail
    {
        get
        {
            if (CarrierId.Value == None) return "FIND THE FUSE";
            return CarrierId.Value == NetworkManager.Singleton.LocalClientId ? "YOU HAVE IT" : "TEAMMATE HAS IT";
        }
    }

    private GameObject held;
    private ulong heldFor = None;
    private float nextNoise;

    // helpers
    private Transform FuseSpot
    {
        get
        {
            if (fuseSpots != null && fuseSpots.Length > 0)
                return fuseSpots[Mathf.Clamp(SpotIndex.Value, 0, fuseSpots.Length - 1)];
            return fuse != null ? fuse.transform : transform;
        }
    }

    private bool Near(Transform t, Vector3 position)
    {
        if (t == null) return false;
        Vector3 offset = position - t.position;
        offset.y = 0f;
        return offset.magnitude <= MaxDistance;
    }

    public override bool InRange(Vector3 position)
        => Near(fuseBox, position) || (CarrierId.Value == None && Near(FuseSpot, position));

    public override string PromptAt(Vector3 point)
    {
        bool atBox = fuseBox != null && (point - fuseBox.position).sqrMagnitude <= (point - FuseSpot.position).sqrMagnitude;

        if (!atBox) return "Press E to pick up the fuse (it's loud!)";

        ulong me = NetworkManager.Singleton.LocalClientId;
        if (CarrierId.Value == me) return "Press E to insert the fuse";
        if (CarrierId.Value == None) return "The fuse box is dead. Find the fuse";
        return "Waiting for your teammate to bring the fuse";
    }

    // server
    protected override void OnRoundSetup()
    {
        CarrierId.Value = None;
        SpotIndex.Value = fuseSpots != null && fuseSpots.Length > 0 ? Random.Range(0, fuseSpots.Length) : 0;
    }

    protected override void OnInteract(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (!nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null) return;
        Vector3 pos = client.PlayerObject.transform.position;

        if (CarrierId.Value == clientId)
        {
            if (!Near(fuseBox, pos)) return;

            CarrierId.Value = None;
            EmitAlarmAt(fuseBox.position, 12f, 1.5f);
            FinishNow();
        }
        else if (CarrierId.Value == None && Near(FuseSpot, pos))
        {
            CarrierId.Value = clientId;
            Progress.Value = 0.5f;
            EmitAlarmAt(FuseSpot.position, 10f, 1f);
        }
    }

    protected override void ServerIdle(float deltaTime)
    {
        if (CarrierId.Value == None) return;

        var nm = NetworkManager.Singleton;
        if (!nm.ConnectedClients.TryGetValue(CarrierId.Value, out var client) || client.PlayerObject == null)
        {
            Drop();
            return;
        }

        if (Time.time < nextNoise) return;
        nextNoise = Time.time + 0.25f;
        EmitAlarmAt(client.PlayerObject.transform.position, carryNoiseRadius, 0.4f);
    }

    private void OnPlayerCaught(ulong clientId)
    {
        if (CarrierId.Value == clientId) Drop();
    }

    private void Drop()
    {
        CarrierId.Value = None;
        if (!IsDone.Value) Progress.Value = 0f;
    }

    // client visuals
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CarrierId.OnValueChanged += OnCarrierChanged;
        SpotIndex.OnValueChanged += OnSpotChanged;

        if (IsServer && GameManager.Instance != null)
            GameManager.Instance.OnPlayerCaught += OnPlayerCaught;

        RefreshVisuals();
    }

    public override void OnNetworkDespawn()
    {
        CarrierId.OnValueChanged -= OnCarrierChanged;
        SpotIndex.OnValueChanged -= OnSpotChanged;

        if (IsServer && GameManager.Instance != null)
            GameManager.Instance.OnPlayerCaught -= OnPlayerCaught;

        if (held != null) Destroy(held);
        held = null;

        base.OnNetworkDespawn();
    }

    private void OnCarrierChanged(ulong previous, ulong current) => RefreshVisuals();
    private void OnSpotChanged(int previous, int current) => RefreshVisuals();

    private void LateUpdate()
    {
        if (IsSpawned) UpdateHeld();
    }

    protected override void RefreshVisuals()
    {
        base.RefreshVisuals();

        if (fuse == null) return;

        bool show = IsActive.Value && !IsDone.Value && CarrierId.Value == None;

        var spot = FuseSpot;
        if (spot != fuse.transform) fuse.transform.SetPositionAndRotation(spot.position, spot.rotation);
        fuse.SetActive(show);

        foreach (var r in fuse.GetComponentsInChildren<Renderer>(true))
            r.material.color = fuseColor;
    }

    // Shows a small copy of the fuse on whoever is carrying it
    private void UpdateHeld()
    {
        ulong carrier = IsActive.Value && !IsDone.Value ? CarrierId.Value : None;
        if (carrier == heldFor && (carrier == None || held != null)) return;

        if (held != null) Destroy(held);
        held = null;
        heldFor = carrier;
        if (carrier == None) return;

        var player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(carrier);
        if (player == null)
        {
            heldFor = None;
            return;
        }

        var source = heldPrefab != null ? heldPrefab : fuse;
        if (source == null) return;

        held = Instantiate(source);

        // The held copy must not block the interact ray or show up on the map
        foreach (var c in held.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var m in held.GetComponentsInChildren<MarkerTarget>(true))
        {
            m.enabled = false;
            Destroy(m);
        }

        held.SetActive(true);
        held.transform.SetParent(player.transform, false);
        held.transform.localPosition = heldOffset;
        held.transform.localRotation = Quaternion.identity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (fuseSpots != null)
            foreach (var s in fuseSpots)
                if (s != null) Gizmos.DrawWireSphere(s.position, MaxDistance);

        Gizmos.color = Color.cyan;
        if (fuseBox != null) Gizmos.DrawWireSphere(fuseBox.position, MaxDistance);
    }
}