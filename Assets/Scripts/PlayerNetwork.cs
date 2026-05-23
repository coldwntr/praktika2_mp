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
    public event Action<int> ScoreChanged;
    public event Action<int> KillsChanged;
    public event Action<int> DeathsChanged;

    [Header("Stats")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _maxAmmo = 8;
    [SerializeField] private float _respawnDelay = 3f;
    [SerializeField] private int _scorePerKill = 1;

    private readonly SyncVar<string> _nickname = new("Player");
    private readonly SyncVar<int> _hp = new(100);
    private readonly SyncVar<int> _currentAmmo = new();
    private readonly SyncVar<bool> _isAlive = new(true);
    private readonly SyncVar<int> _score = new();
    private readonly SyncVar<int> _kills = new();
    private readonly SyncVar<int> _deaths = new();

    public int MaxHealth => _maxHealth;
    public int MaxAmmo => _maxAmmo;
    public float RespawnDelay => _respawnDelay;
    public string Nickname => _nickname.Value;
    public int HP => _hp.Value;
    public int CurrentAmmo => _currentAmmo.Value;
    public bool IsAlive => _isAlive.Value;
    public int Score => _score.Value;
    public int Kills => _kills.Value;
    public int Deaths => _deaths.Value;
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
        _score.OnChange += HandleScoreChanged;
        _kills.OnChange += HandleKillsChanged;
        _deaths.OnChange += HandleDeathsChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _hp.Value = _maxHealth;
        _currentAmmo.Value = _maxAmmo;
        _isAlive.Value = true;
        RespawnEndTime = 0d;

        GameStateManager.NotifyPlayerCountChanged();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        RaiseAllStateEvents();

        if (!Owner.IsLocalClient)
            return;

        LocalPlayerSpawned?.Invoke(this);
        SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
    }

    public override void OnStopClient()
    {
        if (Owner.IsLocalClient)
            LocalPlayerDespawned?.Invoke(this);

        base.OnStopClient();
    }

    public override void OnStopServer()
    {
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }

        GameStateManager.NotifyPlayerCountChanged();
        base.OnStopServer();
    }

    [ServerRpc]
    private void SubmitNicknameServerRpc(string nickname, NetworkConnection sender = null)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname) ? $"Player_{sender?.ClientId ?? OwnerId}" : nickname.Trim();
        _nickname.Value = safeValue;
        gameObject.name = safeValue;
    }

    public bool CanReceiveDamage()
    {
        if (!IsServerInitialized || !IsSpawned)
            return false;

        return IsAlive && HP > 0;
    }

    public void TakeDamage(int amount, PlayerNetwork attacker = null)
    {
        if (!IsServerInitialized || amount <= 0 || !CanReceiveDamage())
            return;

        _hp.Value = Mathf.Max(0, HP - amount);

        if (_hp.Value == 0)
            HandleDeathServer(attacker);
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
            return false;

        _currentAmmo.Value -= amount;
        return true;
    }

    public void RefillAmmoServer()
    {
        if (!IsServerInitialized || !IsSpawned)
            return;

        _currentAmmo.Value = _maxAmmo;
    }

    public void AddKill()
    {
        if (!IsServerInitialized)
            return;

        _kills.Value++;
        _score.Value += _scorePerKill;
    }

    public void AddDeath()
    {
        if (!IsServerInitialized)
            return;

        _deaths.Value++;
    }

    public void ResetForMatch()
    {
        if (!IsServerInitialized)
            return;

        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }

        _score.Value = 0;
        _kills.Value = 0;
        _deaths.Value = 0;
        ReviveAtSpawnServer();
    }

    private void HandleDeathServer(PlayerNetwork attacker)
    {
        if (!IsServerInitialized || !IsSpawned || !IsAlive)
            return;

        _isAlive.Value = false;
        AddDeath();

        if (attacker != null && attacker != this)
            attacker.AddKill();

        if (_respawnCoroutine != null)
            StopCoroutine(_respawnCoroutine);

        _respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);

        if (!IsServerInitialized || !IsSpawned)
            yield break;

        if (!GameStateManager.AllowsGameplay())
        {
            _respawnCoroutine = null;
            yield break;
        }

        ReviveAtSpawnServer();
        _respawnCoroutine = null;
    }

    private void ReviveAtSpawnServer()
    {
        if (_playerSpawner == null)
            _playerSpawner = GetComponent<PlayerSpawner>();

        _playerSpawner?.MoveToSpawnPoint();

        _hp.Value = _maxHealth;
        _currentAmmo.Value = _maxAmmo;
        _isAlive.Value = true;
        RespawnEndTime = 0d;

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ResetMotionStateServer();
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
        HandleScoreChanged(Score, Score, false);
        HandleKillsChanged(Kills, Kills, false);
        HandleDeathsChanged(Deaths, Deaths, false);
    }

    private void HandleNicknameChanged(string prev, string next, bool asServer) => NicknameChanged?.Invoke(next);
    private void HandleHpChanged(int prev, int next, bool asServer) => HpChanged?.Invoke(next);
    private void HandleAmmoChanged(int prev, int next, bool asServer) => AmmoChanged?.Invoke(next);
    private void HandleAliveChanged(bool prev, bool next, bool asServer)
    {
        RespawnEndTime = next ? 0d : Time.unscaledTimeAsDouble + _respawnDelay;
        AliveChanged?.Invoke(next);
    }

    private void HandleScoreChanged(int prev, int next, bool asServer) => ScoreChanged?.Invoke(next);
    private void HandleKillsChanged(int prev, int next, bool asServer) => KillsChanged?.Invoke(next);
    private void HandleDeathsChanged(int prev, int next, bool asServer) => DeathsChanged?.Invoke(next);
}
