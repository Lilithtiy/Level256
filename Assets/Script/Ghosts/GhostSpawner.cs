using Unity.Netcode;
using UnityEngine;

public class GhostSpawner : MonoBehaviour
{
    [System.Serializable]
    public class GhostSpawn
    {
        public GameObject prefab;
        public Transform point;
    }

    [SerializeField] private GhostSpawn[] ghosts;

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += SpawnGhosts;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= SpawnGhosts;
    }

    private void SpawnGhosts()
    {
        foreach (var g in ghosts)
        {
            var ghost = Instantiate(g.prefab, g.point.position, g.point.rotation);
            ghost.GetComponent<NetworkObject>().Spawn();
        }
    }
}