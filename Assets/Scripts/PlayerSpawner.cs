using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private NetworkTransform _networkTransform;

    private static PlayerSpawnPoint[] _cachedSpawnPoints;

    private void Awake()
    {
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        if (_networkTransform == null)
        {
            _networkTransform = GetComponent<NetworkTransform>();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            MoveToSpawnPoint(false);
        }
    }

    public void MoveToSpawnPoint()
    {
        MoveToSpawnPoint(true);
    }

    public void MoveToSpawnPoint(bool synchronizeTeleport)
    {
        if (!IsServer)
        {
            return;
        }

        PlayerSpawnPoint spawnPoint = GetRandomSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning("PlayerSpawnPoint was not found on the scene.");
            return;
        }

        TeleportTo(spawnPoint.transform.position, spawnPoint.transform.rotation, synchronizeTeleport);
    }

    private void TeleportTo(Vector3 worldPosition, Quaternion worldRotation, bool synchronizeTeleport)
    {
        bool hadController = _characterController != null && _characterController.enabled;

        if (hadController)
        {
            _characterController.enabled = false;
        }

        transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (synchronizeTeleport && _networkTransform != null)
        {
            _networkTransform.Teleport(worldPosition, worldRotation, transform.localScale);
        }

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
