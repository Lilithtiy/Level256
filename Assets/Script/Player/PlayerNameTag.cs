using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerNameTag : NetworkBehaviour
{
    [Header("Placement")]
    [SerializeField] private Transform headPoint;
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private float maxDistance = 30f;

    [Header("Look")]
    [SerializeField] private float fontSize = 3f;
    [SerializeField] private float sizePerMeter = 0.1f;
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2.5f;

    private readonly NetworkVariable<FixedString32Bytes> displayName = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<byte> colorIndex = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private CharacterController controller;
    private TextMeshPro tmp;
    private Transform tagTransform;
    private bool fromLobby;

    private static Camera viewCam;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (headPoint == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "NameTagPoint") continue;
                headPoint = t;
                break;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        displayName.OnValueChanged += OnNameChanged;
        colorIndex.OnValueChanged += OnColorChanged;

        if (IsServer) AssignFromLobby();
        if (IsOwner) SubmitNameRpc(Fixed(PlayerName.Load()));

        if (!IsOwner) CreateTag();
        Refresh();
    }

    public override void OnNetworkDespawn()
    {
        displayName.OnValueChanged -= OnNameChanged;
        colorIndex.OnValueChanged -= OnColorChanged;
    }

    // server: who is who
    private void AssignFromLobby()
    {
        if (LobbyManager.Profiles.TryGetValue(OwnerClientId, out var p))
        {
            fromLobby = true;
            displayName.Value = Fixed(p.name);
            colorIndex.Value = p.color;
        }
        else // A player who comes in mid round
        {
            colorIndex.Value = FreeColor();
            displayName.Value = Fixed($"Player {OwnerClientId + 1}");
        }
    }

    private byte FreeColor()
    {
        var used = new System.Collections.Generic.HashSet<byte>();
        foreach (var other in FindObjectsByType<PlayerNameTag>(FindObjectsSortMode.None))
            if (other != this) used.Add(other.colorIndex.Value);

        for (byte c = 0; c < LobbyManager.PlayerColors.Length; c++)
            if (!used.Contains(c)) return c;
        return 0;
    }

    [Rpc(SendTo.Server)]
    private void SubmitNameRpc(FixedString32Bytes raw, RpcParams rpcParams = default)
    {
        if (fromLobby || rpcParams.Receive.SenderClientId != OwnerClientId) return;

        string clean = PlayerName.Sanitize(raw.ToString());
        if (clean.Length > 0) displayName.Value = Fixed(clean);
    }

    private static FixedString32Bytes Fixed(string s)
    {
        FixedString32Bytes f = default;
        f.CopyFromTruncated(s ?? "");
        return f;
    }

    private void CreateTag()
    {
        var go = new GameObject("NameTag", typeof(RectTransform));
        tagTransform = go.transform;
        tagTransform.SetParent(transform, false);

        tmp = go.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.richText = false;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.rectTransform.sizeDelta = new Vector2(12f, 2f);
    }

    private void OnNameChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
    private void OnColorChanged(byte previous, byte current) => Refresh();

    private void Refresh()
    {
        if (tmp == null) return;

        tmp.text = displayName.Value.ToString();
        tmp.color = LobbyManager.PlayerColors[colorIndex.Value % LobbyManager.PlayerColors.Length];
    }

    private static Camera ViewCamera()
    {
        if (viewCam != null && viewCam.isActiveAndEnabled) return viewCam;

        var nm = NetworkManager.Singleton;
        var local = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        viewCam = local != null ? local.GetComponentInChildren<Camera>() : null;
        if (viewCam == null) viewCam = Camera.main;
        return viewCam;
    }

    private void LateUpdate()
    {
        if (tagTransform == null) return;

        var cam = ViewCamera();
        if (cam == null) { SetTagVisible(false); return; }

        Vector3 head = headPoint != null
            ? headPoint.position
            : transform.TransformPoint(controller.center + Vector3.up * (controller.height * 0.5f));
        head += Vector3.up * heightOffset;

        float dist = Vector3.Distance(cam.transform.position, head);
        bool visible = dist <= maxDistance;
        SetTagVisible(visible);
        if (!visible) return;

        tagTransform.position = head;
        tagTransform.rotation = cam.transform.rotation;
        tagTransform.localScale = Vector3.one * Mathf.Clamp(dist * sizePerMeter, minScale, maxScale);
    }

    private void SetTagVisible(bool visible)
    {
        if (tagTransform.gameObject.activeSelf != visible)
            tagTransform.gameObject.SetActive(visible);
    }
}