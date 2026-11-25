using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class GhostSpawner : MonoBehaviour
{
    public static GhostSpawner Instance { get; private set; }

    [System.Serializable]
    public class GhostSpawn
    {
        public GameObject prefab;
        public Transform point;
        [Range(0f, 1f)] public float spawnAtProgress = 0f; // 0.10 = spawns at 10% progress
    }

    [SerializeField] private GhostSpawn[] ghosts;

    [Tooltip("If a player is closer than this to a spawn point, the ghost spawns at the point furthest from the players instead")]
    [SerializeField] private float minSpawnDistance = 15f;

    private bool[] spawned;
    private bool roundActive;

    private void Awake()
    {
        Instance = this;
        spawned = new bool[ghosts.Length];
    }

    // Reset spawn tracking and spawn any ghosts due at the start of the round
    public void StartRound()
    {
        spawned = new bool[ghosts.Length];
        roundActive = true;
        SpawnDue(0f);
    }

    private void Update()
    {
        if (!roundActive) return;

        // Only the server controls networked ghost spawning
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var gm = GameManager.Instance;
        var objectives = ObjectiveManager.Instance;
        if (gm == null || gm.State.Value != GameState.Playing) return;
        if (objectives == null || !objectives.IsSpawned) return;

        SpawnDue(objectives.Progress.Value);
    }

    // Spawn each ghost once its configured objective progress threshold is reached
    private void SpawnDue(float progress)
    {
        for (int i = 0; i < ghosts.Length; i++)
        {
            if (spawned[i] || progress < ghosts[i].spawnAtProgress) continue;

            spawned[i] = true;
            SpawnOne(ghosts[i]);
        }
    }

    private void SpawnOne(GhostSpawn g)
    {
        if (g.prefab == null || g.point == null)
        {
            Debug.LogError("GhostSpawner: a Ghosts entry is missing its Prefab or Point.");
            return;
        }

        // Avoid spawning near players when another spawn point is farther away
        Transform point = g.point;
        if (PlayerWithin(point.position, minSpawnDistance))
        {
            Transform far = FindFurthestSpawnPoint();
            if (far != null) point = far;
        }

        var ghost = Instantiate(g.prefab, point.position, point.rotation);
        ghost.GetComponent<GhostAI>().SetRespawnPrefab(g.prefab);
        ghost.GetComponent<NetworkObject>().Spawn();

        Debug.Log($"GhostSpawner: spawned {g.prefab.name} (threshold {g.spawnAtProgress:P0})");
    }

    private bool PlayerWithin(Vector3 position, float distance)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            Vector3 offset = client.PlayerObject.transform.position - position;
            offset.y = 0f;
            if (offset.magnitude < distance) return true;
        }
        return false;
    }

    // Respawning eaten ghosts
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

    // Choose the spawn point whose nearest player is farthest away
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

            if (closestPlayerDistance > bestDistance)
            {
                bestDistance = closestPlayerDistance;
                bestPoint = g.point;
            }
        }
        return bestPoint;
    }
}