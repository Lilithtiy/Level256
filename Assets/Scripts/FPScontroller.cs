using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;


public class FPScontroller : MonoBehaviour
{
    /*variabler*/
    [SerializeField] private float moveSpeed;
    [SerializeField] private float regularMoveSpeed = 2f;
    [SerializeField] private float sprintMoveSpeed = 2.5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpHeight = 0.75f;

    //gravity
    private float verticalVelocity;

    //component reference
    private CharacterController characterController;
    public Transform cameraTransform;

    //Input-variabler, der skal indeholde spillerens inputs
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isSprinting;
    private float xRotation = 0f;

    //public UiManager uiManager;

    //footstep sounds
    public AudioSource footStepSource;
    public AudioClip[] footstepClips;

    private float walkStepInterval = 0.5f;
    private float sprintStepInterval = 0.35f;
    private float stepTimer;


    private void Start()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2f);
        //uiManager = GameObject.FindGameObjectWithTag("UImanager").GetComponent<UiManager>();
    }

    //Konfigurationer ligeså snart spillet starter
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        moveSpeed = regularMoveSpeed;
    }


    //OnMove-, OnLook-, OnSprint og OnJump-event der kaldes ved input fra spilleren
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        
        isSprinting = context.ReadValueAsButton();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        if (!characterController.isGrounded)
        {
            return;
        }

        Jump();
    }

    public void OnSettings(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        Settings();
    }

    //Selve funktionaliteten af MovePlayer og LookPlayer, der konstant køres for at opdatere hvor spilleren skal bevæges hen/kigge hen i relation til deres inputs
    private void Update()
    {
        ApplyGravity();
        Move();
        Look();
        Sprint();
    }

    //gravity module, der tilfører en nedadgående kraft (tyngdekraft) på spilleren
    private void ApplyGravity()
    {
        if (characterController != null && characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += Physics.gravity.y * Time.deltaTime;
    }

    private void Move()
    {
        //konverterer Vector2 værdier til Vector3. moveInput.x (A og D) og moveInput.y (WS) 
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        //spillerens retning i relation til deres input og hastigheds-variabel
        Vector3 velocity = move * moveSpeed;

        velocity.y += verticalVelocity;

        //oversætter spillerens verticalVelocity til reel bevægelse in-game via komponentets metode "MovePlayer()"
        characterController.Move(velocity * Time.deltaTime);

        //footstep sound
        if (!characterController.isGrounded)
        {
            return;
        }

        Vector3 horizontalVelocity = velocity;
        horizontalVelocity.y = 0f;

        //bevæger spilleren sig?
        if(horizontalVelocity.magnitude < 0.1f)
        {
            return;
        }

        stepTimer -= Time.deltaTime;

        if(stepTimer <= 0f)
        {
            PlayFootstep();

            if (isSprinting)
            {
                stepTimer = sprintStepInterval;
            }
            else if (!isSprinting)
            {
                stepTimer = walkStepInterval;
            }
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

    private void Sprint()
    {
        if (isSprinting) 
        {
            moveSpeed = sprintMoveSpeed;
        }
        else if (!isSprinting)
        {
            moveSpeed = regularMoveSpeed;
        }
    }

    private void Jump()
    {
        if (characterController.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
        }
    }

    public void SetSensitivity(float playerSensitivity)
    {
        mouseSensitivity = playerSensitivity;
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
    }

    private void Settings()
    {
        //uiManager.ShowSettings();
    }

    private void PlayFootstep()
    {
        if (footstepClips == null || footStepSource == null || footstepClips.Length == 0) //uiManager.settingsCanvas.enabled == true)
        {
            return;
        }

        AudioClip clip = footstepClips[Random.Range(0,footstepClips.Length)];

        footStepSource.pitch = Random.Range(0.95f, 1.05f);
        footStepSource.volume = isSprinting ? Random.Range(0.9f, 1f) : Random.Range(0.7f, 0.8f);

        footStepSource.PlayOneShot(clip);
    }
}
