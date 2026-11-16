using Unity.Netcode;
using UnityEngine;
using System.Collections;
using UnityEngine.AI;

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
            ghost.GetComponent<GhostAI>().SetRespawnPrefab(g.prefab);
            ghost.GetComponent<NetworkObject>().Spawn();
            count++;
        }

        Debug.Log($"GhostSpawner: spawned {count} ghosts");
    }

    public void RespawnGhost(GameObject ghost, float delay)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        StartCoroutine(RespawnGhostRoutine(ghost, delay));
    }

    private IEnumerator RespawnGhostRoutine(GameObject ghost, float delay)
    {
        yield return new WaitForSeconds(delay);
        Transform spawnPoint = FindFurthestSpawnPoint();

        if (ghost == null || spawnPoint == null) yield break;

        // Set the ghost's spawn position to the spawn point
        Vector3 spawnPosition = spawnPoint.position;

        // Check if the spawn position is close to the NavMesh
        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            spawnPosition = hit.position;
        else
        {
            Debug.LogError("GhostSpawner: Spawn point is not close to the NavMesh.");
            yield break;
        }

        // Respawn the ghost
        GameObject newGhost = Instantiate(ghost, spawnPosition, spawnPoint.rotation);

        newGhost.GetComponent<GhostAI>().SetRespawnPrefab(ghost);

        newGhost.GetComponent<NetworkObject>().Spawn();

        Debug.Log($"GhostSpawner: Respawned ghost at {spawnPosition}");
    }

    private Transform FindFurthestSpawnPoint()
    {
        Transform bestPoint = null;
        float bestDistance = -1f;

        foreach (var g in ghosts)
        {
            if (g.point == null) continue;

            float closestPlayerDistance = float.MaxValue;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;

                Vector3 offset = client.PlayerObject.transform.position - g.point.position;
                offset.y = 0f;

                float distance = offset.sqrMagnitude;

                if (distance < closestPlayerDistance)
                    closestPlayerDistance = distance;
            }

            // Select the spawn point with the greatest distance
            // from its nearest player.
            if (closestPlayerDistance > bestDistance)
            {
                bestDistance = closestPlayerDistance;
                bestPoint = g.point;
            }
        }
        return bestPoint;
    }
}