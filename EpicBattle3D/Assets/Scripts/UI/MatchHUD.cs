using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Live score readout plus the end-of-match results screen.
///
/// The interstitial ad sits on the buttons of the results screen — the gap
/// between two matches. That is the moment Yandex recommends for fullscreen
/// ads, and it never interrupts a fight in progress. If no ad is available the
/// button behaves exactly as if there were no advertising at all.
/// </summary>
public class MatchHUD : MonoBehaviour
{
    public GameObject player;

    Text scoreText;
    GameObject resultsPanel;
    Text resultsText;
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

    void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("MatchHUD", 1);
        Transform root = canvas.transform;

        Vector2 topLeft = new Vector2(0f, 1f);

        UIFactory.CreateImage(root, "ScoreBackground", new Color(0f, 0f, 0f, 0.45f),
                              topLeft, topLeft, new Vector2(40f, -40f), new Vector2(430f, 74f));

        scoreText = UIFactory.CreateText(root, "ScoreText", "", 24, TextAnchor.UpperLeft, Color.white,
                                         topLeft, topLeft, new Vector2(56f, -52f), new Vector2(410f, 60f));

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

        resultsText = UIFactory.CreateText(resultsPanel.transform, "ResultsText", "", 52,
                                           TextAnchor.MiddleCenter, Color.white,
                                           centre, centre, new Vector2(0f, 120f), new Vector2(900f, 200f));

        UIFactory.CreateButton(resultsPanel.transform, "RestartButton", Localization.Get("play_again"), 30,
                               centre, centre, new Vector2(0f, -40f), new Vector2(340f, 70f),
                               () => ShowAdThen(() =>
                               {
                                   if (MatchManager.Instance != null) MatchManager.Instance.RestartMatch();
                               }));

        UIFactory.CreateButton(resultsPanel.transform, "MenuButton", Localization.Get("to_menu"), 30,
                               centre, centre, new Vector2(0f, -130f), new Vector2(340f, 70f),
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

        bool playerWon = winner == player;
        resultsText.text = Localization.Get(playerWon ? "victory" : "defeat")
                           + "\n\n" + Localization.Get("winner") + ": " + DisplayName(winner)
                           + " (" + winnerScore + ")";

        resultsPanel.SetActive(true);
        FirstPersonController.LockCursor(false);
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
