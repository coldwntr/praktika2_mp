using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text _ammoText;
    [SerializeField] private TMP_Text _respawnText;
    [SerializeField] private GameObject _respawnPanel;

    private PlayerNetwork _localPlayer;

    private void OnEnable()
    {
        PlayerNetwork.LocalPlayerSpawned += HandleLocalPlayerSpawned;
        PlayerNetwork.LocalPlayerDespawned += HandleLocalPlayerDespawned;
        TryBindExistingLocalPlayer();
    }

    private void OnDisable()
    {
        PlayerNetwork.LocalPlayerSpawned -= HandleLocalPlayerSpawned;
        PlayerNetwork.LocalPlayerDespawned -= HandleLocalPlayerDespawned;
        UnbindLocalPlayer();
    }

    private void Update()
    {
        if (_localPlayer == null || _localPlayer.IsAlive.Value || _localPlayer.RespawnEndTime.Value <= 0d)
        {
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        double remainingTime = _localPlayer.RespawnEndTime.Value - NetworkManager.Singleton.ServerTime.Time;
        remainingTime = Mathf.Max(0f, (float)remainingTime);

        if (_respawnText != null)
        {
            _respawnText.text = $"Respawn in: {remainingTime:0.0}s";
        }
    }

    private void HandleLocalPlayerSpawned(PlayerNetwork player)
    {
        BindLocalPlayer(player);
    }

    private void HandleLocalPlayerDespawned(PlayerNetwork player)
    {
        if (_localPlayer == player)
        {
            UnbindLocalPlayer();
        }
    }

    private void BindLocalPlayer(PlayerNetwork player)
    {
        if (player == _localPlayer)
        {
            return;
        }

        UnbindLocalPlayer();
        _localPlayer = player;

        if (_localPlayer == null)
        {
            RefreshRespawnState(false);
            return;
        }

        _localPlayer.CurrentAmmo.OnValueChanged += OnAmmoChanged;
        _localPlayer.IsAlive.OnValueChanged += OnAliveChanged;
        _localPlayer.RespawnEndTime.OnValueChanged += OnRespawnEndTimeChanged;

        OnAmmoChanged(0, _localPlayer.CurrentAmmo.Value);
        OnAliveChanged(true, _localPlayer.IsAlive.Value);
        OnRespawnEndTimeChanged(0d, _localPlayer.RespawnEndTime.Value);
    }

    private void UnbindLocalPlayer()
    {
        if (_localPlayer != null)
        {
            _localPlayer.CurrentAmmo.OnValueChanged -= OnAmmoChanged;
            _localPlayer.IsAlive.OnValueChanged -= OnAliveChanged;
            _localPlayer.RespawnEndTime.OnValueChanged -= OnRespawnEndTimeChanged;
        }

        _localPlayer = null;

        if (_ammoText != null)
        {
            _ammoText.text = "Ammo: -";
        }

        if (_respawnText != null)
        {
            _respawnText.text = string.Empty;
        }

        RefreshRespawnState(false);
    }

    private void OnAmmoChanged(int oldValue, int newValue)
    {
        if (_ammoText != null && _localPlayer != null)
        {
            _ammoText.text = $"Ammo: {newValue}/{_localPlayer.MaxAmmo}";
        }
    }

    private void OnAliveChanged(bool oldValue, bool newValue)
    {
        RefreshRespawnState(!newValue);

        if (newValue && _respawnText != null)
        {
            _respawnText.text = string.Empty;
        }
    }

    private void OnRespawnEndTimeChanged(double oldValue, double newValue)
    {
        if (_localPlayer == null || _localPlayer.IsAlive.Value)
        {
            return;
        }

        RefreshRespawnState(true);
    }

    private void RefreshRespawnState(bool shouldShow)
    {
        if (_respawnPanel != null)
        {
            _respawnPanel.SetActive(shouldShow);
        }
        else if (_respawnText != null)
        {
            _respawnText.gameObject.SetActive(shouldShow);
        }
    }

    private void TryBindExistingLocalPlayer()
    {
        PlayerNetwork[] players = FindObjectsOfType<PlayerNetwork>();

        foreach (PlayerNetwork player in players)
        {
            if (player != null && player.IsOwner)
            {
                BindLocalPlayer(player);
                break;
            }
        }
    }
}
