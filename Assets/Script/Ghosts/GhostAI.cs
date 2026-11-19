using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public enum GhostPersonality { Chaser, Ambusher, Flanker, Shy }

[RequireComponent(typeof(NavMeshAgent))]
public class GhostAI : NetworkBehaviour
{
    private static readonly List<GhostAI> allGhosts = new();

    [Header("Personality")]
    [SerializeField] private GhostPersonality personality = GhostPersonality.Chaser;
    [SerializeField] private float aheadDistance = 6f;
    [SerializeField] private float shyDistance = 10f;

    [Header("Senses")]
    [SerializeField] private float detectRange = 15f;
    [SerializeField] private float eyeHeight = 1.5f;
    [SerializeField] private float hearingMultiplier = 1f;
    [SerializeField] private float wallMuffle = 0.5f;
    [SerializeField] private float investigateSeconds = 6f;
    [SerializeField] private float searchRadius = 5f;

    [Header("Movement")]
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float wanderRadius = 12f;
    [SerializeField] private float repathInterval = 0.25f;

    [Header("Catching")]
    [SerializeField] private float catchDistance = 1.8f;
    [SerializeField] private float stunSeconds = 3f;

    [Header("Fleeing")]
    [SerializeField] private float fleeDistance = 10f;
    [SerializeField] private float fleeRepathInterval = 0.5f;
    private float nextFleeRepath;
    [SerializeField] private ScriptableAudio ghostDeathAudio;

    [Header("Respawning")]
    [SerializeField] private float ghostRespawnDelay = 5f;
    [SerializeField] private GameObject ghostPrefab;

