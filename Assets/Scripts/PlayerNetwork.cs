using System;
using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    public static event Action<PlayerNetwork> LocalPlayerSpawned;
    public static event Action<PlayerNetwork> LocalPlayerDespawned;

    public event Action<string> NicknameChanged;
    public event Action<int> HpChanged;
    public event Action<int> AmmoChanged;
    public event Action<bool> AliveChanged;

    [Header("Stats")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _maxAmmo = 8;
    [SerializeField] private float _respawnDelay = 3f;

    private readonly SyncVar<string> _nickname = new("Player");
    private readonly SyncVar<int> _hp = new(100);
    private readonly SyncVar<int> _currentAmmo = new();
    private readonly SyncVar<bool> _isAlive = new(true);

    public int MaxHealth => _maxHealth;
    public int MaxAmmo => _maxAmmo;
    public float RespawnDelay => _respawnDelay;
    public string Nickname => _nickname.Value;
    public int HP => _hp.Value;
    public int CurrentAmmo => _currentAmmo.Value;
    public bool IsAlive => _isAlive.Value;
    public double RespawnEndTime { get; private set; }
    public bool IsDeadOrRespawning => !IsAlive || RespawnEndTime > Time.unscaledTimeAsDouble;

    private Coroutine _respawnCoroutine;
    private PlayerSpawner _playerSpawner;

    private void Awake()
    {
        _playerSpawner = GetComponent<PlayerSpawner>();

        _nickname.OnChange += HandleNicknameChanged;
        _hp.OnChange += HandleHpChanged;
        _currentAmmo.OnChange += HandleAmmoChanged;
        _isAlive.OnChange += HandleAliveChanged;
    }

    public override void OnStartServer()
    {
        _hp.Value = _maxHealth;
        _currentAmmo.Value = _maxAmmo;
        _isAlive.Value = true;
        RespawnEndTime = 0d;
        Debug.Log($"PlayerNetwork OnStartServer object={name} ObjectId={ObjectId} OwnerId={OwnerId} Owner={Owner} HP={HP} IsAlive={IsAlive} Ammo={CurrentAmmo}");
    }

    public override void OnStartClient()
    {
        Debug.Log($"PlayerNetwork OnStartClient object={name} ObjectId={ObjectId} IsOwner={IsOwner} IsClient={IsClientInitialized} IsServer={IsServerInitialized} OwnerId={OwnerId} Owner={Owner} HP={HP} IsAlive={IsAlive} Ammo={CurrentAmmo} Nickname={Nickname}");
        RaiseAllStateEvents();

        if (!Owner.IsLocalClient)
            return;

        Debug.Log($"PlayerNetwork OnStartClient local owner object={name} submitting nickname '{ConnectionUI.PlayerNickname}'.");
        LocalPlayerSpawned?.Invoke(this);
        SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
    }

    public override void OnOwnershipClient(NetworkConnection prevOwner)
    {
        Debug.Log($"PlayerNetwork OnOwnershipClient object={name} ObjectId={ObjectId} PrevOwner={prevOwner} NewOwner={Owner} IsOwner={IsOwner}");
    }

    public override void OnStopClient()
    {
        if (Owner.IsLocalClient)
            LocalPlayerDespawned?.Invoke(this);
    }

    public override void OnStopServer()
    {
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }
    }

    [ServerRpc]
    private void SubmitNicknameServerRpc(string nickname, NetworkConnection sender = null)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname) ? $"Player_{sender?.ClientId ?? OwnerId}" : nickname.Trim();
        Debug.Log($"PlayerNetwork SubmitNicknameServerRpc object={name} sender={sender} owner={Owner} nickname='{safeValue}'");
        _nickname.Value = safeValue;
        gameObject.name = safeValue;
    }

    public bool CanReceiveDamage()
    {
        if (!IsServerInitialized || !IsSpawned)
            return false;

        return IsAlive && HP > 0;
    }

    public void TakeDamage(int amount)
    {
        if (!IsServerInitialized || amount <= 0 || !CanReceiveDamage())
            return;

        _hp.Value = Mathf.Max(0, HP - amount);

        if (_hp.Value == 0)
            HandleDeathServer();
    }

    public void Heal(int amount)
    {
        if (!IsServerInitialized || amount <= 0 || !IsAlive || HP >= _maxHealth)
            return;

        _hp.Value = Mathf.Min(_maxHealth, HP + amount);
    }

    public bool TryConsumeAmmoServer(int amount)
    {
        if (!IsServerInitialized || !IsSpawned || !IsAlive || amount <= 0 || CurrentAmmo < amount)
        {
            return false;
        }

        _currentAmmo.Value -= amount;
        return true;
    }

    public void RefillAmmoServer()
    {
        if (!IsServerInitialized || !IsSpawned)
        {
            return;
        }

        _currentAmmo.Value = _maxAmmo;
    }

    private void HandleDeathServer()
    {
        if (!IsServerInitialized || !IsSpawned || !IsAlive)
        {
            return;
        }

        _isAlive.Value = false;
        Debug.Log($"PlayerNetwork HandleDeathServer object={name} ObjectId={ObjectId} OwnerId={OwnerId} RespawnDelay={_respawnDelay}");

        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
        }

        _respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        Debug.Log($"PlayerNetwork RespawnRoutine started on server object={name} delay={_respawnDelay} IsServerInitialized={IsServerInitialized} IsOwner={IsOwner} currentPos={transform.position}");
        yield return new WaitForSeconds(_respawnDelay);

        if (!IsServerInitialized || !IsSpawned)
        {
            yield break;
        }

        if (_playerSpawner == null)
        {
            _playerSpawner = GetComponent<PlayerSpawner>();
        }

        Debug.Log($"PlayerNetwork RespawnRoutine before teleport object={name} position={transform.position} IsServerInitialized={IsServerInitialized} IsOwner={IsOwner}");
        _playerSpawner?.MoveToSpawnPoint();
        Debug.Log($"PlayerNetwork RespawnRoutine after teleport object={name} position={transform.position} IsServerInitialized={IsServerInitialized} IsOwner={IsOwner}");

        _hp.Value = _maxHealth;
        _currentAmmo.Value = _maxAmmo;
        _isAlive.Value = true;
        Debug.Log($"PlayerNetwork RespawnRoutine completed for object={name} ObjectId={ObjectId} HP={HP} Ammo={CurrentAmmo} IsAlive={IsAlive}");
        _respawnCoroutine = null;
    }

    public double GetRespawnRemainingTime()
    {
        return Math.Max(0d, RespawnEndTime - Time.unscaledTimeAsDouble);
    }

    private void RaiseAllStateEvents()
    {
        HandleNicknameChanged(Nickname, Nickname, false);
        HandleHpChanged(HP, HP, false);
        HandleAmmoChanged(CurrentAmmo, CurrentAmmo, false);
        HandleAliveChanged(IsAlive, IsAlive, false);
    }

    private void HandleNicknameChanged(string prev, string next, bool asServer)
    {
        NicknameChanged?.Invoke(next);
    }

    private void HandleHpChanged(int prev, int next, bool asServer)
    {
        HpChanged?.Invoke(next);
    }

    private void HandleAmmoChanged(int prev, int next, bool asServer)
    {
        AmmoChanged?.Invoke(next);
    }

    private void HandleAliveChanged(bool prev, bool next, bool asServer)
    {
        RespawnEndTime = next ? 0d : Time.unscaledTimeAsDouble + _respawnDelay;
        AliveChanged?.Invoke(next);
    }
}
