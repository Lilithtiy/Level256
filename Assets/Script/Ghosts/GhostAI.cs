using System.Collections.Generic;
using Unity.Netcode;
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

    [Header("Movement")]
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float detectRange = 15f;
    [SerializeField] private float wanderRadius = 12f;
    [SerializeField] private float repathInterval = 0.25f;

    [Header("Catching")]
    [SerializeField] private float catchDistance = 1.8f;
    [SerializeField] private float stunSeconds = 3f;

    private NavMeshAgent agent;
    private Vector3 homePosition;
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
        if (IsServer) allGhosts.Remove(this);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.State.Value == GameState.GameOver)
        {
            agent.isStopped = true;
            return;
        }

        if (Time.time < stunnedUntil) return;
        if (agent.isStopped) agent.isStopped = false;

        if (Time.time < nextRepath) return;
        nextRepath = Time.time + repathInterval;

        Transform target = FindNearestPlayer(out float distance, out ulong clientId);

        if (target != null && distance <= catchDistance && GameManager.Instance.TryCatchPlayer(clientId, transform.position))
        {
            stunnedUntil = Time.time + stunSeconds;
            agent.ResetPath();
            agent.isStopped = true;
            return;
        }

        if (target != null && distance <= detectRange)
            agent.SetDestination(GetChaseDestination(target, distance));
        else if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance < 0.5f))
            Wander();
    }

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

    private Vector3 SnapToNavMesh(Vector3 position, Vector3 fallback)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas) ? hit.position : fallback;
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

    public static void ResetAll()
    {
        foreach (var ghost in allGhosts) ghost.ResetGhost();
    }

    private void ResetGhost()
    {
        stunnedUntil = 0f;
        agent.ResetPath();
        agent.Warp(homePosition);
        agent.isStopped = false;
    }
}