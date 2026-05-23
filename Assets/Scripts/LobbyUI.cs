using TMPro;
using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _statusText;

    private void Awake()
    {
        EnsureUiReferences();
        SanitizePanelReference();
    }

    private void Update()
    {
        if (Application.isBatchMode)
        {
            SetPanelActive(false);
            return;
        }

        GameStateManager manager = GameStateManager.GetOrFind();
        bool shouldShow = manager != null &&
                          (manager.CurrentState == GameState.Lobby || manager.CurrentState == GameState.Countdown);

        SetPanelActive(shouldShow);

        if (!shouldShow || _statusText == null || manager == null)
            return;

        _statusText.text = $"Waiting for players: {manager.ConnectedPlayers} / {manager.RequiredPlayers}";
    }

    private void SetPanelActive(bool isActive)
    {
        if (_panel == null)
            return;

        if (_statusText != null)
            _statusText.gameObject.SetActive(isActive);
        else
            _panel.SetActive(isActive);
    }

    private void SanitizePanelReference()
    {
        if (_panel == null)
            return;

        if (_panel.GetComponent<ConnectionUI>() != null || _panel.GetComponentInChildren<TMP_InputField>(true) != null)
            _panel = null;
    }

    private void EnsureUiReferences()
    {
        if (_panel != null && _statusText != null)
            return;

        if (_panel == null)
        {
            Transform existing = transform.Find("LobbyUI");
            if (existing != null)
                _panel = existing.gameObject;
        }

        if (_panel == null)
        {
            Transform existing = transform.Find("LobbyPanel");
            if (existing != null)
                _panel = existing.gameObject;
        }

        if (_panel == null)
            _panel = CreatePanel();

        if (_statusText == null && _panel != null)
        {
            Transform statusTransform = _panel.transform.Find("StatusText");
            if (statusTransform != null)
                _statusText = statusTransform.GetComponent<TMP_Text>();

            if (_statusText == null)
                _statusText = _panel.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private GameObject CreatePanel()
    {
        GameObject panelObject = new GameObject("LobbyPanel", typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -80f);
        rectTransform.sizeDelta = new Vector2(420f, 60f);

        GameObject textObject = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.text = "Waiting for players: 0 / 2";

        _statusText = text;
        panelObject.SetActive(false);
        return panelObject;
    }
}
