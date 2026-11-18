using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    [Tooltip("Higher = reacts faster, lower = smoother")]
    [SerializeField] private float smoothing = 12f;

    private Animator animator;
    private Vector3 lastPosition;
    private float speed;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        speed = 0f;
    }

    private void Update()
    {
        if (animator == null) return;

        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;

        float raw = delta.magnitude < 3f ? delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) : 0f;

        speed = Mathf.Lerp(speed, raw, smoothing * Time.deltaTime);
        animator.SetFloat(SpeedHash, speed);
    }
}