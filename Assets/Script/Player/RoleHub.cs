using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerRole))]
public class RoleHud : MonoBehaviour
{
    [SerializeField] private int maxPelletMarkers = 6;
    [SerializeField] private float bannerSeconds = 5f;
    [SerializeField] private float edgeMargin = 40f;

    private PlayerRole role;
    private Camera cam;
    private RoleType lastRole = RoleType.None;
    private float bannerUntil;
    private readonly List<MarkerTarget> targets = new();

    private void Awake()
    {
        role = GetComponent<PlayerRole>();
        cam = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (!role.IsOwner) return;

        // Show the big role banner whenever the role changes
        RoleType current = role.Current.Value;
        if (current != lastRole)
        {
            lastRole = current;
            bannerUntil = Time.time + bannerSeconds;
        }

        // Just temporary for test keys
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f2Key.wasPressedThisFrame) role.DebugSetRoleRpc(RoleType.Locater);
        if (kb.f3Key.wasPressedThisFrame) role.DebugSetRoleRpc(RoleType.TeamLeader);
        if (kb.f4Key.wasPressedThisFrame) role.DebugSetRoleRpc(RoleType.Worker);
        if (kb.f5Key.wasPressedThisFrame) role.DebugSetRoleRpc(RoleType.Seeker);
    }

    private void OnGUI()
    {
        if (!role.IsOwner || cam == null) return;
        if (Event.current.type != EventType.Repaint) return;

        RoleType current = role.Current.Value;
        if (current == RoleType.None) return;

        if (GameManager.Instance != null && GameManager.Instance.State.Value != GameState.Playing) return;

        DrawRoleLabel(current);

        MarkerType wanted = GetMarkerType(current);
        CollectTargets(wanted);

        foreach (var target in targets)
            DrawMarker(target, wanted);
    }

    // What each role sees

    private static MarkerType GetMarkerType(RoleType role) => role switch
    {
        RoleType.Locater => MarkerType.Ghost,
        RoleType.TeamLeader => MarkerType.Player,
        RoleType.Worker => MarkerType.Task,
        _ => MarkerType.Pellet // Seeker
    };

    private static string GetTitle(RoleType role) => role switch
    {
        RoleType.Locater => "LOCATER",
        RoleType.TeamLeader => "TEAM LEADER",
        RoleType.Worker => "WORKER",
        _ => "SEEKER"
    };

    private static string GetDescription(RoleType role) => role switch
    {
        RoleType.Locater => "You can see where the ghosts are.",
        RoleType.TeamLeader => "You can see where all the players are.",
        RoleType.Worker => "You can see where the tasks are.",
        _ => "You can see where the energy pellets are."
    };

    private static Color GetColor(MarkerType type) => type switch
    {
        MarkerType.Ghost => Color.red,
        MarkerType.Player => Color.green,
        MarkerType.Task => Color.yellow,
        _ => new Color(1f, 0.6f, 0f)
    };

    // Collecting and drawing

    private void CollectTargets(MarkerType wanted)
    {
        targets.Clear();

        foreach (var t in MarkerTarget.All)
        {
            if (t.Type != wanted) continue;

            // Don't mark yourself
            if (wanted == MarkerType.Player &&
                t.TryGetComponent(out NetworkObject netObj) && netObj.IsOwner) continue;

            targets.Add(t);
        }

        if (wanted == MarkerType.Pellet && targets.Count > maxPelletMarkers)
        {
            Vector3 me = cam.transform.position;
            targets.Sort((a, b) =>
                (a.transform.position - me).sqrMagnitude.CompareTo((b.transform.position - me).sqrMagnitude));
            targets.RemoveRange(maxPelletMarkers, targets.Count - maxPelletMarkers);
        }
    }

    private void DrawMarker(MarkerTarget target, MarkerType type)
    {
        Vector3 world = target.transform.position + Vector3.up;
        Vector3 sp = cam.WorldToScreenPoint(world);
        bool behind = sp.z < 0f;

        Vector2 pos = new Vector2(sp.x, sp.y);
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;

        bool onScreen = !behind &&
                        sp.x > edgeMargin && sp.x < Screen.width - edgeMargin &&
                        sp.y > edgeMargin && sp.y < Screen.height - edgeMargin;

        if (!onScreen)
        {
            // Put the marker on the screen edge, so pointing toward the target
            Vector2 dir = pos - center;
            if (behind) dir = -dir;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;

            float sx = (center.x - edgeMargin) / Mathf.Max(Mathf.Abs(dir.x), 0.001f);
            float sy = (center.y - edgeMargin) / Mathf.Max(Mathf.Abs(dir.y), 0.001f);
            pos = center + dir * Mathf.Min(sx, sy);
        }

        float distance = Vector3.Distance(cam.transform.position, target.transform.position);
 
        float x = pos.x;
        float y = Screen.height - pos.y;

        Color old = GUI.color;
        GUI.color = GetColor(type);
        GUI.DrawTexture(new Rect(x - 8, y - 8, 16, 16), Texture2D.whiteTexture);
        GUI.color = old;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter };
        style.normal.textColor = GetColor(type);
        GUI.Label(new Rect(x - 40, y + 8, 80, 20), $"{distance:0} m", style);
    }

    private void DrawRoleLabel(RoleType current)
    {
        var small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        GUI.Label(new Rect(10, Screen.height - 34, 400, 28), $"Role: {GetTitle(current)}", small);

        if (Time.time >= bannerUntil) return;

        float fade = Mathf.Clamp01((bannerUntil - Time.time) / 1.5f);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fade);

        var title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 56,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        var sub = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };

        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 80), GetTitle(current), title);
        GUI.Label(new Rect(0, Screen.height * 0.18f + 70, Screen.width, 40), GetDescription(current), sub);
        GUI.Label(new Rect(0, Screen.height * 0.18f + 105, Screen.width, 40), "Tell your team what you know!", sub);

        GUI.color = old;
    }
}