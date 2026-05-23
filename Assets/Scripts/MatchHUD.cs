using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class MatchHUD : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _scoreboardText;
    [SerializeField] private TMP_Text _roundTimerText;

    private void Awake()
    {
        EnsureUiReferences();
        GameStateManager.StateChanged += HandleStateChanged;
        GameStateManager.TimersChanged += HandleTimersChanged;
        SanitizePanelReference();
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

        GameStateManager manager = GameStateManager.GetOrFind();

        RefreshRoundTimerText(manager);
        RefreshScoreboardText();

        if (manager == null && _roundTimerText != null)
            _roundTimerText.text = "Connect to server (Client / Host)";
    }

    private void HandleStateChanged(GameState state)
    {
        RefreshRoundTimerText(GameStateManager.GetOrFind());
    }

    private void HandleTimersChanged()
    {
        RefreshRoundTimerText(GameStateManager.GetOrFind());
    }

    private void RefreshRoundTimerText(GameStateManager manager)
    {
        if (_roundTimerText == null)
            return;

        if (manager == null)
        {
            _roundTimerText.text = string.Empty;
            return;
        }

        switch (manager.CurrentState)
        {
            case GameState.Lobby:
            case GameState.Countdown:
                _roundTimerText.text =
                    $"Waiting for players: {manager.ConnectedPlayers} / {manager.RequiredPlayers}";
                break;

            case GameState.Match:
                _roundTimerText.text = $"Match time: {Mathf.CeilToInt(manager.MatchTimer)}s";
                break;

            case GameState.Results:
                _roundTimerText.text = $"Restarting round in: {Mathf.CeilToInt(manager.ResultsTimer)}s";
                break;

            default:
                _roundTimerText.text = string.Empty;
                break;
        }
    }

    private void RefreshScoreboardText()
    {
        if (_scoreboardText == null)
            return;

        GameStateManager manager = GameStateManager.GetOrFind();
        if (manager != null && manager.CurrentState == GameState.Results)
        {
            _scoreboardText.text = MatchResultsFormatter.BuildResultsText();
            return;
        }

        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);
        if (players.Length == 0)
        {
            _scoreboardText.text = "Score:\n(no players)";
            return;
        }

        List<PlayerNetwork> sortedPlayers = new List<PlayerNetwork>(players);
        sortedPlayers.Sort(ComparePlayersForScoreboard);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Score:");

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            PlayerNetwork player = sortedPlayers[i];
            if (player == null)
                continue;

            builder.AppendLine(FormatScoreboardLine(player));
        }

        _scoreboardText.text = builder.ToString().TrimEnd();
    }

    private static int ComparePlayersForScoreboard(PlayerNetwork left, PlayerNetwork right)
    {
        int scoreCompare = right.Score.CompareTo(left.Score);
        if (scoreCompare != 0)
            return scoreCompare;

        int killsCompare = right.Kills.CompareTo(left.Kills);
        if (killsCompare != 0)
            return killsCompare;

        return string.Compare(left.Nickname, right.Nickname, System.StringComparison.Ordinal);
    }

    private static string FormatScoreboardLine(PlayerNetwork player)
    {
        string nickname = string.IsNullOrWhiteSpace(player.Nickname) ? "Player" : player.Nickname;
        return $"{nickname} — {player.Kills} kills";
    }

    private void SetPanelActive(bool isActive)
    {
        if (_panel == null || _panel == gameObject)
            return;

        _panel.SetActive(isActive);
    }

    private void SanitizePanelReference()
    {
        if (_panel == gameObject)
            _panel = null;
    }

    private void EnsureUiReferences()
    {
        if (_scoreboardText != null && _roundTimerText != null)
            return;

        if (_panel == null)
        {
            Transform existingPanel = transform.Find("MatchHudPanel");
            if (existingPanel != null)
                _panel = existingPanel.gameObject;
        }

        if (_panel == null)
            _panel = CreateDefaultPanel();

        if (_scoreboardText == null && _panel != null)
        {
            Transform scoreboardTransform = _panel.transform.Find("ScoreboardText");
            if (scoreboardTransform != null)
                _scoreboardText = scoreboardTransform.GetComponent<TMP_Text>();
        }

        if (_roundTimerText == null && _panel != null)
        {
            Transform roundTimerTransform = _panel.transform.Find("RoundTimerText");
            if (roundTimerTransform != null)
                _roundTimerText = roundTimerTransform.GetComponent<TMP_Text>();
        }

        Transform parent = _panel != null ? _panel.transform : transform;

        if (_scoreboardText == null)
        {
            _scoreboardText = CreateText(parent, "ScoreboardText", TextAnchor.UpperLeft,
                new Vector2(20f, -20f), new Vector2(320f, 220f), 20f, "Score:\n(no players)");
        }

        if (_roundTimerText == null)
        {
            _roundTimerText = CreateText(parent, "RoundTimerText", TextAnchor.UpperCenter,
                new Vector2(0f, -20f), new Vector2(420f, 40f), 24f, "Waiting for players...");
        }
    }

    private GameObject CreateDefaultPanel()
    {
        GameObject panelObject = new GameObject("MatchHudPanel", typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        panelObject.SetActive(true);
        return panelObject;
    }

    private static TMP_Text CreateText(Transform parent, string name, TextAnchor anchor,
        Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, string defaultText)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();

        switch (anchor)
        {
            case TextAnchor.UpperLeft:
                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = new Vector2(0f, 1f);
                textRect.pivot = new Vector2(0f, 1f);
                break;

            default:
                textRect.anchorMin = new Vector2(0.5f, 1f);
                textRect.anchorMax = new Vector2(0.5f, 1f);
                textRect.pivot = new Vector2(0.5f, 1f);
                break;
        }

        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = sizeDelta;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = anchor == TextAnchor.UpperLeft ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top;
        text.fontSize = fontSize;
        text.text = defaultText;
        return text;
    }
}
