using System;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

public enum GameState : byte
{
    Lobby,
    Countdown,
    Match,
    Results
}

public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public static event Action<GameState> StateChanged;
    public static event Action TimersChanged;

    [Header("Match Settings")]
    [SerializeField] private int _requiredPlayers = 2;
    [SerializeField] private float _matchDuration = 60f;
    [SerializeField] private float _resultsDuration = 5f;
    [SerializeField] private float _lobbyDelayBeforeRematch = 2f;
    [SerializeField] private int _killsToWin = 3;

    private readonly SyncVar<GameState> _currentState = new(GameState.Lobby);
    private readonly SyncVar<int> _connectedPlayers = new();
    private readonly SyncVar<float> _matchTimer = new();
    private readonly SyncVar<float> _resultsTimer = new();

    private FishNet.Component.Spawning.PlayerSpawner _fishNetPlayerSpawner;
    private float _lobbyRecheckTimer;

    public int RequiredPlayers => _requiredPlayers;
    public GameState CurrentState => _currentState.Value;
    public int ConnectedPlayers => _connectedPlayers.Value;
    public float MatchTimer => _matchTimer.Value;
    public float ResultsTimer => _resultsTimer.Value;

    public static bool AllowsGameplay()
    {
        GameStateManager manager = GetOrFind();
        if (manager == null)
            return false;

        return manager.CurrentState == GameState.Match;
    }

    public static GameStateManager GetOrFind()
    {
        if (Instance != null)
            return Instance;

        return FindFirstObjectByType<GameStateManager>(FindObjectsInactive.Include);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (Instance != null && Instance != this)
            Debug.LogWarning("[GameStateManager] Multiple instances detected. Replacing singleton reference.");

        Instance = this;
        InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        SubscribeToPlayerSpawner();

        _currentState.OnChange += OnCurrentStateChanged;
        UpdateConnectedPlayersCount();
    }

    public override void OnStopServer()
    {
        if (Instance == this)
            Instance = null;

        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;

        UnsubscribeFromPlayerSpawner();
        _currentState.OnChange -= OnCurrentStateChanged;

        base.OnStopServer();
    }

    public static void NotifyPlayerCountChanged()
    {
        if (Instance == null || !Instance.IsServerInitialized)
            return;

        Instance.UpdateConnectedPlayersCount();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        Instance = this;

        _currentState.OnChange += OnCurrentStateChanged;
        _matchTimer.OnChange += OnTimerChanged;
        _resultsTimer.OnChange += OnTimerChanged;

        StateChanged?.Invoke(_currentState.Value);
        TimersChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        _currentState.OnChange -= OnCurrentStateChanged;
        _matchTimer.OnChange -= OnTimerChanged;
        _resultsTimer.OnChange -= OnTimerChanged;

        if (Instance == this)
            Instance = null;

        base.OnStopClient();
    }

    private void Update()
    {
        if (!IsServerInitialized)
            return;

        switch (_currentState.Value)
        {
            case GameState.Lobby:
                _lobbyRecheckTimer -= Time.deltaTime;
                if (_lobbyRecheckTimer <= 0f)
                {
                    _lobbyRecheckTimer = 0.25f;
                    UpdateConnectedPlayersCount();
                }

                break;

            case GameState.Match:
                _matchTimer.Value -= Time.deltaTime;

                if (_matchTimer.Value <= 0f)
                {
                    _matchTimer.Value = 0f;
                    EndMatch();
                }
                else
                {
                    CheckKillWinCondition();
                }

                break;

            case GameState.Results:
                _resultsTimer.Value -= Time.deltaTime;

                if (_resultsTimer.Value <= 0f)
                {
                    _resultsTimer.Value = 0f;
                    ResetLobby();
                }

                break;
        }
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        UpdateConnectedPlayersCount();
    }

    private void SubscribeToPlayerSpawner()
    {
        if (InstanceFinder.NetworkManager == null)
            return;

        _fishNetPlayerSpawner = InstanceFinder.NetworkManager.GetComponent<FishNet.Component.Spawning.PlayerSpawner>();
        if (_fishNetPlayerSpawner == null)
            return;

        _fishNetPlayerSpawner.OnSpawned -= OnFishNetPlayerSpawned;
        _fishNetPlayerSpawner.OnSpawned += OnFishNetPlayerSpawned;
    }

    private void UnsubscribeFromPlayerSpawner()
    {
        if (_fishNetPlayerSpawner == null)
            return;

        _fishNetPlayerSpawner.OnSpawned -= OnFishNetPlayerSpawned;
        _fishNetPlayerSpawner = null;
    }

    private void OnFishNetPlayerSpawned(NetworkObject playerObject)
    {
        UpdateConnectedPlayersCount();
    }

    private void UpdateConnectedPlayersCount()
    {
        if (InstanceFinder.ServerManager == null)
            return;

        _connectedPlayers.Value = CountReadyPlayers();

        if (_currentState.Value == GameState.Lobby)
            TryStartMatch();
    }

    private int CountReadyPlayers()
    {
        int spawnedPlayers = CountSpawnedPlayersOnServer();
        if (spawnedPlayers > 0)
            return spawnedPlayers;

        return CountActiveConnections();
    }

    private int CountSpawnedPlayersOnServer()
    {
        if (InstanceFinder.ServerManager?.Objects?.Spawned == null)
            return 0;

        int count = 0;
        foreach (NetworkObject networkObject in InstanceFinder.ServerManager.Objects.Spawned.Values)
        {
            if (networkObject == null || !networkObject.IsSpawned)
                continue;

            if (networkObject.GetComponent<PlayerNetwork>() != null)
                count++;
        }

        return count;
    }

    private int CountActiveConnections()
    {
        if (InstanceFinder.ServerManager == null)
            return 0;

        int count = InstanceFinder.ServerManager.Clients.Count;

        // Host: локальный игрок не входит в Clients, но уже на сервере.
        if (InstanceFinder.IsHostStarted)
            count++;

        return count;
    }

    private void TryStartMatch()
    {
        if (_currentState.Value != GameState.Lobby)
            return;

        int readyPlayers = CountReadyPlayers();
        _connectedPlayers.Value = readyPlayers;

        if (readyPlayers < _requiredPlayers)
            return;

        StartMatch();
    }

    private void StartMatch()
    {
        if (_currentState.Value != GameState.Lobby)
            return;

        _currentState.Value = GameState.Match;
        _matchTimer.Value = _matchDuration;
        _resultsTimer.Value = 0f;

        ResetAllPlayersForMatch();
        PickupManager.Instance?.OnMatchStarted();

        Debug.Log($"[GameStateManager] Match started. Duration={_matchDuration}s Players={CountReadyPlayers()}");
    }

    private void EndMatch()
    {
        if (_currentState.Value != GameState.Match)
            return;

        _resultsTimer.Value = _resultsDuration;
        _currentState.Value = GameState.Results;
        PickupManager.Instance?.OnMatchEnded();

        Debug.Log($"[GameStateManager] Match ended. Showing results for {_resultsDuration}s.");
    }

    private void ResetLobby()
    {
        CancelInvoke(nameof(TryStartMatch));

        _matchTimer.Value = 0f;
        _resultsTimer.Value = 0f;
        _connectedPlayers.Value = CountReadyPlayers();
        _currentState.Value = GameState.Lobby;

        ResetAllPlayersForMatch();
        PickupManager.Instance?.OnMatchEnded();

        Debug.Log($"[GameStateManager] Returned to lobby. Players={_connectedPlayers.Value}/{_requiredPlayers}");
        Invoke(nameof(TryStartMatch), Mathf.Max(0.5f, _lobbyDelayBeforeRematch));
    }

    private void CheckKillWinCondition()
    {
        if (_killsToWin <= 0)
            return;

        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);

        for (int i = 0; i < players.Length; i++)
        {
            PlayerNetwork player = players[i];
            if (player != null && player.Kills >= _killsToWin)
            {
                Debug.Log($"[GameStateManager] Match ended. {player.Nickname} reached {_killsToWin} kills.");
                EndMatch();
                return;
            }
        }
    }

    private void ResetAllPlayersForMatch()
    {
        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);

        for (int i = 0; i < players.Length; i++)
        {
            PlayerNetwork player = players[i];
            if (player != null && player.IsServerInitialized)
                player.ResetForMatch();
        }
    }

    private void OnCurrentStateChanged(GameState prev, GameState next, bool asServer)
    {
        StateChanged?.Invoke(next);
    }

    private void OnTimerChanged(float prev, float next, bool asServer)
    {
        TimersChanged?.Invoke();
    }
}
