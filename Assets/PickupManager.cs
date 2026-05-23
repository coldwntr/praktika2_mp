using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class PickupManager : MonoBehaviour
{
    public static PickupManager Instance { get; private set; }

    [SerializeField] private HealthPickup _pickupPrefab;
    [SerializeField] private PickupSpawnPoint[] _spawnPoints;
    [SerializeField] private float _respawnDelay = 10f;

    private readonly Dictionary<int, NetworkObject> _activePickups = new Dictionary<int, NetworkObject>();
    private readonly Dictionary<int, Coroutine> _respawnCoroutines = new Dictionary<int, Coroutine>();
    private bool _spawnPointsInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple PickupManager instances detected.", this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (InstanceFinder.NetworkManager != null)
            InstanceFinder.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
    }

    private void Start()
    {
        if (InstanceFinder.NetworkManager == null)
        {
            Debug.LogWarning("PickupManager could not find FishNet NetworkManager.");
            return;
        }

        InstanceFinder.ServerManager.OnServerConnectionState += HandleServerConnectionState;
        EnsureSpawnPoints();
    }

    private void HandleServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
            EnsureSpawnPoints();
    }

    public void OnMatchStarted()
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.IsServerStarted)
            return;

        EnsureSpawnPoints();
        SpawnAllPickups();
    }

    public void OnMatchEnded()
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.IsServerStarted)
            return;

        ClearAllPickups();
    }

    public void NotifyPickupCollected(int spawnPointIndex)
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.IsServerStarted)
            return;

        if (!GameStateManager.AllowsGameplay())
            return;

        _activePickups.Remove(spawnPointIndex);

        if (_respawnCoroutines.TryGetValue(spawnPointIndex, out Coroutine runningRoutine) && runningRoutine != null)
            StopCoroutine(runningRoutine);

        _respawnCoroutines[spawnPointIndex] = StartCoroutine(RespawnPickupRoutine(spawnPointIndex));
    }

    private void EnsureSpawnPoints()
    {
        if (_spawnPointsInitialized)
            return;

        if (_pickupPrefab == null)
        {
            Debug.LogError("PickupManager is missing pickup prefab reference.", this);
            return;
        }

        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            _spawnPoints = FindObjectsByType<PickupSpawnPoint>(FindObjectsSortMode.None)
                .OrderBy(point => point.name)
                .ToArray();
        }

        if (_spawnPoints.Length == 0)
        {
            Debug.LogWarning("PickupManager did not find any PickupSpawnPoint objects.");
            return;
        }

        _spawnPointsInitialized = true;
    }

    private void SpawnAllPickups()
    {
        if (!_spawnPointsInitialized)
            return;

        for (int i = 0; i < _spawnPoints.Length; i++)
            SpawnPickupAtPoint(i);
    }

    private IEnumerator RespawnPickupRoutine(int spawnPointIndex)
    {
        yield return new WaitForSeconds(_respawnDelay);

        if (InstanceFinder.NetworkManager == null || !InstanceFinder.IsServerStarted)
            yield break;

        if (!GameStateManager.AllowsGameplay())
            yield break;

        SpawnPickupAtPoint(spawnPointIndex);
        _respawnCoroutines.Remove(spawnPointIndex);
    }

    private void SpawnPickupAtPoint(int spawnPointIndex)
    {
        if (!_spawnPointsInitialized || spawnPointIndex < 0 || spawnPointIndex >= _spawnPoints.Length)
            return;

        if (_activePickups.ContainsKey(spawnPointIndex) && _activePickups[spawnPointIndex] != null)
            return;

        PickupSpawnPoint spawnPoint = _spawnPoints[spawnPointIndex];
        if (spawnPoint == null)
            return;

        HealthPickup pickupInstance = Instantiate(
            _pickupPrefab,
            spawnPoint.transform.position,
            spawnPoint.transform.rotation);

        pickupInstance.Initialize(this, spawnPointIndex);
        NetworkObject networkObject = pickupInstance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("HealthPickup prefab is missing FishNet NetworkObject.", pickupInstance);
            Destroy(pickupInstance.gameObject);
            return;
        }

        InstanceFinder.ServerManager.Spawn(networkObject);
        _activePickups[spawnPointIndex] = networkObject;
    }

    private void ClearAllPickups()
    {
        foreach (KeyValuePair<int, Coroutine> pair in _respawnCoroutines)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }

        _respawnCoroutines.Clear();

        foreach (KeyValuePair<int, NetworkObject> pair in _activePickups)
        {
            NetworkObject networkObject = pair.Value;
            if (networkObject != null && networkObject.IsSpawned)
                InstanceFinder.ServerManager.Despawn(networkObject);
        }

        _activePickups.Clear();
    }
}
