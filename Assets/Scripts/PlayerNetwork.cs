using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    public static event Action<PlayerNetwork> LocalPlayerSpawned;
    public static event Action<PlayerNetwork> LocalPlayerDespawned;

    [Header("Stats")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _maxAmmo = 8;
    [SerializeField] private float _respawnDelay = 3f;

    public NetworkVariable<FixedString32Bytes> Nickname = new NetworkVariable<FixedString32Bytes>(
        new FixedString32Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> HP = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> CurrentAmmo = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<double> RespawnEndTime = new NetworkVariable<double>(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int MaxHealth => _maxHealth;
    public int MaxAmmo => _maxAmmo;
    public float RespawnDelay => _respawnDelay;

    private Coroutine _respawnCoroutine;
    private PlayerSpawner _playerSpawner;

    private void Awake()
    {
        _playerSpawner = GetComponent<PlayerSpawner>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            HP.Value = _maxHealth;
            CurrentAmmo.Value = _maxAmmo;
            IsAlive.Value = true;
            RespawnEndTime.Value = 0d;
        }

        if (IsOwner)
        {
            LocalPlayerSpawned?.Invoke(this);
            SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            LocalPlayerDespawned?.Invoke(this);
        }

        if (IsServer && _respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }
    }

    [ServerRpc]
    private void SubmitNicknameServerRpc(string nickname)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname) ? $"Player_{OwnerClientId}" : nickname.Trim();
        Nickname.Value = safeValue;
        gameObject.name = safeValue;
    }

    public bool TryApplyDamageServer(int damage)
    {
        if (!IsServer || !IsSpawned || !IsAlive.Value || damage <= 0)
        {
            return false;
        }

        HP.Value = Mathf.Max(0, HP.Value - damage);

        if (HP.Value == 0)
        {
            HandleDeathServer();
        }

        return true;
    }

    public bool TryRestoreHealthServer(int amount)
    {
        if (!IsServer || !IsSpawned || !IsAlive.Value || amount <= 0 || HP.Value >= _maxHealth)
        {
            return false;
        }

        HP.Value = Mathf.Min(_maxHealth, HP.Value + amount);
        return true;
    }

    public bool TryConsumeAmmoServer(int amount)
    {
        if (!IsServer || !IsSpawned || !IsAlive.Value || amount <= 0 || CurrentAmmo.Value < amount)
        {
            return false;
        }

        CurrentAmmo.Value -= amount;
        return true;
    }

    public void RefillAmmoServer()
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        CurrentAmmo.Value = _maxAmmo;
    }

    private void HandleDeathServer()
    {
        if (!IsServer || !IsSpawned || !IsAlive.Value)
        {
            return;
        }

        IsAlive.Value = false;
        RespawnEndTime.Value = NetworkManager.ServerTime.Time + _respawnDelay;

        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
        }

        _respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);

        if (!IsServer || !IsSpawned)
        {
            yield break;
        }

        if (_playerSpawner == null)
        {
            _playerSpawner = GetComponent<PlayerSpawner>();
        }

        _playerSpawner?.MoveToSpawnPoint();

        HP.Value = _maxHealth;
        CurrentAmmo.Value = _maxAmmo;
        IsAlive.Value = true;
        RespawnEndTime.Value = 0d;
        _respawnCoroutine = null;
    }
}
