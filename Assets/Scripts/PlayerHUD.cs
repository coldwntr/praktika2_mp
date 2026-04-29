using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private TMP_Text _ammoText;
    [SerializeField] private TMP_Text _respawnText;

    private Canvas _canvas;
    private GraphicRaycaster _graphicRaycaster;
    private bool _subscribed;
    private bool _isOwnerHud;

    private void Awake()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponentInParent<PlayerNetwork>();
        }

        if (_ammoText == null)
        {
            _ammoText = FindTextByName("Bullets");
        }

        if (_respawnText == null)
        {
            _respawnText = FindTextByName("DeathTimer");
        }

        _canvas = GetComponent<Canvas>();
        _graphicRaycaster = GetComponent<GraphicRaycaster>();
    }

    private void OnEnable()
    {
        TryInitializeHud();
    }

    private void Update()
    {
        if (!_isOwnerHud)
        {
            TryInitializeHud();
            return;
        }

        if (_playerNetwork == null || _playerNetwork.IsAlive || _playerNetwork.RespawnEndTime <= 0d)
        {
            return;
        }

        double remainingTime = _playerNetwork.GetRespawnRemainingTime();

        if (_respawnText != null)
        {
            _respawnText.text = $"Respawn in: {remainingTime:0.0}s";
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void TryInitializeHud()
    {
        if (_playerNetwork == null || !_playerNetwork.IsClientInitialized)
        {
            return;
        }

        bool shouldBeOwnerHud = _playerNetwork.Owner.IsLocalClient;
        SetCanvasEnabled(shouldBeOwnerHud);

        if (!shouldBeOwnerHud)
        {
            Unbind();
            return;
        }

        if (_subscribed)
        {
            return;
        }

        _isOwnerHud = true;
        _subscribed = true;

        _playerNetwork.AmmoChanged += OnAmmoChanged;
        _playerNetwork.AliveChanged += OnAliveChanged;

        OnAmmoChanged(_playerNetwork.CurrentAmmo);
        OnAliveChanged(_playerNetwork.IsAlive);
        RefreshRespawnState(_playerNetwork.IsDeadOrRespawning);
    }

    private void Unbind()
    {
        if (_subscribed && _playerNetwork != null)
        {
            _playerNetwork.AmmoChanged -= OnAmmoChanged;
            _playerNetwork.AliveChanged -= OnAliveChanged;
        }

        _subscribed = false;
        _isOwnerHud = false;

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

    private void OnAmmoChanged(int ammo)
    {
        if (_ammoText != null && _playerNetwork != null)
        {
            _ammoText.text = $"Bullets: {ammo}/{_playerNetwork.MaxAmmo}";
        }
    }

    private void OnAliveChanged(bool isAlive)
    {
        RefreshRespawnState(!isAlive);

        if (isAlive && _respawnText != null)
        {
            _respawnText.text = string.Empty;
        }
    }

    private void RefreshRespawnState(bool shouldShow)
    {
        if (_respawnText != null)
        {
            _respawnText.gameObject.SetActive(shouldShow);
        }
    }

    private void SetCanvasEnabled(bool isEnabled)
    {
        if (_canvas != null)
        {
            _canvas.enabled = isEnabled;
        }

        if (_graphicRaycaster != null)
        {
            _graphicRaycaster.enabled = isEnabled;
        }
    }

    private TMP_Text FindTextByName(string objectName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text textComponent = texts[i];
            if (textComponent != null && textComponent.name == objectName)
            {
                return textComponent;
            }
        }

        return null;
    }
}
