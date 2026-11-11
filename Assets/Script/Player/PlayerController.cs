using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference lookAction;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Crouch")]
    [SerializeField] private Transform visual;
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float standEyeHeight = 1.7f;
    [SerializeField] private float crouchEyeHeight = 1.0f;


    [Header("Camera")]
    [SerializeField] private float mouseSensitivity = 2f;
    new Camera camera;
    Transform cameraTransform;
    private Vector2 lookInput;
    private float xRotation = 0f;

    private AudioListener audioListener;

    private readonly NetworkVariable<bool> isCrouching = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        camera = GetComponentInChildren<Camera>();
        cameraTransform = camera.transform;
        audioListener = camera.GetComponent<AudioListener>();
    }

    public override void OnNetworkSpawn()
    {
        isCrouching.OnValueChanged += OnCrouchChanged;
        ApplyCrouch(isCrouching.Value);

        camera.enabled = IsOwner;
        audioListener.enabled = IsOwner;

        if (!IsOwner) return;

        moveAction.action.Enable();
        jumpAction.action.Enable();
        crouchAction.action.Enable();
        sprintAction.action.Enable();
        lookAction.action.Enable();

        controller.enabled = false;
        transform.position = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
        controller.enabled = true;

        if (visual != null) visual.gameObject.SetActive(false);
        SetCursorLocked(true);
    }

    public override void OnNetworkDespawn()
    {
        isCrouching.OnValueChanged -= OnCrouchChanged;

        if (!IsOwner) return;

        moveAction.action.Disable();
        jumpAction.action.Disable();
        crouchAction.action.Disable();
        sprintAction.action.Disable();
        lookAction.action.Disable();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        if (Cursor.lockState != CursorLockMode.Locked &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        bool crouching = crouchAction.action.IsPressed();
        if (crouching != isCrouching.Value)
        {
            isCrouching.Value = crouching;
            ApplyCrouch(crouching);
        }

        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;

        if (grounded && !crouching && jumpAction.action.WasPressedThisFrame())
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        float speed = crouching ? crouchSpeed
                    : sprintAction.action.IsPressed() ? runSpeed
                    : walkSpeed;

        Vector2 input = moveAction.action.ReadValue<Vector2>();

        //movement in relation to the players rotation
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move *= speed;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            lookInput = lookAction.action.ReadValue<Vector2>();
            Look();
        }
    }

    private void OnCrouchChanged(bool previous, bool current) => ApplyCrouch(current);

    private void ApplyCrouch(bool crouching)
    {
        float h = crouching ? crouchHeight : standHeight;

        controller.height = h;
        controller.center = new Vector3(0f, h * 0.5f, 0f);

        if (visual != null)
        {
            visual.localScale = new Vector3(1f, h / standHeight, 1f);
            visual.localPosition = new Vector3(0f, h * 0.5f, 0f);
        }

        if (cameraTransform != null)
        {
            Vector3 p = cameraTransform.localPosition;
            p.y = crouching ? crouchEyeHeight : standEyeHeight;
            cameraTransform.localPosition = p;
        }
    }

    private void Look()
    {
        if(!IsOwner) return;
        //input fra spillerens roation i forbindelse med Unitys Time.deltaTime
        float MouseX = lookInput.x * mouseSensitivity;
        float MouseY = lookInput.y * mouseSensitivity;

        //y-værdien af mouse-input laves om til in-game x-rotation hvilket i Unity er "op og ned"
        xRotation -= MouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //rotering af kamera og gameobject transform
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * MouseX);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    [Rpc(SendTo.Owner)]
    public void RespawnRpc(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
        verticalVelocity = 0f;
    }
}