using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

/// <summary>
/// Spawns scene GameStateManager on server start (Host and dedicated server).
/// </summary>
public class GameStateManagerSpawner : MonoBehaviour
{
    private void Start()
    {
        if (InstanceFinder.NetworkManager == null)
            return;

        InstanceFinder.ServerManager.OnServerConnectionState += OnServerConnectionState;

        if (InstanceFinder.IsServerStarted)
            TrySpawnGameStateManager();
    }

    private void OnDestroy()
    {
        if (InstanceFinder.NetworkManager != null)
            InstanceFinder.ServerManager.OnServerConnectionState -= OnServerConnectionState;
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
            TrySpawnGameStateManager();
    }

    private static void TrySpawnGameStateManager()
    {
        GameStateManager existing = FindFirstObjectByType<GameStateManager>(FindObjectsInactive.Include);
        if (existing == null)
        {
            Debug.LogWarning("[GameStateManagerSpawner] GameStateManager was not found in the scene.");
            return;
        }

        NetworkObject networkObject = existing.NetworkObject;
        if (networkObject == null)
        {
            Debug.LogError("[GameStateManagerSpawner] GameStateManager is missing NetworkObject.", existing);
            return;
        }

        if (networkObject.IsSpawned)
            return;

        InstanceFinder.ServerManager.Spawn(networkObject);
        Debug.Log("[GameStateManagerSpawner] GameStateManager spawned on server.");
    }
}
