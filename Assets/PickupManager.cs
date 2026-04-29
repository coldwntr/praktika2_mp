using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class PickupManager : MonoBehaviour
{
    [SerializeField] private HealthPickup _pickupPrefab;
    [SerializeField] private PickupSpawnPoint[] _spawnPoints;
    [SerializeField] private float _respawnDelay = 10f;

    private readonly Dictionary<int, NetworkObject> _activePickups = new Dictionary<int, NetworkObject>();
    private bool _initialized;

    private void Start()
    {
        if (InstanceFinder.NetworkManager == null)
        {
            Debug.LogWarning("PickupManager could not find FishNet NetworkManager.");
            return;
        }

        InstanceFinder.ServerManager.OnServerConnectionState += HandleServerConnectionState;

        if (InstanceFinder.IsServerStarted)
        {
            HandleServerStarted();
        }
    }

    private void OnDestroy()
    {
        if (InstanceFinder.NetworkManager != null)
        {
            InstanceFinder.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
        }
    }

    private void HandleServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
            HandleServerStarted();
    }

    private void HandleServerStarted()
    {
        if (_initialized || InstanceFinder.NetworkManager == null || !InstanceFinder.IsServerStarted)
        {
            return;
        }

        if (_pickupPrefab == null)
        {
            Debug.LogError("PickupManager is missing pickup prefab reference.", this);
            return;
        }

        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            _spawnPoints = FindObjectsOfType<PickupSpawnPoint>()
                .OrderBy(point => point.name)
                .ToArray();
        }

        if (_spawnPoints.Length == 0)
        {
            Debug.LogWarning("PickupManager did not find any PickupSpawnPoint objects.");
            return;
        }

        _initialized = true;

        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            SpawnPickupAtPoint(i);
        }
    }

    public void NotifyPickupCollected(int spawnPointIndex)
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.IsServerStarted)
        {
            return;
        }

        _activePickups.Remove(spawnPointIndex);
        StartCoroutine(RespawnPickupRoutine(spawnPointIndex));
    }

    private IEnumerator RespawnPickupRoutine(int spawnPointIndex)
    {
        yield return new WaitForSeconds(_respawnDelay);

        if (InstanceFinder.NetworkManager == null || !InstanceFinder.IsServerStarted)
        {
            yield break;
        }

        SpawnPickupAtPoint(spawnPointIndex);
    }

    private void SpawnPickupAtPoint(int spawnPointIndex)
    {
        if (spawnPointIndex < 0 || spawnPointIndex >= _spawnPoints.Length)
        {
            return;
        }

        if (_activePickups.ContainsKey(spawnPointIndex) && _activePickups[spawnPointIndex] != null)
        {
            return;
        }

        PickupSpawnPoint spawnPoint = _spawnPoints[spawnPointIndex];
        if (spawnPoint == null)
        {
            return;
        }

        HealthPickup pickupInstance = Instantiate(
            _pickupPrefab,
            spawnPoint.transform.position,
            spawnPoint.transform.rotation);

        pickupInstance.Initialize(this, spawnPointIndex);
        NetworkObject networkObject = pickupInstance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            // TODO FishNet Editor setup: add FishNet NetworkObject to HealthPickup prefab and register it in DefaultPrefabObjects.
            Debug.LogError("HealthPickup prefab is missing FishNet NetworkObject.", pickupInstance);
            Destroy(pickupInstance.gameObject);
            return;
        }

        InstanceFinder.ServerManager.Spawn(networkObject);
        _activePickups[spawnPointIndex] = networkObject;
    }
}
