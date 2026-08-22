using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Start screen: pick how many bots to fight and how many frags win the match,
/// then load the arena.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public string arenaSceneName = "Pool";

    Slider botSlider;
    Slider fragSlider;
    Text botLabel;
    Text fragLabel;

    void Start()
    {
        FirstPersonController.LockCursor(false);
        UIFactory.EnsureEventSystem();
        Build();
    }

    void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("MainMenu", 0);
        Transform root = canvas.transform;

        Vector2 centre = new Vector2(0.5f, 0.5f);

        var background = UIFactory.CreateImage(root, "Background", new Color(0.07f, 0.09f, 0.12f),
                                               centre, centre, Vector2.zero, Vector2.zero);
        UIFactory.Stretch(background.gameObject);

        UIFactory.CreateText(root, "Title", "POOL SHOOTER", 72, TextAnchor.MiddleCenter,
                             new Color(0.35f, 0.70f, 1f),
                             centre, centre, new Vector2(0f, 300f), new Vector2(900f, 100f));

        UIFactory.CreateText(root, "Subtitle", "Бой всех против всех с ботами", 26, TextAnchor.MiddleCenter,
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

        UIFactory.CreateButton(root, "StartButton", "В БОЙ", 36,
                               centre, centre, new Vector2(0f, -190f), new Vector2(360f, 80f), StartMatch);

        UIFactory.CreateText(root, "Controls",
                             "WASD — движение   Shift — бег   Space — прыжок   Ctrl — присесть\n" +
                             "ЛКМ — огонь   R — перезарядка   Esc — освободить курсор",
                             22, TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.7f),
                             centre, centre, new Vector2(0f, -320f), new Vector2(1100f, 80f));

        RefreshLabels();
    }

    void RefreshLabels()
    {
        int bots = Mathf.RoundToInt(botSlider.value);

        // Snap the frag limit to steps of 10 so the label never shows an odd number.
        int frags = Mathf.RoundToInt(fragSlider.value / 10f) * 10;
        frags = Mathf.Clamp(frags, MatchSettings.MinFrags, MatchSettings.MaxFrags);

        botLabel.text = "Количество ботов: " + bots;
        fragLabel.text = "Фрагов до победы: " + frags;
    }

    void StartMatch()
    {
        MatchSettings.BotCount = Mathf.RoundToInt(botSlider.value);

        int frags = Mathf.RoundToInt(fragSlider.value / 10f) * 10;
        MatchSettings.FragLimit = Mathf.Clamp(frags, MatchSettings.MinFrags, MatchSettings.MaxFrags);

        SceneManager.LoadScene(arenaSceneName);
    }
}
