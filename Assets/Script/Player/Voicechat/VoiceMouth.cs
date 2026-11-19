using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using UnityEngine;

public class VoiceMouth : NetworkBehaviour
{
    [SerializeField] private Transform mouth;

    [Header("Look")]
    [SerializeField] private Vector3 openScale = new Vector3(0.85f, 4f, 1f);

    [Header("Response")]
    [Tooltip("Higher = the mouth opens wider for the same loudness")]
    [SerializeField] private float gain = 2.5f;
    [SerializeField] private float minOpenWhileSpeaking = 0.25f;
    [SerializeField] private float openSpeed = 25f;
    [SerializeField] private float closeSpeed = 12f;
    [SerializeField] private bool debugLog;

    private readonly NetworkVariable<FixedString64Bytes> voiceId = new( default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Vector3 closedScale = Vector3.one;
    private string cachedId = "";
    private float level;
    private float nextLog;

    private void Awake()
    {
        if (mouth == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "pacMouth") continue;
                mouth = t;
                break;
            }
        }

        if (mouth != null) closedScale = mouth.localScale;
    }

    public override void OnNetworkSpawn()
    {
        voiceId.OnValueChanged += OnIdChanged;
        cachedId = voiceId.Value.ToString();

        if (!IsOwner) return;

        try { voiceId.Value = AuthenticationService.Instance.PlayerId; }
        catch (System.Exception e) { Debug.LogWarning($"VoiceMouth: no player id yet ({e.Message})"); }
    }

    public override void OnNetworkDespawn()
    {
        voiceId.OnValueChanged -= OnIdChanged;
    }

    private void OnIdChanged(FixedString64Bytes previous, FixedString64Bytes current)
        => cachedId = current.ToString();

    // How loud this player is right now, 0 to 1
    private float TargetLevel()
    {
        if (string.IsNullOrEmpty(cachedId)) return 0f;

        float best = 0f;
        bool speaking = false;
        try
        {
            var vivox = VivoxService.Instance;
            if (!vivox.IsLoggedIn) return 0f;

            foreach (var channel in vivox.ActiveChannels)
            {
                foreach (var p in channel.Value)
                {
                    // Only count real speech, so background noise doesn't flap the mouth
                    if (p.PlayerId != cachedId || !p.SpeechDetected) continue;
                    speaking = true;
                    best = Mathf.Max(best, (float)p.AudioEnergy);
                }
            }
        }
        catch { return 0f; } // voice isn't ready yet

        if (!speaking) return 0f;

        if (debugLog && Time.time >= nextLog)
        {
            nextLog = Time.time + 0.5f;
            Debug.Log($"{name}: speaking, voice energy {best:0.00}");
        }

        // Even if the energy value is tiny, the mouth still opens a bit while someone is talking
        return Mathf.Clamp01(Mathf.Max(best * gain, minOpenWhileSpeaking));
}

    // Runs after the Animator, so this wins if an animation also touches the mouth
    private void LateUpdate()
    {
        if (mouth == null || !IsSpawned) return;

        // Never see your own pacman, so only other players' mouths are driven
        float target = IsOwner ? 0f : TargetLevel();

        float rate = target > level ? openSpeed : closeSpeed;
        level = Mathf.Lerp(level, target, rate * Time.deltaTime);

        mouth.localScale = Vector3.Scale(closedScale, Vector3.Lerp(Vector3.one, openScale, level));
    }
}