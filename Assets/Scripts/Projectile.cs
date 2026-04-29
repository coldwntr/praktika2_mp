using System.Collections;
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private float _lifetime = 3f;
    [SerializeField] private float _transformSyncInterval = 0.033f;

    private int _ownerClientId;
    private int _ownerObjectId;
    private Vector3 _direction;
    private float _speed;
    private int _damage;
    private bool _initialized;
    private bool _useManualTransformSync;
    private Coroutine _lifetimeCoroutine;
    private bool _loggedMissingRigidbody;
    private bool _loggedFirstTransformSync;
    private bool _loggedFirstTransformApply;
    private Vector3 _lastSyncedPosition;
    private Quaternion _lastSyncedRotation;
    private float _nextTransformSyncTime;

    private void Awake()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }
    }

    public void Initialize(int ownerClientId, int ownerObjectId, Vector3 direction, float speed, int damage)
    {
        _ownerClientId = ownerClientId;
        _ownerObjectId = ownerObjectId;
        _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward.normalized;
        _speed = speed;
        _damage = damage;
        _initialized = true;
    }

    public override void OnStartServer()
    {
        _useManualTransformSync = GetComponent<NetworkTransform>() == null;
        Debug.Log($"Projectile OnStartServer object={name} speed={_speed} forward={_direction} ownerClientId={_ownerClientId} ownerObjectId={_ownerObjectId} IsServerInitialized={IsServerInitialized}");

        if (!IsServerInitialized)
        {
            Debug.LogWarning($"Projectile object={name} started without server initialization.", this);
            return;
        }

        if (_speed <= 0f)
        {
            Debug.LogWarning($"Projectile object={name} has non-positive speed {_speed}.", this);
        }

        if (_rigidbody == null && !_loggedMissingRigidbody)
        {
            _loggedMissingRigidbody = true;
            Debug.LogWarning($"Projectile object={name} has no Rigidbody. Falling back to transform-based movement.", this);
        }

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
        }

        transform.rotation = Quaternion.LookRotation(_direction.sqrMagnitude > 0.001f ? _direction : transform.forward, Vector3.up);
        _lastSyncedPosition = transform.position;
        _lastSyncedRotation = transform.rotation;
        _nextTransformSyncTime = 0f;

        if (_useManualTransformSync)
        {
            BroadcastTransformState();
        }

        _lifetimeCoroutine = StartCoroutine(LifetimeRoutine());
    }

    public override void OnStartClient()
    {
        _useManualTransformSync = GetComponent<NetworkTransform>() == null;
        if (_rigidbody != null)
            _rigidbody.isKinematic = true;
    }

    public override void OnStopNetwork()
    {
        if (_lifetimeCoroutine != null)
        {
            StopCoroutine(_lifetimeCoroutine);
            _lifetimeCoroutine = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized || !_initialized)
        {
            return;
        }

        PlayerNetwork targetPlayer = other.GetComponentInParent<PlayerNetwork>();

        if (targetPlayer != null)
        {
            if (targetPlayer.ObjectId == _ownerObjectId || targetPlayer.OwnerId == _ownerClientId)
            {
                return;
            }

            if (targetPlayer.CanReceiveDamage())
            {
                Debug.Log($"Hit target={targetPlayer.name} serverPos={targetPlayer.transform.position} hp={targetPlayer.HP}");
                targetPlayer.TakeDamage(_damage);
                DespawnServer();
                return;
            }

            if (targetPlayer.IsDeadOrRespawning)
            {
                DespawnServer();
                return;
            }
        }

        if (!other.isTrigger)
        {
            DespawnServer();
        }
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(_lifetime);
        DespawnServer();
    }

    private void Update()
    {
        if (!IsServerInitialized || !_initialized)
        {
            return;
        }

        if (_speed <= 0f)
        {
            return;
        }

        Vector3 stepDirection = _direction.sqrMagnitude > 0.001f ? _direction : transform.forward;
        transform.position += stepDirection * (_speed * Time.deltaTime);

        if (_useManualTransformSync)
        {
            TryBroadcastTransformState();
        }
    }

    private void DespawnServer()
    {
        if (!IsServerInitialized)
        {
            return;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            ServerManager.Despawn(NetworkObject);
            return;
        }

        Destroy(gameObject);
    }

    private void TryBroadcastTransformState()
    {
        if (Time.unscaledTime < _nextTransformSyncTime)
        {
            return;
        }

        if (Vector3.Distance(transform.position, _lastSyncedPosition) <= 0.0001f &&
            Quaternion.Angle(transform.rotation, _lastSyncedRotation) <= 0.1f)
        {
            return;
        }

        BroadcastTransformState();
    }

    private void BroadcastTransformState()
    {
        _lastSyncedPosition = transform.position;
        _lastSyncedRotation = transform.rotation;
        _nextTransformSyncTime = Time.unscaledTime + Mathf.Max(0.02f, _transformSyncInterval);

        if (!_loggedFirstTransformSync)
        {
            _loggedFirstTransformSync = true;
            Debug.Log($"Projectile BroadcastTransformState object={name} position={_lastSyncedPosition} rotation={_lastSyncedRotation.eulerAngles}");
        }

        SyncTransformObserversRpc(_lastSyncedPosition, _lastSyncedRotation);
    }

    [ObserversRpc(ExcludeServer = true)]
    private void SyncTransformObserversRpc(Vector3 position, Quaternion rotation)
    {
        if (IsServerInitialized || !_useManualTransformSync)
        {
            return;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (!_loggedFirstTransformApply)
        {
            _loggedFirstTransformApply = true;
            Debug.Log($"Projectile ApplyRemoteTransform object={name} position={position} rotation={rotation.eulerAngles}");
        }
    }
}