    private NavMeshAgent agent;
    private Vector3 homePosition;
    private Vector3 investigatePos;
    private float investigateUntil;
    private float nextRepath;
    private float stunnedUntil;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public override void OnNetworkSpawn()
    {

        if (!IsServer)
        {
            agent.enabled = false;
            enabled = false;
            return;
        }

        agent.speed = speed;
        homePosition = transform.position;
        allGhosts.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            allGhosts.Remove(this);

        }
    }

    public void SetRespawnPrefab(GameObject prefab)
    {
        ghostPrefab = prefab;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // if it game over or escaped, every ghost stands still, in every state
        if (gm.State.Value != GameState.Playing)
        {
            agent.isStopped = true;
            return;
        }

        // Normal state
        if (gm.ghostState.Value == GhostState.Normal)
        {
            if (Time.time < stunnedUntil) return;
            if (agent.isStopped) agent.isStopped = false;

            if (Time.time < nextRepath) return;
            nextRepath = Time.time + repathInterval;

            Transform target = FindNearestPlayer(out float distance, out ulong clientId);
            bool sees = target != null && distance <= detectRange && HasLineOfSight(target.position);

            // 1: catch
            if (sees && distance <= catchDistance &&
                gm.TryCatchPlayer(clientId, transform.position))
            {
                stunnedUntil = Time.time + stunSeconds;
                investigateUntil = 0f;
                agent.ResetPath();
                agent.isStopped = true;
                return;
            }

            // 2: sight: chase with this ghost's personality
            if (sees)
            {
                investigateUntil = 0f;
                agent.SetDestination(GetChaseDestination(target, distance));
            }
            // 3: hearing: go to where the noise came from
            else if (TryHearNoise(out Vector3 heardPos))
            {
                investigatePos = heardPos;
                investigateUntil = Time.time + investigateSeconds;
                agent.SetDestination(SnapToNavMesh(heardPos, heardPos));
            }
            // 4: reached the last heard spot: search the area for a while
            else if (Time.time < investigateUntil)
            {
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                    SearchAround(investigatePos);
            }
            // 5: nothing going on: wander
            else if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance < 0.5f))
            {
                Wander();
            }
        }
        // Exposed state
        else
        {
            if (agent.isStopped) agent.isStopped = false;

            Transform nearest = FindNearestPlayer(out float d, out _);

            if (nearest != null && d <= catchDistance)
            {
                EatGhost();
                return;
            }

            if (Time.time < nextFleeRepath) return;
            nextFleeRepath = Time.time + fleeRepathInterval;

            if (nearest != null) FleeFromPlayer(nearest.position);
        }
    }

    private void EatGhost()
    {
        if (!NetworkObject.IsSpawned) return;

        NetworkAudioManager.Instance.PlayNetworked3D(ghostDeathAudio, transform.position);
        GhostSpawner.Instance.RespawnGhost(ghostPrefab, ghostRespawnDelay);
        NetworkObject.Despawn(true);
    }

    // Fleeing
    public void FleeFromPlayer(Vector3 playerPosition)
    {
        Vector3 directionAway = transform.position - playerPosition;
        directionAway.y = 0f;

        directionAway.Normalize();

        Vector3 fleePosition = transform.position + directionAway * fleeDistance;

        Vector3 destination = SnapToNavMesh(fleePosition, transform.position);

        agent.SetDestination(destination);
    }

    // Hearing
    private bool TryHearNoise(out Vector3 heardPos)
    {
        heardPos = default;
        var tracker = NoiseTracker.Instance;
        if (tracker == null) return false;

        bool heard = false;
        float bestScore = 0f;

        foreach (var noise in tracker.Noises)
        {
            Vector3 offset = noise.position - transform.position;
            offset.y = 0f;
            float dist = offset.magnitude;

            float radius = noise.radius * hearingMultiplier;
            if (!HasLineOfSight(noise.position)) radius *= wallMuffle;
            if (dist > radius) continue;

            // if its closer relative to the noise radius = clearer sound
            float score = 1f - dist / radius;
            if (score > bestScore)
            {
                bestScore = score;
                heardPos = noise.position;
                heard = true;
            }
        }
        return heard;
    }

    private bool HasLineOfSight(Vector3 targetPos)
    {
        Vector3 from = transform.position + Vector3.up * eyeHeight;
        Vector3 to = targetPos + Vector3.up * 1f;

        if (Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<PlayerController>() != null; // blocked unless we hit a player

        return true;
    }

    // Personalities for the sweet little ghosts
    private Vector3 GetChaseDestination(Transform player, float distance)
    {
        switch (personality)
        {
            case GhostPersonality.Ambusher:
            {
                if (distance < aheadDistance) return player.position;
                return SnapToNavMesh(player.position + player.forward * aheadDistance, player.position);
            }

            case GhostPersonality.Flanker:
            {
                GhostAI chaser = FindChaser();
                if (chaser == null || distance < aheadDistance) return player.position;

                Vector3 pivot = player.position + player.forward * (aheadDistance * 0.5f);
                Vector3 target = pivot + (pivot - chaser.transform.position);
                return SnapToNavMesh(target, player.position);
            }

            case GhostPersonality.Shy:
                return distance > shyDistance ? player.position : homePosition;

            default:
                return player.position;
        }
    }

    private GhostAI FindChaser()
    {
        foreach (var ghost in allGhosts)
            if (ghost != this && ghost.personality == GhostPersonality.Chaser)
                return ghost;
        return null;
    }

    // Helpers

    private Vector3 SnapToNavMesh(Vector3 position, Vector3 fallback)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas)
            ? hit.position
            : fallback;
    }

    private Transform FindNearestPlayer(out float nearestDistance, out ulong nearestClientId)
    {
        Transform nearest = null;
        nearestDistance = float.MaxValue;
        nearestClientId = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            Vector3 offset = client.PlayerObject.transform.position - transform.position;
            offset.y = 0f;
            float d = offset.magnitude;

            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearest = client.PlayerObject.transform;
                nearestClientId = client.ClientId;
            }
        }
        return nearest;
    }

    private void Wander()
    {
        Vector3 random = transform.position + Random.insideUnitSphere * wanderRadius;
        if (NavMesh.SamplePosition(random, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    private void SearchAround(Vector3 center)
    {
        Vector3 random = center + Random.insideUnitSphere * searchRadius;
        if (NavMesh.SamplePosition(random, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    // Restart

    public static void ResetAll()
    {
        foreach (var ghost in allGhosts) ghost.ResetGhost();
    }

    public void ResetGhost()
    {
        stunnedUntil = 0f;
        investigateUntil = 0f;
        agent.ResetPath();
        agent.Warp(homePosition);
        agent.isStopped = false;
    }
}