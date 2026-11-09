using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PickUpSystem : MonoBehaviour
{
    //Raycast
    private static float pickupMaxDistance = 2f;
    public LayerMask pickupMask;
    
    //item
    public GameObject item;
    public Transform pickupPoint;

    private Camera camera;

    private void Awake()
    {
        camera = GetComponentInChildren<Camera>();
    }

    //UI
    //public UiManager UIManager;

    private void Update()
    {
        if (Physics.Raycast(camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit, pickupMaxDistance, pickupMask))
        {
            //UIManager.ShowInteractableText();
        }
        else
        {
            //UIManager.HideInteractableText();
        }
    }

    //event der tjekker om spilleren forsøger at samle et object op (I dette tilfælde ved at trykke på "E")
    public void OnPickup(InputAction.CallbackContext context)
    {
        // reager kun når knappen faktisk er trykket
        if (!context.performed)
        {
            return;
        }

        // saml ikke noget op hvis man allerede holder et item
        if (item != null)
        {
            return;
        }

        //hvis der intet kamera er
        if (camera == null)
        {
            Debug.LogError("No camera found!");
            return;
        }

        //raycast-check
        if (Physics.Raycast(camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit, pickupMaxDistance, pickupMask))
        {
            item = hit.collider.gameObject;
            PickUpItem();
        }
    }

    //event der tjekker om spilleren forsøger at smide et object (ved at trykke på "Q")
    public void OnItemDrop(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        if (item == null)
        {
            return;
        }

        DropItem();
        //UIManager.HideqText();
    }


    //funktionalitet for at samle et objekt op
    public void PickUpItem()
    {
        //fjerner physics fra object
        Rigidbody rb = item.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        //sætter objektets parent til pickupPoint transform defineret i inspector, samt nulstiller lokal position og rotation
        item.transform.SetParent(pickupPoint);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.Euler(0f,0f,90f);
        item.transform.localScale = gameObject.GetComponentInParent<Transform>().localScale;

        //UIManager.ShowKeyText();
    }

    public void DropItem()
    {
        Rigidbody rb = item.GetComponent<Rigidbody>();

        item.transform.SetParent(null);
        item.transform.localRotation = Quaternion.identity;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        item = null;
    }
}
