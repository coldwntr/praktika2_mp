using TMPro;
using FishNet;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private GameObject _menuPanel;

    public static string PlayerNickname { get; private set; } = "Player";

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

        Debug.Log("ConnectionUI StartAsHost: starting FishNet server, then local client. No custom player spawn is performed here.");
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();
                                             
        if (_menuPanel != null)
            _menuPanel.SetActive(false);
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

        Debug.Log("ConnectionUI StartAsClient: starting FishNet client only. No custom player spawn is performed here.");
        InstanceFinder.ClientManager.StartConnection();
        if (_menuPanel != null)
            _menuPanel.SetActive(false);
    }

    private void SaveNickname()
    {
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Player" : rawValue.Trim();
        Debug.Log($"Nickname saved: {PlayerNickname}"); 
    }

    private Mouse _mouse;
    void Start()
    {
        _mouse = Mouse.current;
    }

    void Update()
    {
        if (_mouse != null)
        {
            if (IsTypingIntoInput())
            {
                return;
            }

            if (Keyboard.current.hKey.wasPressedThisFrame)
                StartAsHost();
            if (Keyboard.current.cKey.wasPressedThisFrame)
                StartAsClient();
        }
    }

    private bool IsTypingIntoInput()
    {
        if (_nicknameInput != null && _nicknameInput.isFocused)
        {
            return true;
        }

        if (EventSystem.current == null)
        {
            return false;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
        {
            return false;
        }

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
