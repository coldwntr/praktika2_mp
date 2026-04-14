using Unity.Netcode;
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

    private void Update()
    {
        if (!IsOwner || _playerNetwork == null || !_playerNetwork.IsAlive.Value)
        {
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
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

        Vector3 shotOrigin = _firePoint.position;
        Vector3 shotDirection = transform.forward.normalized;

        RequestShootServerRpc(shotOrigin, shotDirection);
    }

    [ServerRpc]
    private void RequestShootServerRpc(Vector3 clientOrigin, Vector3 clientDirection, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        if (_playerNetwork == null || !_playerNetwork.IsAlive.Value)
        {
            return;
        }

        double serverTime = NetworkManager.ServerTime.Time;
        if (serverTime < _nextAllowedShotTime)
        {
            return;
        }

        if (_playerNetwork.CurrentAmmo.Value <= 0)
        {
            return;
        }

        if (_firePoint == null || _projectilePrefab == null)
        {
            return;
        }

        Vector3 serverOrigin = _firePoint.position;
        Vector3 validatedDirection = transform.forward.normalized;

        if (Vector3.Distance(clientOrigin, serverOrigin) > _maxOriginError)
        {
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
            Destroy(projectileInstance);
            return;
        }

        projectile.Initialize(OwnerClientId, NetworkObjectId, validatedDirection, _projectileSpeed, _damage);
        projectileNetworkObject.Spawn(true);
    }
}
