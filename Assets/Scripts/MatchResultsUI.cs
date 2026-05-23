using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchResultsUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _resultsText;

    private Image _backgroundImage;

    private void Awake()
    {
        EnsureUiReferences();
        SanitizePanelReference();
        EnsureBackground();
        ConfigureResultsPanelLayout();
        SetPanelActive(false);
        GameStateManager.StateChanged += HandleStateChanged;
        GameStateManager.TimersChanged += HandleTimersChanged;
    }

    private void OnDestroy()
    {
        GameStateManager.StateChanged -= HandleStateChanged;
        GameStateManager.TimersChanged -= HandleTimersChanged;
    }

    private void Update()
    {
        if (Application.isBatchMode)
        {
            SetPanelActive(false);
            return;
        }

        RefreshVisibility();
    }

    private void HandleStateChanged(GameState state)
    {
        RefreshVisibility(state);
    }

    private void HandleTimersChanged()
    {
        RefreshVisibility();
    }

    private void RefreshVisibility(GameState? forcedState = null)
    {
        GameStateManager manager = GameStateManager.GetOrFind();
        GameState state = forcedState ?? (manager != null ? manager.CurrentState : GameState.Lobby);
        bool shouldShow = manager != null &&
                          (state == GameState.Results || manager.ResultsTimer > 0.01f);

        SetPanelActive(shouldShow);

        if (!shouldShow || _resultsText == null)
            return;

        LayoutResultsText();
        _resultsText.text = MatchResultsFormatter.BuildResultsText();
    }

    private void SetPanelActive(bool isActive)
    {
        if (_panel == null || _panel == gameObject)
        {
            if (_resultsText != null)
                _resultsText.gameObject.SetActive(isActive);
            return;
        }

        _panel.SetActive(isActive);

        if (isActive)
        {
            _panel.transform.SetAsLastSibling();
            if (_backgroundImage != null)
                _backgroundImage.enabled = true;
        }
    }

    private void SanitizePanelReference()
    {
        if (_panel == gameObject)
            _panel = null;
    }

    private void EnsureBackground()
    {
        if (_panel == null)
            return;

        _backgroundImage = _panel.GetComponent<Image>();
        if (_backgroundImage == null)
            _backgroundImage = _panel.AddComponent<Image>();

        _backgroundImage.color = new Color(0f, 0f, 0f, 0.82f);
        _backgroundImage.raycastTarget = false;
    }

    private void ConfigureResultsPanelLayout()
    {
        if (_panel == null)
            return;

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        if (panelRect == null)
            return;

        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;
    }

    private void LayoutResultsText()
    {
        if (_resultsText == null)
            return;

        RectTransform textRect = _resultsText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(900f, 520f);

        _resultsText.fontSize = 28f;
        _resultsText.alignment = TextAlignmentOptions.Center;
        _resultsText.color = Color.white;
    }

    private void EnsureUiReferences()
    {
        if (_panel == null)
        {
            Transform existing = transform.Find("MatchResultsUI");
            if (existing != null)
                _panel = existing.gameObject;
        }

        if (_panel == null)
        {
            Transform existing = transform.Find("ResultsPanel");
            if (existing != null)
                _panel = existing.gameObject;
        }

        if (_panel == null)
            _panel = CreateResultsPanel();

        if (_resultsText == null && _panel != null)
        {
            Transform resultsTransform = _panel.transform.Find("ResultText");
            if (resultsTransform == null)
                resultsTransform = _panel.transform.Find("ResultsText");

            if (resultsTransform != null)
                _resultsText = resultsTransform.GetComponent<TMP_Text>();

            if (_resultsText == null)
                _resultsText = _panel.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private GameObject CreateResultsPanel()
    {
        GameObject panelObject = new GameObject("ResultsPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.82f);
        image.raycastTarget = false;

        GameObject textObject = new GameObject("ResultsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 40f);
        textRect.offsetMax = new Vector2(-40f, -40f);

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 28f;
        text.color = Color.white;
        text.text = "Results";

        _resultsText = text;
        panelObject.SetActive(false);
        return panelObject;
    }
}
