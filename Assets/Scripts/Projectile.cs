using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private float _lifetime = 3f;

    private ulong _ownerClientId;
    private ulong _ownerObjectId;
    private Vector3 _direction;
    private float _speed;
    private int _damage;
    private bool _initialized;
    private Coroutine _lifetimeCoroutine;

    private void Awake()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }
    }

    public void Initialize(ulong ownerClientId, ulong ownerObjectId, Vector3 direction, float speed, int damage)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        _ownerClientId = ownerClientId;
        _ownerObjectId = ownerObjectId;
        _direction = direction.normalized;
        _speed = speed;
        _damage = damage;
        _initialized = true;

        ApplyVelocity();
    }

    public override void OnNetworkSpawn()
    {
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = !IsServer;
        }

        if (IsServer)
        {
            ApplyVelocity();
            _lifetimeCoroutine = StartCoroutine(LifetimeRoutine());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_lifetimeCoroutine != null)
        {
            StopCoroutine(_lifetimeCoroutine);
            _lifetimeCoroutine = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !_initialized)
        {
            return;
        }

        PlayerNetwork targetPlayer = other.GetComponentInParent<PlayerNetwork>();

        if (targetPlayer != null)
        {
            if (targetPlayer.NetworkObjectId == _ownerObjectId || targetPlayer.OwnerClientId == _ownerClientId)
            {
                return;
            }

            if (targetPlayer.TryApplyDamageServer(_damage))
            {
                DespawnServer();
                return;
            }

            if (!targetPlayer.IsAlive.Value)
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

    private void ApplyVelocity()
    {
        if (!IsServer || !_initialized || _rigidbody == null)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        _rigidbody.linearVelocity = _direction * _speed;
    }

    private void DespawnServer()
    {
        if (!IsServer)
        {
            return;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
            return;
        }

        Destroy(gameObject);
    }
}
