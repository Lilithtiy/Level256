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

    [Header("Camera")]
    [SerializeField] private float mouseSensitivity = 2f;
    public Transform cameraTransform;
    private Vector2 lookInput;
    private float xRotation = 0f;

    private readonly NetworkVariable<bool> isCrouching = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnNetworkSpawn()
    {
        isCrouching.OnValueChanged += OnCrouchChanged;
        ApplyCrouch(isCrouching.Value);

        if (!IsOwner) return;

        moveAction.action.Enable();
        jumpAction.action.Enable();
        crouchAction.action.Enable();
        sprintAction.action.Enable();
        lookAction.action.Enable();

        controller.enabled = false;
        transform.position = new Vector3(Random.Range(-3f, 3f), 0.1f, Random.Range(-3f, 3f));
        controller.enabled = true;
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
        Vector3 move = new Vector3(input.x, 0f, input.y) * speed;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);

        lookInput = lookAction.action.ReadValue<Vector2>();
        Look();
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
    }

    private void Look()
    {
        //input fra spillerens roation i forbindelse med Unitys Time.deltaTime
        float MouseX = lookInput.x * mouseSensitivity * 0.75f * Time.deltaTime;
        float MouseY = lookInput.y * mouseSensitivity * 0.75f * Time.deltaTime;

        //y-værdien af mouse-input laves om til in-game x-rotation hvilket i Unity er "op og ned"
        xRotation -= MouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //rotering af kamera og gameobject transform
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * MouseX);
    }
}