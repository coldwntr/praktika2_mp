using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private TMP_Text _hpText;
    [SerializeField] private GameObject _modelRoot;

    private void Awake()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        if (_modelRoot == null)
        {
            Transform modelTransform = transform.Find("ModelRoot");
            if (modelTransform != null)
            {
                _modelRoot = modelTransform.gameObject;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        _playerNetwork.Nickname.OnValueChanged += OnNicknameChanged;
        _playerNetwork.HP.OnValueChanged += OnHpChanged;
        _playerNetwork.IsAlive.OnValueChanged += OnAliveChanged;

        OnNicknameChanged(default, _playerNetwork.Nickname.Value);
        OnHpChanged(0, _playerNetwork.HP.Value);
        OnAliveChanged(true, _playerNetwork.IsAlive.Value);
    }

    public override void OnNetworkDespawn()
    {
        _playerNetwork.Nickname.OnValueChanged -= OnNicknameChanged;
        _playerNetwork.HP.OnValueChanged -= OnHpChanged;
        _playerNetwork.IsAlive.OnValueChanged -= OnAliveChanged;
    }

    private void OnNicknameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        if (_nicknameText != null)
        {
            _nicknameText.text = newValue.ToString();
        }
    }

    private void OnHpChanged(int oldValue, int newValue)
    {
        if (_hpText != null)
        {
            _hpText.text = $"HP: {newValue}";
        }
    }

    private void OnAliveChanged(bool oldValue, bool newValue)
    {
        if (_modelRoot != null)
        {
            _modelRoot.SetActive(newValue);
        }
    }
}
