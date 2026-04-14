using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
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
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("PickupManager could not find NetworkManager.");
            return;
        }

        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;

        if (NetworkManager.Singleton.IsServer)
        {
            HandleServerStarted();
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }
    }

    private void HandleServerStarted()
    {
        if (_initialized || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
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
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        _activePickups.Remove(spawnPointIndex);
        StartCoroutine(RespawnPickupRoutine(spawnPointIndex));
    }

    private IEnumerator RespawnPickupRoutine(int spawnPointIndex)
    {
        yield return new WaitForSeconds(_respawnDelay);

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
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
        pickupInstance.NetworkObject.Spawn(true);
        _activePickups[spawnPointIndex] = pickupInstance.NetworkObject;
    }
}
