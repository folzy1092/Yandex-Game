using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The briefing screen: pick an airframe, pick a charge, launch.
///
/// It is one screen rather than the three-tab menu the reference mockups use,
/// because everything those tabs held that actually changes the mission fits
/// here — and a player who has to walk through three screens before flying
/// mostly does not fly. The tabs can come back when there is a campaign behind
/// them to justify the walk.
///
/// The locked airframes are the monetisation. Each one is unlocked for good by
/// watching a rewarded ad, and the whole roster is optional — the starter drone
/// clears every target on the map, so the ad is an offer rather than a toll.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    const string MissionScene = "IndustrialZone";

    Transform root;
    Text statusText;

    readonly Button[] cardButtons = new Button[DroneCount];
    readonly Image[] cardFrames = new Image[DroneCount];
    readonly Text[] cardActions = new Text[DroneCount];

    const int DroneCount = 3;

    Button compactButton;
    Button standardButton;

    /// <summary>True while an ad is in flight, so the buttons cannot be spammed.</summary>
    bool waitingForAd;

    void Start()
    {
        UIFactory.EnsureEventSystem();

        // The menu is a pointer-driven screen; the mission takes the cursor back
        // when it starts.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Build();
        Refresh();

        DroneLoadout.OnChanged += Refresh;
        Localization.OnLanguageChanged += Refresh;

        YandexAds.NotifyGameReady();
    }

    void OnDestroy()
    {
        DroneLoadout.OnChanged -= Refresh;
        Localization.OnLanguageChanged -= Refresh;
    }

    // ---------- construction ----------

    void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("MainMenu", 0);
        root = canvas.transform;

        BuildBackground();

        Vector2 top = new Vector2(0.5f, 1f);
        Vector2 bottom = new Vector2(0.5f, 0f);

        UIFactory.CreateText(root, "Title", "DRONE STRIKE", 82, TextAnchor.MiddleCenter,
                             new Color(0.92f, 0.94f, 0.96f), top, top,
                             new Vector2(0f, -70f), new Vector2(1200f, 100f));

        UIFactory.CreateText(root, "Subtitle", "СИМУЛЯТОР FPV-ДРОНА · ПЕРЕДОВАЯ ПОЗИЦИЯ", 26,
                             TextAnchor.MiddleCenter, new Color(0.55f, 0.68f, 0.62f), top, top,
                             new Vector2(0f, -130f), new Vector2(1200f, 40f));

        UIFactory.CreateText(root, "SectionDrone", "ВЫБОР БОРТА", 30, TextAnchor.MiddleLeft,
                             new Color(0.62f, 0.72f, 0.68f), top, top,
                             new Vector2(-540f, -190f), new Vector2(600f, 40f));

        BuildCards();
        BuildWarheadRow();

        UIFactory.CreateButton(root, "Launch", "В БОЙ", 42, bottom, bottom,
                               new Vector2(0f, 130f), new Vector2(440f, 92f), Launch);

        statusText = UIFactory.CreateText(root, "Status", "", 24, TextAnchor.MiddleCenter,
                                          new Color(0.85f, 0.72f, 0.35f), bottom, bottom,
                                          new Vector2(0f, 84f), new Vector2(1200f, 34f));

        UIFactory.CreateText(root, "Controls",
                             "W / S — вперёд и назад по взгляду   A / D — снос   Space / Ctrl — высота\n"
                             + "Мышь — камера   Esc — освободить курсор   Дрон детонирует при ударе",
                             22, TextAnchor.MiddleCenter, new Color(0.55f, 0.60f, 0.62f),
                             bottom, bottom, new Vector2(0f, 34f), new Vector2(1400f, 60f));
    }

    /// <summary>
    /// A dark field with a faint grid, so the screen reads as a briefing
    /// terminal rather than an empty canvas. Built from stretched images
    /// because a background texture would be one more asset to generate.
    /// </summary>
    void BuildBackground()
    {
        Image field = UIFactory.CreateImage(root, "Field", new Color(0.055f, 0.075f, 0.07f),
                                            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                            Vector2.zero, Vector2.zero);
        UIFactory.Stretch(field.gameObject);

        var grid = new GameObject("Grid");
        grid.transform.SetParent(root, false);
        UIFactory.Stretch(grid);

        const int lines = 26;
        for (int i = 1; i < lines; i++)
        {
            var line = new GameObject("H" + i);
            line.transform.SetParent(grid.transform, false);

            var image = line.AddComponent<Image>();
            image.sprite = UIFactory.BlankSprite;
            image.color = new Color(0.35f, 0.55f, 0.45f, 0.05f);
            image.raycastTarget = false;

            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, (float)i / lines);
            rect.anchorMax = new Vector2(1f, (float)i / lines);
            rect.offsetMin = new Vector2(0f, -1f);
            rect.offsetMax = new Vector2(0f, 1f);
        }
    }

    void BuildCards()
    {
        Vector2 top = new Vector2(0.5f, 1f);

        const float cardWidth = 380f;
        const float cardHeight = 340f;
        const float gap = 30f;

        float span = DroneCount * cardWidth + (DroneCount - 1) * gap;
        float startX = -span * 0.5f + cardWidth * 0.5f;

        for (int i = 0; i < DroneCount; i++)
        {
            DroneModel model = DroneLoadout.Models[i];
            int index = i;                     // captured per iteration, not shared

            float x = startX + i * (cardWidth + gap);
            var position = new Vector2(x, -400f);

            Image frame = UIFactory.CreateImage(root, "Card" + i, new Color(0.11f, 0.14f, 0.13f),
                                                top, new Vector2(0.5f, 0.5f), position,
                                                new Vector2(cardWidth, cardHeight));
            cardFrames[i] = frame;

            // A colour bar rather than a render of the airframe: three drones
            // that differ only in numbers still have to be told apart at a
            // glance, and the same accent is the colour the drone is painted.
            UIFactory.CreateImage(frame.transform, "Accent", model.accent,
                                  new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                  new Vector2(0f, 0f), new Vector2(cardWidth, 10f));

            UIFactory.CreateText(frame.transform, "Name", model.displayName, 34,
                                 TextAnchor.MiddleCenter, Color.white,
                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                 new Vector2(0f, -40f), new Vector2(cardWidth - 20f, 44f));

            var tagline = UIFactory.CreateText(frame.transform, "Tagline", model.tagline, 20,
                                               TextAnchor.UpperCenter,
                                               new Color(0.66f, 0.72f, 0.70f),
                                               new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                               new Vector2(0f, -74f),
                                               new Vector2(cardWidth - 44f, 80f));
            tagline.horizontalOverflow = HorizontalWrapMode.Wrap;

            BuildStatBar(frame.transform, "ТЯГА", model.thrustFactor, cardWidth, -156f);
            BuildStatBar(frame.transform, "СКОРОСТЬ", model.speedFactor, cardWidth, -190f);
            BuildStatBar(frame.transform, "ЗАРЯД", model.damageFactor, cardWidth, -224f);
            BuildStatBar(frame.transform, "РЕСУРС", model.enduranceFactor, cardWidth, -258f);

            Button button = UIFactory.CreateButton(frame.transform, "Action", "", 24,
                                                   new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                                   new Vector2(0f, 18f),
                                                   new Vector2(cardWidth - 40f, 56f),
                                                   () => OnCardPressed(index));
            cardButtons[i] = button;
            cardActions[i] = button.GetComponentInChildren<Text>();
        }
    }

    /// <summary>
    /// One stat as a bar. Scaled against 1.6, which is a little above the
    /// highest factor any airframe has, so the best drone still leaves headroom
    /// visible rather than pinning the bar and looking maxed out.
    /// </summary>
    void BuildStatBar(Transform parent, string label, float factor, float cardWidth, float y)
    {
        UIFactory.CreateText(parent, "Label" + label, label, 18, TextAnchor.MiddleLeft,
                             new Color(0.58f, 0.64f, 0.62f),
                             new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                             new Vector2(24f, y), new Vector2(140f, 24f));

        const float barWidth = 170f;

        UIFactory.CreateImage(parent, "Track" + label, new Color(0.06f, 0.08f, 0.08f),
                              new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                              new Vector2(cardWidth - barWidth - 24f, y),
                              new Vector2(barWidth, 12f));

        float fill = Mathf.Clamp01(factor / 1.6f);
        UIFactory.CreateImage(parent, "Fill" + label, new Color(0.42f, 0.72f, 0.58f),
                              new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                              new Vector2(cardWidth - barWidth - 24f, y),
                              new Vector2(barWidth * fill, 12f));
    }

    void BuildWarheadRow()
    {
        Vector2 top = new Vector2(0.5f, 1f);

        UIFactory.CreateText(root, "SectionWarhead", "БОЕПРИПАС", 30, TextAnchor.MiddleLeft,
                             new Color(0.62f, 0.72f, 0.68f), top, top,
                             new Vector2(-540f, -600f), new Vector2(600f, 40f));

        compactButton = UIFactory.CreateButton(root, "Compact", "МАЛЫЙ · лёгкий, борт резвее", 24,
                                               top, new Vector2(0.5f, 0.5f),
                                               new Vector2(-300f, -650f), new Vector2(560f, 62f),
                                               () => DroneLoadout.SelectedWarhead = WarheadType.Compact);

        standardButton = UIFactory.CreateButton(root, "Standard", "СТАНДАРТ · тяжелее, бьёт сильнее", 24,
                                                top, new Vector2(0.5f, 0.5f),
                                                new Vector2(300f, -650f), new Vector2(560f, 62f),
                                                () => DroneLoadout.SelectedWarhead = WarheadType.Standard);
    }

    // ---------- state ----------

    void Refresh()
    {
        int selected = DroneLoadout.SelectedIndex;

        for (int i = 0; i < DroneCount; i++)
        {
            DroneModel model = DroneLoadout.Models[i];
            bool unlocked = DroneLoadout.IsUnlocked(model);
            bool active = unlocked && i == selected;

            cardFrames[i].color = active
                ? new Color(0.16f, 0.24f, 0.20f)
                : new Color(0.11f, 0.14f, 0.13f);

            if (cardActions[i] != null)
            {
                cardActions[i].text = !unlocked
                    ? "ОТКРЫТЬ ЗА РЕКЛАМУ"
                    : active ? "ВЫБРАН" : "ВЫБРАТЬ";
            }

            Image background = cardButtons[i].targetGraphic as Image;
            if (background != null)
            {
                background.color = !unlocked
                    ? new Color(0.72f, 0.48f, 0.12f)
                    : active ? new Color(0.24f, 0.52f, 0.36f) : new Color(0.16f, 0.45f, 0.75f);
            }

            cardButtons[i].interactable = !waitingForAd && !(active && unlocked);
        }

        WarheadType warhead = DroneLoadout.SelectedWarhead;
        Highlight(compactButton, warhead == WarheadType.Compact);
        Highlight(standardButton, warhead == WarheadType.Standard);
    }

    static void Highlight(Button button, bool active)
    {
        if (button == null) return;

        var image = button.targetGraphic as Image;
        if (image == null) return;

        image.color = active ? new Color(0.24f, 0.52f, 0.36f) : new Color(0.16f, 0.20f, 0.22f);
    }

    void OnCardPressed(int index)
    {
        if (waitingForAd) return;

        DroneModel model = DroneLoadout.Models[index];

        if (DroneLoadout.IsUnlocked(model))
        {
            DroneLoadout.SelectedIndex = index;
            SetStatus("");
            return;
        }

        RequestUnlock(index);
    }

    /// <summary>
    /// Shows a rewarded ad and unlocks the airframe if it was watched through.
    ///
    /// The reward is only granted on a real completed view — Yandex reports that
    /// separately from the ad merely closing — but a failure has to say so out
    /// loud, or a player on a blocked ad slot is left pressing a button that
    /// silently does nothing.
    /// </summary>
    void RequestUnlock(int index)
    {
        waitingForAd = true;
        SetStatus("Загрузка рекламы...");
        Refresh();

        YandexAds.ShowRewarded(watched =>
        {
            waitingForAd = false;

#if UNITY_EDITOR
            // There is no ad network in the editor, so every request reports
            // "not watched" and the unlock could never be tested before a build.
            // Only ever true in the editor — a shipped build grants nothing
            // without a completed view.
            watched = true;
#endif

            if (watched)
            {
                DroneLoadout.Unlock(DroneLoadout.Models[index]);
                DroneLoadout.SelectedIndex = index;
                SetStatus("Борт «" + DroneLoadout.Models[index].displayName + "» открыт.");
            }
            else
            {
                SetStatus("Реклама недоступна. Попробуйте позже.");
            }

            Refresh();
        });
    }

    void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    void Launch()
    {
        if (waitingForAd) return;
        SceneManager.LoadScene(MissionScene);
    }
}
