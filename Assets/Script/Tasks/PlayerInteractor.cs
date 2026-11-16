using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerInteractor : NetworkBehaviour
{
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private float interactRange = 3f;

    private Camera cam;
    private PlayerController player;
    private StationTask target;    // the task we're looking at
    private StationTask working;   // the task we're working on (holding or in the panel)
    private bool panelOpen;

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
        if (IsOwner) interactAction.action.Disable();
    }

    private void Update()
    {
        if (!IsOwner) return;

        bool canInteract = GameManager.Instance != null &&
                           GameManager.Instance.State.Value == GameState.Playing &&
                           !player.IsCaught;

        // A panel is open: we only check whether it should close
        if (panelOpen)
        {
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (!canInteract || escape || working == null || working.IsDone.Value || !working.IsActive.Value)
                ClosePanel();
            return;
        }

        target = canInteract ? FindTarget() : null;

        // Holding a task: keep going while we look at it and hold the button
        if (working != null)
        {
            if (target != working || !interactAction.action.IsPressed())
                StopWorking();
            return;
        }

        if (target == null) return;

        if (target.Mode == TaskMode.Hold)
        {
            if (interactAction.action.IsPressed()) StartWorking(target);
        }
        else if (interactAction.action.WasPressedThisFrame())
        {
            OpenPanel(target);
        }
    }

    private StationTask FindTarget()
    {
        var ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var task = hit.collider.GetComponentInParent<StationTask>();
            if (task != null && task.IsActive.Value && !task.IsDone.Value) return task;
        }
        return null;
    }

    // ---------- Starting and stopping ----------

    private void StartWorking(StationTask task)
    {
        working = task;
        task.SetWorkingRpc(true);
    }

    private void StopWorking()
    {
        if (working != null) working.SetWorkingRpc(false);
        working = null;
    }

    private void OpenPanel(StationTask task)
    {
        working = task;
        panelOpen = true;
        task.SetWorkingRpc(true);
        task.OnPanelOpened();
        player.SetInputLocked(true);   // frozen, and the mouse is free
    }

    private void ClosePanel()
    {
        panelOpen = false;
        StopWorking();
        player.SetInputLocked(false);
    }

    // ---------- UI ----------

    private void OnGUI()
    {
        if (!IsOwner) return;

        if (panelOpen && working != null)
        {
            DrawPanelWindow();
            return;
        }

        if (target == null || Event.current.type != EventType.Repaint) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };

        string text = working != null
            ? $"{target.TaskName}...  {target.Progress.Value * 100f:0}%"
            : target.Prompt;

        if (target.Workers.Value > 0)
            text += $"   ({target.Workers.Value} working)";

        GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 40), text, style);
    }

    private void DrawPanelWindow()
    {
        // Dim the world
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        Rect window = new Rect(Screen.width / 2f - 340, Screen.height / 2f - 230, 680, 460);
        GUI.Box(window, working.TaskName);

        working.DrawPanel(new Rect(window.x + 20, window.y + 50, window.width - 40, window.height - 140));

        // Progress shared by everyone working on this task
        Rect bar = new Rect(window.x + 20, window.yMax - 70, window.width - 40, 20);
        GUI.color = Color.gray;
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = Color.green;
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * working.Progress.Value, bar.height), Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(new Rect(window.x + 20, window.yMax - 40, 300, 30), $"Working here: {working.Workers.Value}");

        if (GUI.Button(new Rect(window.xMax - 160, window.yMax - 45, 140, 32), "Close (Esc)"))
            ClosePanel();
    }
}