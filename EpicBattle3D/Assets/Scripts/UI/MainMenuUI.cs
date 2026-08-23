using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Start screen: pick how many bots to fight and how many frags win the match,
/// then load the arena.
///
/// The menu waits for the SDK to report the player's language before drawing
/// anything. Yandex requirement 2.14 wants the language settled during startup
/// rather than part-way through a session, and a short wait here is what
/// guarantees that.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public string arenaSceneName = "Pool";
    public float languageWaitTimeout = 2f;

    Slider botSlider;
    Slider fragSlider;
    Text botLabel;
    Text fragLabel;
    Text loadingText;
    Canvas canvas;
    Button[] difficultyButtons;

    void Start()
    {
        FirstPersonController.LockCursor(false);
        UIFactory.EnsureEventSystem();

        canvas = UIFactory.CreateCanvas("MainMenu", 0);
        BuildBackdrop();

        StartCoroutine(WaitForLanguageThenBuild());
    }

    IEnumerator WaitForLanguageThenBuild()
    {
        float deadline = Time.realtimeSinceStartup + languageWaitTimeout;

        while (!Localization.IsResolved && Time.realtimeSinceStartup < deadline)
            yield return null;

        // Nothing answered in time — carry on with the default language rather
        // than leaving the player staring at a loading screen.
        if (!Localization.IsResolved) Localization.MarkResolved();

        if (loadingText != null) Destroy(loadingText.gameObject);
        BuildMenu();
    }

    void BuildBackdrop()
    {
        Vector2 centre = new Vector2(0.5f, 0.5f);

        var background = UIFactory.CreateImage(canvas.transform, "Background", new Color(0.07f, 0.09f, 0.12f),
                                               centre, centre, Vector2.zero, Vector2.zero);
        UIFactory.Stretch(background.gameObject);

        // The title is language-dependent (ru/en spelling differs), so it is
        // drawn once the language is resolved rather than here.
        loadingText = UIFactory.CreateText(canvas.transform, "Loading", Localization.Get("loading"), 30,
                                           TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.7f),
                                           centre, centre, Vector2.zero, new Vector2(600f, 60f));
    }

    void BuildMenu()
    {
        Transform root = canvas.transform;
        Vector2 centre = new Vector2(0.5f, 0.5f);

        UIFactory.CreateText(root, "Title", Localization.Get("game_title"), 72, TextAnchor.MiddleCenter,
                             new Color(0.35f, 0.70f, 1f),
                             centre, centre, new Vector2(0f, 300f), new Vector2(900f, 100f));

        UIFactory.CreateText(root, "Subtitle", Localization.Get("subtitle"), 26, TextAnchor.MiddleCenter,
                             new Color(0.7f, 0.75f, 0.8f),
                             centre, centre, new Vector2(0f, 230f), new Vector2(900f, 50f));

        botLabel = UIFactory.CreateText(root, "BotLabel", "", 30, TextAnchor.MiddleLeft, Color.white,
                                        centre, centre, new Vector2(-300f, 100f), new Vector2(600f, 45f));

        botSlider = UIFactory.CreateSlider(root, "BotSlider",
                                           MatchSettings.MinBots, MatchSettings.MaxBots,
                                           MatchSettings.BotCount, true,
                                           centre, centre, new Vector2(0f, 55f), new Vector2(600f, 30f));
        botSlider.onValueChanged.AddListener(_ => RefreshLabels());

        fragLabel = UIFactory.CreateText(root, "FragLabel", "", 30, TextAnchor.MiddleLeft, Color.white,
                                         centre, centre, new Vector2(-300f, -25f), new Vector2(600f, 45f));

        fragSlider = UIFactory.CreateSlider(root, "FragSlider",
                                            MatchSettings.MinFrags, MatchSettings.MaxFrags,
                                            MatchSettings.FragLimit, true,
                                            centre, centre, new Vector2(0f, -70f), new Vector2(600f, 30f));
        fragSlider.onValueChanged.AddListener(_ => RefreshLabels());

        BuildDifficultyRow(root, centre);

        UIFactory.CreateButton(root, "StartButton", Localization.Get("play"), 36,
                               centre, centre, new Vector2(0f, -260f), new Vector2(360f, 80f), StartMatch);

        UIFactory.CreateText(root, "Controls", Localization.Get("controls"),
                             22, TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.7f),
                             centre, centre, new Vector2(0f, -370f), new Vector2(1100f, 80f));

        RefreshLabels();
        RefreshDifficultyButtons();
    }

    /// <summary>
    /// Three buttons rather than a slider: difficulty is a named choice, and a
    /// slider would make the player guess what position means what.
    /// </summary>
    void BuildDifficultyRow(Transform root, Vector2 centre)
    {
        UIFactory.CreateText(root, "DifficultyLabel", Localization.Get("difficulty"), 30,
                             TextAnchor.MiddleLeft, Color.white,
                             centre, centre, new Vector2(-300f, -140f), new Vector2(600f, 45f));

        var difficulties = new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };
        var keys = new[] { "easy", "normal", "hard" };

        difficultyButtons = new Button[difficulties.Length];

        const float buttonWidth = 190f;
        const float gap = 10f;
        float totalWidth = difficulties.Length * buttonWidth + (difficulties.Length - 1) * gap;
        float startX = -totalWidth * 0.5f + buttonWidth * 0.5f;

        for (int i = 0; i < difficulties.Length; i++)
        {
            BotDifficulty difficulty = difficulties[i];

            difficultyButtons[i] = UIFactory.CreateButton(root, "Difficulty" + difficulty,
                Localization.Get(keys[i]), 26, centre, centre,
                new Vector2(startX + i * (buttonWidth + gap), -190f),
                new Vector2(buttonWidth, 56f),
                () =>
                {
                    MatchSettings.Difficulty = difficulty;
                    RefreshDifficultyButtons();
                });
        }
    }

    void RefreshDifficultyButtons()
    {
        if (difficultyButtons == null) return;

        var difficulties = new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };

        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            bool selected = MatchSettings.Difficulty == difficulties[i];

            var image = difficultyButtons[i].GetComponent<Image>();
            image.color = selected
                ? new Color(0.20f, 0.60f, 0.95f)
                : new Color(0.18f, 0.20f, 0.24f);
        }
    }

    void RefreshLabels()
    {
        botLabel.text = Localization.Get("bots") + ": " + Mathf.RoundToInt(botSlider.value);
        fragLabel.text = Localization.Get("frags") + ": " + SnappedFragLimit();
    }

    /// <summary>Frag limit rounded to tens, so the label never shows an odd number.</summary>
    int SnappedFragLimit()
    {
        int frags = Mathf.RoundToInt(fragSlider.value / 10f) * 10;
        return Mathf.Clamp(frags, MatchSettings.MinFrags, MatchSettings.MaxFrags);
    }

    void StartMatch()
    {
        MatchSettings.BotCount = Mathf.RoundToInt(botSlider.value);
        MatchSettings.FragLimit = SnappedFragLimit();
        SceneManager.LoadScene(arenaSceneName);
    }
}
