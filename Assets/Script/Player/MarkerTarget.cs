using System.Collections.Generic;
using UnityEngine;

public enum MarkerType { Ghost, Player, Task, Pellet }

public class MarkerTarget : MonoBehaviour
{
    public static readonly List<MarkerTarget> All = new();

    [SerializeField] private MarkerType type;
    public MarkerType Type => type;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);
}