using System;
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEngine;

public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private PlayerMovement _playerMovement;

    private static PlayerSpawnPoint[] _cachedSpawnPoints;

    private void Awake()
    {
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }
    }

    public override void OnStartServer()
    {
        Debug.Log($"PlayerSpawner OnStartServer object={name} Owner={Owner} IsServer={IsServerInitialized} hasFishNetNetworkTransform={GetComponent<NetworkTransform>() != null}");
        Debug.Log($"PlayerSpawner initial teleport disabled for object={name}. FishNet built-in PlayerSpawner owns initial spawn placement.");
    }

    public void MoveToSpawnPoint()
    {
        MoveToSpawnPoint(true);
    }

    public void MoveToSpawnPoint(bool synchronizeTeleport)
    {
        if (!IsServerInitialized)
        {
            Debug.Log($"PlayerSpawner MoveToSpawnPoint ignored on non-server object={name} IsServerInitialized={IsServerInitialized}");
            return;
        }

        PlayerSpawnPoint spawnPoint = GetRandomSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning("PlayerSpawnPoint was not found on the scene.");
            return;
        }

        Debug.Log($"PlayerSpawner selected spawn point object={name} spawnPoint={spawnPoint.name} currentPos={transform.position} targetPos={spawnPoint.transform.position} synchronizeTeleport={synchronizeTeleport}");
        TeleportTo(spawnPoint.transform.position, spawnPoint.transform.rotation, synchronizeTeleport);
    }

    private void TeleportTo(Vector3 worldPosition, Quaternion worldRotation, bool synchronizeTeleport)
    {
        _ = synchronizeTeleport;
        bool hadController = _characterController != null && _characterController.enabled;

        if (hadController)
        {
            _characterController.enabled = false;
        }

        Debug.Log($"PlayerSpawner server teleport object={name} before={transform.position} after={worldPosition} rotation={worldRotation.eulerAngles}");
        transform.SetPositionAndRotation(worldPosition, worldRotation);
        // TODO FishNet Editor setup: add FishNet NetworkTransform or CSP reconciliation so teleports replicate cleanly.
        _playerMovement?.ResetMotionStateServer();

        if (hadController)
        {
            _characterController.enabled = true;
        }
    }

    private static PlayerSpawnPoint GetRandomSpawnPoint()
    {
        if (_cachedSpawnPoints == null || _cachedSpawnPoints.Length == 0)
        {
            _cachedSpawnPoints = FindObjectsOfType<PlayerSpawnPoint>();
            Array.Sort(_cachedSpawnPoints, (left, right) => string.CompareOrdinal(left.name, right.name));
        }

        if (_cachedSpawnPoints.Length == 0)
        {
            return null;
        }

        int index = UnityEngine.Random.Range(0, _cachedSpawnPoints.Length);
        return _cachedSpawnPoints[index];
    }
}
