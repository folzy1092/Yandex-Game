using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Match clock, live score readout, and the end-of-match scoreboard.
///
/// The interstitial ad sits on the buttons of the results screen — the gap
/// between two matches. That is the moment Yandex recommends for fullscreen
/// ads, and it never interrupts a fight in progress. If no ad is available the
/// button behaves exactly as if there were no advertising at all.
/// </summary>
public class MatchHUD : MonoBehaviour
{
    public GameObject player;

    /// <summary>Seconds left at which the clock turns red and starts pulsing.</summary>
    public float urgentTime = 30f;

    Text scoreText;
    Text timerText;
    GameObject resultsPanel;
    Text resultsTitle;
    Text resultsBoard;
    bool leavingMatch;

    void Start()
    {
        UIFactory.EnsureEventSystem();
        Build();
        RefreshScore();

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.OnScoreChanged += HandleScoreChanged;
            MatchManager.Instance.OnMatchEnded += ShowResults;
        }
    }

    void OnDestroy()
    {
        if (MatchManager.Instance == null) return;
        MatchManager.Instance.OnScoreChanged -= HandleScoreChanged;
        MatchManager.Instance.OnMatchEnded -= ShowResults;
    }

    void Update()
    {
        if (timerText == null || MatchManager.Instance == null) return;

        float remaining = Mathf.Max(0f, MatchManager.Instance.TimeRemaining);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = minutes + ":" + seconds.ToString("00");

        if (remaining > urgentTime)
        {
            timerText.color = Color.white;
            return;
        }

        // Pulse red over the last stretch so the deadline is impossible to miss.
        float pulse = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
        timerText.color = Color.Lerp(new Color(1f, 0.35f, 0.3f), Color.white, pulse * 0.4f);
    }

    void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("MatchHUD", 1);
        Transform root = canvas.transform;

        Vector2 topLeft = new Vector2(0f, 1f);
        Vector2 topCentre = new Vector2(0.5f, 1f);

        UIFactory.CreateImage(root, "ScoreBackground", new Color(0f, 0f, 0f, 0.45f),
                              topLeft, topLeft, new Vector2(40f, -40f), new Vector2(430f, 74f));

        scoreText = UIFactory.CreateText(root, "ScoreText", "", 24, TextAnchor.UpperLeft, Color.white,
                                         topLeft, topLeft, new Vector2(56f, -52f), new Vector2(410f, 60f));

        UIFactory.CreateImage(root, "TimerBackground", new Color(0f, 0f, 0f, 0.5f),
                              topCentre, topCentre, new Vector2(0f, -40f), new Vector2(180f, 62f));

        timerText = UIFactory.CreateText(root, "TimerText", "5:00", 40, TextAnchor.MiddleCenter,
                                         Color.white, topCentre, topCentre,
                                         new Vector2(0f, -71f), new Vector2(180f, 62f));

        BuildResultsPanel(root);
    }

    void BuildResultsPanel(Transform root)
    {
        resultsPanel = new GameObject("ResultsPanel");
        resultsPanel.transform.SetParent(root, false);

        var background = resultsPanel.AddComponent<Image>();
        background.sprite = UIFactory.BlankSprite;
        background.color = new Color(0f, 0f, 0f, 0.85f);
        UIFactory.Stretch(resultsPanel);

        Vector2 centre = new Vector2(0.5f, 0.5f);

        resultsTitle = UIFactory.CreateText(resultsPanel.transform, "ResultsTitle", "", 52,
                                            TextAnchor.MiddleCenter, Color.white,
                                            centre, centre, new Vector2(0f, 300f), new Vector2(900f, 90f));

        UIFactory.CreateImage(resultsPanel.transform, "BoardBackground", new Color(1f, 1f, 1f, 0.06f),
                              centre, centre, new Vector2(0f, 60f), new Vector2(560f, 380f));

        resultsBoard = UIFactory.CreateText(resultsPanel.transform, "ResultsBoard", "", 28,
                                            TextAnchor.UpperLeft, Color.white,
                                            centre, centre, new Vector2(0f, 60f), new Vector2(510f, 350f));

        UIFactory.CreateButton(resultsPanel.transform, "RestartButton", Localization.Get("play_again"), 30,
                               centre, centre, new Vector2(0f, -190f), new Vector2(340f, 70f),
                               () => ShowAdThen(() =>
                               {
                                   if (MatchManager.Instance != null) MatchManager.Instance.RestartMatch();
                               }));

        UIFactory.CreateButton(resultsPanel.transform, "MenuButton", Localization.Get("to_menu"), 30,
                               centre, centre, new Vector2(0f, -275f), new Vector2(340f, 70f),
                               () => ShowAdThen(() =>
                               {
                                   if (MatchManager.Instance != null) MatchManager.Instance.ReturnToMenu();
                               }));

        resultsPanel.SetActive(false);
    }

    /// <summary>
    /// Runs an interstitial, then does the thing the player asked for. The action
    /// runs whether or not an ad appeared, so a missing or blocked ad can never
    /// strand the player on the results screen.
    /// </summary>
    void ShowAdThen(Action continueAction)
    {
        if (leavingMatch) return;
        leavingMatch = true;

        YandexAds.ShowFullscreen(_ => continueAction());
    }

    void HandleScoreChanged(GameObject combatant, int score)
    {
        RefreshScore();
    }

    void RefreshScore()
    {
        if (scoreText == null || MatchManager.Instance == null) return;

        int playerScore = player != null ? MatchManager.Instance.GetScore(player) : 0;

        GameObject leader = MatchManager.Instance.GetLeader();
        string leaderLine = leader != null
            ? DisplayName(leader) + " — " + MatchManager.Instance.GetScore(leader)
            : Localization.Get("nobody");

        scoreText.text = Localization.Get("your_frags") + ": " + playerScore
                         + "  /  " + Localization.Get("to_win") + ": " + MatchManager.Instance.fragLimit
                         + "\n" + Localization.Get("leader") + ": " + leaderLine;
    }

    void ShowResults(GameObject winner, int winnerScore)
    {
        if (resultsPanel == null) return;

        bool ranOutOfTime = MatchManager.Instance != null && MatchManager.Instance.TimeRemaining <= 0f;

        if (winner == null)
            resultsTitle.text = Localization.Get("draw");
        else if (winner == player)
            resultsTitle.text = Localization.Get("victory");
        else
            resultsTitle.text = Localization.Get("defeat");

        if (ranOutOfTime)
            resultsTitle.text += "  ·  " + Localization.Get("time_up");

        resultsBoard.text = BuildScoreboard();

        resultsPanel.SetActive(true);
        FirstPersonController.LockCursor(false);
    }

    /// <summary>
    /// The full standings, not just the winner — with several bots in play, where
    /// you actually placed is the interesting part.
    /// </summary>
    string BuildScoreboard()
    {
        if (MatchManager.Instance == null) return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine(Localization.Get("results"));
        builder.AppendLine();

        var standings = MatchManager.Instance.GetStandings();
        for (int i = 0; i < standings.Count; i++)
        {
            MatchManager.Standing standing = standings[i];
            bool isPlayer = standing.combatant == player;

            string line = (i + 1) + ".  " + DisplayName(standing.combatant) + "   —   " + standing.score;
            // Unity's built-in rich text is enough to make the player's own row stand out.
            builder.AppendLine(isPlayer ? "<b>" + line + "</b>" : line);
        }

        return builder.ToString();
    }

    /// <summary>
    /// GameObject names are kept in English so scenes stay readable; the player
    /// sees them translated.
    /// </summary>
    string DisplayName(GameObject combatant)
    {
        if (combatant == null) return Localization.Get("nobody");
        if (combatant == player) return Localization.Get("player");

        if (combatant.name.StartsWith("Bot"))
            return Localization.Get("bot") + combatant.name.Substring(3);

        return combatant.name;
    }
}
