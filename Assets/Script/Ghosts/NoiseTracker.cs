using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NoiseTracker : MonoBehaviour
{
    public static NoiseTracker Instance { get; private set; }

    public struct Noise
    {
        public Vector3 position;
        public float radius;
    }

    [Header("Noise radius (metres)")]
    [SerializeField] private float crouchNoise = 3f;
    [SerializeField] private float walkNoise = 8f;
    [SerializeField] private float runNoise = 18f;

    [Header("Speed thresholds (m/s)")]
    [SerializeField] private float movingThreshold = 0.5f;
    [SerializeField] private float runThreshold = 4.5f;

    public readonly List<Noise> Noises = new();

    private readonly Dictionary<ulong, Vector3> lastPositions = new();
    private readonly Dictionary<ulong, float> smoothedSpeeds = new();

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        Noises.Clear();

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        foreach (var client in nm.ConnectedClientsList)
        {
            var obj = client.PlayerObject;
            if (obj == null) continue;

            Vector3 pos = obj.transform.position;
            float speed = 0f;

            if (lastPositions.TryGetValue(client.ClientId, out Vector3 last))
            {
                Vector3 delta = pos - last;
                delta.y = 0f;

                if (delta.magnitude < 3f)
                    speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            }
            lastPositions[client.ClientId] = pos;

            smoothedSpeeds.TryGetValue(client.ClientId, out float previous);
            speed = Mathf.Lerp(previous, speed, 10f * Time.deltaTime);
            smoothedSpeeds[client.ClientId] = speed;

            bool crouching = obj.GetComponent<PlayerController>().IsCrouching;

            float radius = 0f;
            if (speed > movingThreshold)
                radius = crouching ? crouchNoise : (speed >= runThreshold ? runNoise : walkNoise);

            if (radius > 0f)
                Noises.Add(new Noise { position = pos, radius = radius });
        }
    }

    // In the scene window, a yellow spheres shows the noise
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        foreach (var n in Noises)
            Gizmos.DrawWireSphere(n.position, n.radius);
    }
}