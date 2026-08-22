using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Live score readout plus the end-of-match results screen.
/// </summary>
public class MatchHUD : MonoBehaviour
{
    public GameObject player;

    Text scoreText;
    GameObject resultsPanel;
    Text resultsText;

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
        background.color = new Color(0f, 0f, 0f, 0.85f);
        UIFactory.Stretch(resultsPanel);

        Vector2 centre = new Vector2(0.5f, 0.5f);

        resultsText = UIFactory.CreateText(resultsPanel.transform, "ResultsText", "", 52,
                                           TextAnchor.MiddleCenter, Color.white,
                                           centre, centre, new Vector2(0f, 120f), new Vector2(900f, 200f));

        UIFactory.CreateButton(resultsPanel.transform, "RestartButton", "Играть снова", 30,
                               centre, centre, new Vector2(0f, -40f), new Vector2(340f, 70f),
                               () => { if (MatchManager.Instance != null) MatchManager.Instance.RestartMatch(); });

        UIFactory.CreateButton(resultsPanel.transform, "MenuButton", "В меню", 30,
                               centre, centre, new Vector2(0f, -130f), new Vector2(340f, 70f),
                               () => { if (MatchManager.Instance != null) MatchManager.Instance.ReturnToMenu(); });

        resultsPanel.SetActive(false);
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
            ? leader.name + " — " + MatchManager.Instance.GetScore(leader)
            : "нет";

        scoreText.text = "Ваши фраги: " + playerScore + "  /  до победы: " + MatchManager.Instance.fragLimit
                         + "\nЛидер: " + leaderLine;
    }

    void ShowResults(string winnerName, int winnerScore)
    {
        if (resultsPanel == null) return;

        bool playerWon = player != null && winnerName == player.name;
        resultsText.text = (playerWon ? "ПОБЕДА" : "ПОРАЖЕНИЕ")
                           + "\n\nПобедитель: " + winnerName + " (" + winnerScore + ")";

        resultsPanel.SetActive(true);
        FirstPersonController.LockCursor(false);
    }
}
