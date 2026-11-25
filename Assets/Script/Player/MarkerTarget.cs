using System.Collections.Generic;
using UnityEngine;

public enum MarkerType { Ghost, Player, Task, Pellet }

public class MarkerTarget : MonoBehaviour
{
    // Active marker targets, used by HUDs to find objects to mark
    public static readonly List<MarkerTarget> All = new();

    [SerializeField] private MarkerType type;
    public MarkerType Type => type;

    // Keep the shared list in sync with this object's active state
    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);
}