using Unity.Netcode;
using UnityEngine;

public class PlayerAnimator : NetworkBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int BlinkHash = Animator.StringToHash("Blink");
    private static readonly int SurprisedHash = Animator.StringToHash("Surprised");

    [Header("Walking")]
    [Tooltip("Higher = reacts faster, lower = smoother")]
    [SerializeField] private float smoothing = 12f;

    [Header("Eyes")]
    [SerializeField] private float minBlinkGap = 2f;
    [SerializeField] private float maxBlinkGap = 6f;
    [Tooltip("No blinking for this long after a surprised face")]
    [SerializeField] private float surprisedSeconds = 1.5f;

    private Animator animator;
    private Vector3 lastPosition;
    private float speed;
    private float nextBlink;
    private float noBlinkUntil;

    private bool CanAnimate => animator != null && animator.isActiveAndEnabled;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        speed = 0f;
        ScheduleBlink();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && GameManager.Instance != null)
            GameManager.Instance.OnPlayerCaught += OnPlayerCaught;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && GameManager.Instance != null)
            GameManager.Instance.OnPlayerCaught -= OnPlayerCaught;
    }

    // A ghost caught somebody. Only the caught player's pacman looks surprised
    private void OnPlayerCaught(ulong clientId)
    {
        if (clientId == OwnerClientId) SurprisedRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void SurprisedRpc()
    {
        noBlinkUntil = Time.time + surprisedSeconds;
        if (!CanAnimate) return;

        animator.ResetTrigger(BlinkHash);
        animator.SetTrigger(SurprisedHash);
    }

    private void ScheduleBlink() => nextBlink = Time.time + Random.Range(minBlinkGap, maxBlinkGap);

    private void Update()
    {
        // Walking speed, measured from how far the player moved
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;

        float raw = delta.magnitude < 3f ? delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) : 0f;
        speed = Mathf.Lerp(speed, raw, smoothing * Time.deltaTime);

        if (!CanAnimate) return;
        animator.SetFloat(SpeedHash, speed);

        // Random blinks
        if (Time.time < nextBlink) return;
        ScheduleBlink();
        if (Time.time >= noBlinkUntil) animator.SetTrigger(BlinkHash);
    }
}