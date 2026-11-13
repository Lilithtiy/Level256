using Unity.Netcode;
using UnityEngine;

public class GhostSpawner : MonoBehaviour
{
    public static GhostSpawner Instance { get; private set; }

    [System.Serializable]
    public class GhostSpawn
    {
        public GameObject prefab;
        public Transform point;
    }

    [SerializeField] private GhostSpawn[] ghosts;

    private void Awake()
    {
        Instance = this;
    }

    // SERVER ONLY
    public void SpawnGhosts()
    {
        int count = 0;

        foreach (var g in ghosts)
        {
            if (g.prefab == null || g.point == null)
            {
                Debug.LogError("GhostSpawner: a Ghosts entry is missing its Prefab or Point.");
                continue;
            }

            var ghost = Instantiate(g.prefab, g.point.position, g.point.rotation);
            ghost.GetComponent<NetworkObject>().Spawn();
            count++;
        }

        Debug.Log($"GhostSpawner: spawned {count} ghosts");
    }
}