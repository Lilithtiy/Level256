using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Teleporter : NetworkBehaviour
{
    [Header("Link")] 
    [SerializeField] private Teleporter partner;
    [Tooltip("Where a player appears when they arrive here. Empty = the pad itself")]
    [SerializeField] private Transform arrivalPoint;
    [Tooltip("An exit-only pad can't be entered")]
    [SerializeField] private bool exitOnly;

    [Header("Use")]
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private float cooldown = 1.5f;
    [Range(0f, 1f)] [SerializeField] private float unlockAtProgress = 0f;

    [Header("Noise")]
    [SerializeField] private float noiseRadius = 15f;
    [SerializeField] private float noiseSeconds = 2f;

    [Header("Feedback (optional)")]
    [SerializeField] private Renderer[] padRenderers;
    [SerializeField] private Color readyColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color pulseColor = Color.white;
    [SerializeField] private ScriptableAudio useSound;

    private static Teleporter arrivalBlock;
    private static float nextUseTime;

    private readonly Dictionary<ulong, float> serverNextUse = new();

    private bool pulsing;
    private bool hasColor;
    private Color shownColor;
    private Coroutine pulse;

    private Vector3 ArrivalPosition => arrivalPoint != null ? arrivalPoint.position : transform.position;
    private float ArrivalYaw => arrivalPoint != null ? arrivalPoint.eulerAngles.y : transform.eulerAngles.y;

    private void Use()
    {
        nextUseTime = Time.time + cooldown;
        arrivalBlock = partner;
        RequestUseRpc();
    }

    private void Awake()
    {
        if (partner != null && partner.partner == null) partner.partner = this;
    }

    private bool IsUnlocked
    {
        get
        {
            if (unlockAtProgress <= 0f) return true;
            var objectives = ObjectiveManager.Instance;
            return objectives != null && objectives.IsSpawned && objectives.Progress.Value >= unlockAtProgress;
        }
    }

    private bool IsInside(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        float height = Mathf.Abs(offset.y);
        offset.y = 0f;
        return offset.magnitude <= radius && height <= 2f;
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (!pulsing)
            SetColor(IsUnlocked ? readyColor : lockedColor);

        var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (local == null) return;

        bool inside = IsInside(local.transform.position);

        if (arrivalBlock == this)
        {
            if (!inside) arrivalBlock = null;
            return;
        }

        if (!inside || exitOnly || partner == null || !IsUnlocked || Time.time < nextUseTime)
            return;

        var gm = GameManager.Instance;
        if (gm == null || gm.State.Value != GameState.Playing)
            return;

        var player = local.GetComponent<PlayerController>();
        if (player == null || player.IsCaught)
            return;

        Use();
    }

    [Rpc(SendTo.Everyone)]
    private void PulseRpc()
    {
        if (useSound != null && AudioManager.audioManagerInstance != null)
            AudioManager.audioManagerInstance.PlayAt(useSound, transform.position);

        if (pulse != null) StopCoroutine(pulse);
        pulse = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        pulsing = true;
        SetColor(pulseColor);
        yield return new WaitForSeconds(0.4f);
        pulsing = false;
    }

    private void SetColor(Color c)
    {
        if (hasColor && c == shownColor) return;
        hasColor = true;
        shownColor = c;

        foreach (var r in padRenderers)
            if (r != null) r.material.color = c;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RequestUseRpc(RpcParams rpcParams = default)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer || partner == null || exitOnly || !IsUnlocked)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!nm.ConnectedClients.TryGetValue(clientId, out var client) ||
            client.PlayerObject == null ||
            !IsInside(client.PlayerObject.transform.position))
            return;

        var gm = GameManager.Instance;
        if (gm == null || gm.State.Value != GameState.Playing)
            return;

        if (serverNextUse.TryGetValue(clientId, out float until) && Time.time < until)
            return;

        serverNextUse[clientId] = Time.time + cooldown;

        var player = client.PlayerObject.GetComponent<PlayerController>();
        if (player == null)
            return;

        player.TeleportOwnerRpc(partner.ArrivalPosition, partner.ArrivalYaw);

    }
}