using TMPro;
using FishNet;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class ConnectionUI : MonoBehaviour
{
    private const string DefaultIp = "127.0.0.1";
    private const ushort DefaultPort = 7770;

    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private TMP_InputField _ipInput;
    [SerializeField] private TMP_InputField _portInput;
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _connectionInputsRoot;

    public static string PlayerNickname { get; private set; } = "Player";

    private Mouse _mouse;

    private void Awake()
    {
        EnsureConnectionInputsRoot();
    }

    private void Start()
    {
        _mouse = Mouse.current;
        EnsureCanvasVisible();

        if (Application.isBatchMode && _menuPanel != null)
            _menuPanel.SetActive(false);

        GameStateManager.StateChanged += HandleGameStateChanged;
        RefreshConnectionVisibility();
    }

    private void OnDestroy()
    {
        GameStateManager.StateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState state)
    {
        RefreshConnectionVisibility();
    }

    private void EnsureConnectionInputsRoot()
    {
        if (_connectionInputsRoot != null)
            return;

        Transform connectionRoot = transform.Find("ConnectionUI ");
        if (connectionRoot == null)
            connectionRoot = transform.Find("ConnectionUI");

        if (connectionRoot != null)
            _connectionInputsRoot = connectionRoot.gameObject;
    }

    private void RefreshConnectionVisibility()
    {
        bool connected = IsNetworkingActive();

        if (_menuPanel != null)
            _menuPanel.SetActive(!connected);

        // IP/port live outside _menuPanel — hide for the whole session after connect.
        if (_connectionInputsRoot != null)
            _connectionInputsRoot.SetActive(!connected);
    }

    private static bool IsNetworkingActive()
    {
        if (InstanceFinder.NetworkManager == null)
            return false;

        return InstanceFinder.IsClientStarted || InstanceFinder.IsServerStarted;
    }

    private void EnsureCanvasVisible()
    {
        RectTransform canvasRect = GetComponent<RectTransform>();
        if (canvasRect != null && canvasRect.localScale.sqrMagnitude < 0.001f)
            canvasRect.localScale = Vector3.one;
    }

    public void StartAsHost()
    {
        SaveNickname();
        if (InstanceFinder.NetworkManager == null)
        {
            Debug.LogWarning("FishNet NetworkManager was not found in the scene.");
            return;
        }

        if (InstanceFinder.IsServerStarted || InstanceFinder.IsClientStarted)
        {
            Debug.LogWarning($"StartAsHost ignored because networking is already running. IsServerStarted={InstanceFinder.IsServerStarted} IsClientStarted={InstanceFinder.IsClientStarted}");
            return;
        }

        Debug.Log("ConnectionUI StartAsHost: starting FishNet server, then local client.");
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();

        if (_menuPanel != null)
            _menuPanel.SetActive(false);

        RefreshConnectionVisibility();
    }

    public void StartAsClient()
    {
        SaveNickname();
        if (InstanceFinder.NetworkManager == null)
        {
            Debug.LogWarning("FishNet NetworkManager was not found in the scene.");
            return;
        }

        if (InstanceFinder.IsClientStarted)
        {
            Debug.LogWarning("StartAsClient ignored because FishNet client is already running.");
            return;
        }

        ApplyClientTransportSettings();
        Debug.Log($"ConnectionUI StartAsClient: connecting to {GetIpAddress()}:{GetPort()}");
        InstanceFinder.ClientManager.StartConnection();

        if (_menuPanel != null)
            _menuPanel.SetActive(false);

        RefreshConnectionVisibility();
    }

    private void ApplyClientTransportSettings()
    {
        if (InstanceFinder.TransportManager?.Transport is not Tugboat tugboat)
        {
            Debug.LogWarning("ConnectionUI could not configure Tugboat transport.");
            return;
        }

        tugboat.SetClientAddress(GetIpAddress());
        tugboat.SetPort(GetPort());
    }

    private string GetIpAddress()
    {
        string rawValue = _ipInput != null ? _ipInput.text : string.Empty;
        return string.IsNullOrWhiteSpace(rawValue) ? DefaultIp : rawValue.Trim();
    }

    private ushort GetPort()
    {
        string rawValue = _portInput != null ? _portInput.text : string.Empty;
        if (string.IsNullOrWhiteSpace(rawValue))
            return DefaultPort;

        if (ushort.TryParse(rawValue.Trim(), out ushort parsedPort))
            return parsedPort;

        Debug.LogWarning($"ConnectionUI invalid port '{rawValue}'. Using default port {DefaultPort}.");
        return DefaultPort;
    }

    private void SaveNickname()
    {
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Player" : rawValue.Trim();
        Debug.Log($"Nickname saved: {PlayerNickname}");
    }

    private void Update()
    {
        RefreshConnectionVisibility();

        if (_mouse == null || Application.isBatchMode)
            return;

        if (IsTypingIntoInput())
            return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
            StartAsHost();
        if (Keyboard.current.cKey.wasPressedThisFrame)
            StartAsClient();
    }

    private bool IsTypingIntoInput()
    {
        if (_nicknameInput != null && _nicknameInput.isFocused)
            return true;

        if (_ipInput != null && _ipInput.isFocused)
            return true;

        if (_portInput != null && _portInput.isFocused)
            return true;

        if (EventSystem.current == null)
            return false;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
            return false;

        return selectedObject.GetComponent<TMP_InputField>() != null;
    }

    public void QuitGame()
    {
        if (InstanceFinder.NetworkManager != null)
        {
            if (InstanceFinder.IsClientStarted)
                InstanceFinder.ClientManager.StopConnection();

            if (InstanceFinder.IsServerStarted)
                InstanceFinder.ServerManager.StopConnection(true);
        }

        Debug.Log("Выход из игры");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
