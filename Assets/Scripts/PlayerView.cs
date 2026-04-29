using TMPro;
using FishNet.Object;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private TMP_Text _hpText;
    [SerializeField] private GameObject _modelRoot;

    private Renderer[] _cachedRenderers;

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

        if (_modelRoot == gameObject)
        {
            // Never disable the root network object, otherwise respawn logic and other network behaviours stop running.
            _modelRoot = null;
        }

        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public override void OnStartClient()
    {
        if (_playerNetwork == null)
            return;

        Debug.Log($"PlayerView OnStartClient object={name} IsOwner={IsOwner} Owner={Owner} IsAlive={_playerNetwork.IsAlive} modelRootAssigned={_modelRoot != null} rendererCount={(_cachedRenderers != null ? _cachedRenderers.Length : 0)}");
        _playerNetwork.NicknameChanged += HandleNicknameChanged;
        _playerNetwork.HpChanged += HandleHpChanged;
        _playerNetwork.AliveChanged += HandleAliveChanged;
        RefreshView();
    }

    public override void OnStopClient()
    {
        if (_playerNetwork == null)
            return;

        _playerNetwork.NicknameChanged -= HandleNicknameChanged;
        _playerNetwork.HpChanged -= HandleHpChanged;
        _playerNetwork.AliveChanged -= HandleAliveChanged;
    }

    private void RefreshView()
    {
        HandleNicknameChanged(_playerNetwork.Nickname);
        HandleHpChanged(_playerNetwork.HP);
        HandleAliveChanged(_playerNetwork.IsAlive);
    }

    private void HandleNicknameChanged(string nickname)
    {
        if (_nicknameText != null)
            _nicknameText.text = nickname;
    }

    private void HandleHpChanged(int hp)
    {
        if (_hpText != null)
            _hpText.text = $"HP: {hp}";
    }

    private void HandleAliveChanged(bool isAlive)
    {
        Debug.Log($"PlayerView HandleAliveChanged object={name} IsOwner={IsOwner} isAlive={isAlive} modelRootAssigned={_modelRoot != null} rendererCount={(_cachedRenderers != null ? _cachedRenderers.Length : 0)}");

        if (_modelRoot != null)
        {
            _modelRoot.SetActive(isAlive);
            return;
        }

        SetVisualState(isAlive);
    }

    private void SetVisualState(bool isVisible)
    {
        if (_cachedRenderers != null)
        {
            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                Renderer rendererComponent = _cachedRenderers[i];
                if (rendererComponent == null)
                {
                    continue;
                }

                rendererComponent.enabled = isVisible;
            }
        }
    }
}
