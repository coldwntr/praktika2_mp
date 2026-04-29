using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private int _damage = 25;
    [SerializeField] private float _projectileSpeed = 18f;
    [SerializeField] private float _shootCooldown = 0.35f;
    [SerializeField] private float _maxOriginError = 1f;

    private double _nextAllowedShotTime;

    private void Awake()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        if (_firePoint == null)
        {
            Transform firePointTransform = transform.Find("FirePoint");
            if (firePointTransform != null)
            {
                _firePoint = firePointTransform;
            }
        }
    }

    public override void OnStartClient()
    {
        Debug.Log($"PlayerCombat OnStartClient object={name} IsOwner={IsOwner} IsClient={IsClientInitialized} IsServer={IsServerInitialized} Owner={Owner}");
    }

    private void Update()
    {
        bool firePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (!IsOwner)
        {
            if (firePressed)
            {
                Debug.Log($"PlayerCombat fire ignored object={name} reason=!IsOwner IsOwner={IsOwner} IsAlive={_playerNetwork != null && _playerNetwork.IsAlive} ammo={_playerNetwork?.CurrentAmmo ?? -1} firePointAssigned={_firePoint != null} projectilePrefabAssigned={_projectilePrefab != null}");
            }

            return;
        }

        if (_playerNetwork == null || !_playerNetwork.IsAlive)
        {
            if (firePressed)
            {
                Debug.Log($"PlayerCombat fire ignored object={name} reason={(_playerNetwork == null ? "missing PlayerNetwork" : "!IsAlive")} IsOwner={IsOwner} IsAlive={_playerNetwork != null && _playerNetwork.IsAlive} ammo={_playerNetwork?.CurrentAmmo ?? -1} firePointAssigned={_firePoint != null} projectilePrefabAssigned={_projectilePrefab != null}");
            }

            return;
        }

        if (firePressed)
        {
            Debug.Log($"PlayerCombat fire pressed object={name} IsOwner={IsOwner} IsAlive={_playerNetwork.IsAlive} ammo={_playerNetwork.CurrentAmmo} firePointAssigned={_firePoint != null} projectilePrefabAssigned={_projectilePrefab != null}");
            TryShoot();
        }
    }

    private void TryShoot()
    {
        if (_firePoint == null || _projectilePrefab == null)
        {
            Debug.LogWarning("PlayerCombat is missing FirePoint or Projectile Prefab reference.", this);
            return;
        }

        if (_projectilePrefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning($"PlayerCombat object={name} projectile prefab '{_projectilePrefab.name}' has no FishNet NetworkObject.", this);
        }

        if (_projectilePrefab.GetComponent<NetworkTransform>() == null)
        {
            Debug.LogWarning($"PlayerCombat object={name} projectile prefab '{_projectilePrefab.name}' has no FishNet NetworkTransform. Clients may not see projectile movement.", this);
        }

        Vector3 shotOrigin = _firePoint.position;
        Vector3 shotDirection = _firePoint.forward.sqrMagnitude > 0.001f ? _firePoint.forward.normalized : transform.forward.normalized;

        if (shotDirection.sqrMagnitude <= 0.001f)
        {
            shotDirection = Vector3.forward;
        }

        RequestShootServerRpc(shotOrigin, shotDirection);
    }

    [ServerRpc]
    private void RequestShootServerRpc(Vector3 clientOrigin, Vector3 clientDirection, NetworkConnection sender = null)
    {
        if (sender != Owner)
        {
            Debug.LogWarning($"PlayerCombat RequestShootServerRpc rejected object={name} reason=senderIsNotOwner sender={sender} owner={Owner}");
            return;
        }

        if (_playerNetwork == null || !_playerNetwork.IsAlive)
        {
            Debug.Log($"PlayerCombat RequestShootServerRpc rejected object={name} reason={(_playerNetwork == null ? "missing PlayerNetwork" : "!IsAlive")}");
            return;
        }

        double serverTime = Time.unscaledTimeAsDouble;
        if (serverTime < _nextAllowedShotTime)
        {
            Debug.Log($"PlayerCombat RequestShootServerRpc rejected object={name} reason=cooldown serverTime={serverTime:F3} nextAllowed={_nextAllowedShotTime:F3}");
            return;
        }

        if (_playerNetwork.CurrentAmmo <= 0)
        {
            Debug.Log($"PlayerCombat RequestShootServerRpc rejected object={name} reason=noAmmo");
            return;
        }

        if (_firePoint == null || _projectilePrefab == null)
        {
            Debug.LogWarning($"PlayerCombat RequestShootServerRpc rejected object={name} reason=missingFirePointOrPrefab firePointAssigned={_firePoint != null} projectilePrefabAssigned={_projectilePrefab != null}");
            return;
        }

        Vector3 serverOrigin = _firePoint.position;
        Vector3 validatedDirection = _firePoint.forward.sqrMagnitude > 0.001f ? _firePoint.forward.normalized : transform.forward.normalized;

        if (validatedDirection.sqrMagnitude <= 0.001f)
        {
            validatedDirection = Vector3.forward;
        }

        float originDistance = Vector3.Distance(clientOrigin, serverOrigin);
        if (originDistance > _maxOriginError)
        {
            Debug.Log($"PlayerCombat RequestShootServerRpc rejected object={name} reason=originMismatch distance={originDistance:F3} max={_maxOriginError:F3}");
            return;
        }

        if (clientDirection.sqrMagnitude > 0.001f)
        {
            Vector3 requestedDirection = clientDirection.normalized;
            float forwardAlignment = Vector3.Dot(validatedDirection, requestedDirection);

            if (forwardAlignment >= 0.5f)
            {
                validatedDirection = requestedDirection;
            }
        }

        if (!_playerNetwork.TryConsumeAmmoServer(1))
        {
            Debug.Log($"PlayerCombat RequestShootServerRpc rejected object={name} reason=TryConsumeAmmoServerFailed ammo={_playerNetwork.CurrentAmmo}");
            return;
        }

        _nextAllowedShotTime = serverTime + _shootCooldown;

        GameObject projectileInstance = Instantiate(
            _projectilePrefab,
            serverOrigin,
            Quaternion.LookRotation(validatedDirection, Vector3.up));

        Projectile projectile = projectileInstance.GetComponent<Projectile>();
        NetworkObject projectileNetworkObject = projectileInstance.GetComponent<NetworkObject>();

        if (projectile == null || projectileNetworkObject == null)
        {
            // TODO FishNet Editor setup: add FishNet NetworkObject to Projectile prefab.
            Debug.LogWarning($"PlayerCombat failed to spawn projectile for object={name}. projectileComponentAssigned={projectile != null} networkObjectAssigned={projectileNetworkObject != null}");
            Destroy(projectileInstance);
            return;
        }

        projectile.Initialize(OwnerId, ObjectId, validatedDirection, _projectileSpeed, _damage);
        Debug.Log($"PlayerCombat RequestShootServerRpc spawning projectile object={name} ownerId={OwnerId} objectId={ObjectId} direction={validatedDirection}");
        ServerManager.Spawn(projectileNetworkObject);
    }
}
