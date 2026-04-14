using TMPro;
using Unity.Netcode;
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

        if (_playerNetwork == null || _playerNetwork.IsAlive.Value || _playerNetwork.RespawnEndTime.Value <= 0d)
        {
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        double remainingTime = _playerNetwork.RespawnEndTime.Value - NetworkManager.Singleton.ServerTime.Time;
        remainingTime = Mathf.Max(0f, (float)remainingTime);

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
        if (_playerNetwork == null || !_playerNetwork.IsSpawned)
        {
            return;
        }

        bool shouldBeOwnerHud = _playerNetwork.IsOwner;
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

        _playerNetwork.CurrentAmmo.OnValueChanged += OnAmmoChanged;
        _playerNetwork.IsAlive.OnValueChanged += OnAliveChanged;
        _playerNetwork.RespawnEndTime.OnValueChanged += OnRespawnEndTimeChanged;

        OnAmmoChanged(0, _playerNetwork.CurrentAmmo.Value);
        OnAliveChanged(true, _playerNetwork.IsAlive.Value);
        OnRespawnEndTimeChanged(0d, _playerNetwork.RespawnEndTime.Value);
    }

    private void Unbind()
    {
        if (_subscribed && _playerNetwork != null)
        {
            _playerNetwork.CurrentAmmo.OnValueChanged -= OnAmmoChanged;
            _playerNetwork.IsAlive.OnValueChanged -= OnAliveChanged;
            _playerNetwork.RespawnEndTime.OnValueChanged -= OnRespawnEndTimeChanged;
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

    private void OnAmmoChanged(int oldValue, int newValue)
    {
        if (_ammoText != null && _playerNetwork != null)
        {
            _ammoText.text = $"Bullets: {newValue}/{_playerNetwork.MaxAmmo}";
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
        if (_playerNetwork == null || _playerNetwork.IsAlive.Value)
        {
            return;
        }

        RefreshRespawnState(true);
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
