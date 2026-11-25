using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum RoleType { None, Locater, TeamLeader, Worker, Seeker }

public class PlayerRole : NetworkBehaviour
{
    // The server assigns roles, only the owning player can read their role
    public NetworkVariable<RoleType> Current = new(RoleType.None, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    // Server assigns shuffled roles to all spawned players
    public static void AssignAll()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var roles = new List<RoleType>
        {
            RoleType.Locater, RoleType.TeamLeader, RoleType.Worker, RoleType.Seeker
        };

        // Shuffle the role list so assignments are random
        for (int i = roles.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (roles[i], roles[j]) = (roles[j], roles[i]);
        }

        int index = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var role = client.PlayerObject.GetComponent<PlayerRole>();
            if (role == null) continue;

            role.Current.Value = index < roles.Count ? roles[index] : RoleType.None;
            index++;
        }
    }

    // Give a mid-round joiner a role that no other player currently has
    public static void AssignUnused(PlayerRole target)
    {
        if (!NetworkManager.Singleton.IsServer || target == null) return;

        var used = new HashSet<RoleType>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var role = client.PlayerObject.GetComponent<PlayerRole>();
            if (role != null && role != target) used.Add(role.Current.Value);
        }

        foreach (RoleType candidate in new[] { RoleType.Locater, RoleType.TeamLeader, RoleType.Worker, RoleType.Seeker })
        {
            if (!used.Contains(candidate))
            {
                target.Current.Value = candidate;
                return;
            }
        }
    }

    // Just for testing
    [Rpc(SendTo.Server)]
    public void DebugSetRoleRpc(RoleType role)
    {
        Current.Value = role;
    }
}