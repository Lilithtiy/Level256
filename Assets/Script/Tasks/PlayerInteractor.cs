using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerInteractor : NetworkBehaviour
{
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private float interactRange = 3f;
    [Tooltip("How long a finished panel stays open so the success message can be read")]
    [SerializeField] private float closeDelayAfterDone = 1.2f;

    private Camera cam;
    private PlayerController player;
    private StationTask target;
    private Vector3 targetPoint;
    private StationTask working;
    private bool panelOpen;
    private float doneAt = -1f;

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>();
        player = GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner) interactAction.action.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        interactAction.action.Disable();

        var host = TaskPanelHost.Instance;
        if (host != null)
        {
            host.ClosePanel();
            host.HidePrompt();
            host.HideHold();
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        var host = TaskPanelHost.Instance;
        if (host == null) return;

        bool canInteract = GameManager.Instance != null &&
                           GameManager.Instance.State.Value == GameState.Playing &&
                           !player.IsCaught;

        // A panel is open, so we only need to check whether it should close
        if (panelOpen)
        {
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool toggle = interactAction.action.WasPressedThisFrame();

            if (!canInteract || escape || toggle || working == null ||
                !working.IsActive.Value || !working.InRange(transform.position))
            {
                ClosePanel();
                return;
            }

            if (working.IsDone.Value)
            {
                if (doneAt < 0f) doneAt = Time.time;
                if (Time.time - doneAt >= closeDelayAfterDone) ClosePanel();
            }
            return;
        }

        target = canInteract ? FindTarget() : null;

        // Holding a task, keep going while we look at it and hold the button
        if (working != null)
        {
            if (target != working || !interactAction.action.IsPressed())
            {
                StopWorking();
                host.HideHold();
            }
            else
            {
                host.HidePrompt();
                host.ShowHold(working.HoldLabel, working.Progress.Value);
                return;
            }
        }

        if (target == null)
        {
            host.HidePrompt();
            host.HideHold();
            return;
        }

        host.HideHold();
        string prompt = target.Prompt;
        if (target.Workers.Value > 0) prompt += $"   ({target.Workers.Value} working)";
        host.ShowPrompt(prompt);

        switch (target.Mode)
        {
            case TaskMode.Hold:
                if (interactAction.action.IsPressed()) StartWorking(target);
                break;
            case TaskMode.Panel:
                if (interactAction.action.WasPressedThisFrame()) OpenPanel(target);
                break;
            case TaskMode.Press:
                if (interactAction.action.WasPressedThisFrame()) target.InteractRpc();
                break;
        }
    }

    private StationTask FindTarget()
    {
        var ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var task = hit.collider.GetComponentInParent<StationTask>();
            if (task != null && task.IsSpawned && task.IsActive.Value && !task.IsDone.Value)
            {
                targetPoint = hit.point;
                return task;
            }
        }
        return null;
    }

    // Starting and stopping
    private void StartWorking(StationTask task)
    {
        working = task;
        task.SetWorkingRpc(true);
    }

    private void StopWorking()
    {
        if (working != null && working.IsSpawned) working.SetWorkingRpc(false);
        working = null;
    }

    private void OpenPanel(StationTask task)
    {
        var host = TaskPanelHost.Instance;

        if (!host.OpenPanel(task)) return;

        working = task;
        panelOpen = true;
        doneAt = -1f;
        task.SetWorkingRpc(true);

        host.HidePrompt();
        host.HideHold();
        player.SetInputLocked(true);
    }

    private void ClosePanel()
    {
        panelOpen = false;
        StopWorking();

        if (TaskPanelHost.Instance != null) TaskPanelHost.Instance.ClosePanel();
        player.SetInputLocked(false);
    }
}