using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The briefing screen.
///
/// Three things decide a mission — which airframe, which charge, which map —
/// and each gets its own place rather than being stacked into one wall of
/// cards. The home screen shows what is currently fitted and lets the player
/// change it; the choosing happens on its own panel with room to read.
///
/// Everything locked is opened by a rewarded ad, and the maps can also be
/// opened by clearing the one before them. That is the whole monetisation
/// model, and it stays honest: the starter airframe with the compact charge
/// clears every map in the game, so every ad is a shortcut rather than a toll.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    // ---------- layout ----------
    //
    // Named rather than typed inline at each call. What went wrong with the
    // first version of this screen was things sitting too close together, and
    // that is impossible to fix reliably when every gap is a different literal
    // buried in a different method.

    const float TitleY = -84f;
    const float SubtitleY = -152f;
    const float SectionGap = 54f;      // section label to the row beneath it
    const float CardGap = 40f;         // between cards in a row

    const float DroneCardWidth = 400f;
    const float DroneCardHeight = 470f;
    const float MapCardWidth = 400f;
    const float MapCardHeight = 310f;

    static readonly Color Panel = new Color(0.11f, 0.14f, 0.13f);
    static readonly Color PanelActive = new Color(0.15f, 0.24f, 0.19f);
    static readonly Color PanelLocked = new Color(0.09f, 0.10f, 0.10f);
    static readonly Color Ink = new Color(0.92f, 0.94f, 0.96f);
    static readonly Color InkDim = new Color(0.62f, 0.70f, 0.67f);
    static readonly Color ActionSelect = new Color(0.16f, 0.45f, 0.75f);
    static readonly Color ActionActive = new Color(0.24f, 0.52f, 0.36f);
    static readonly Color ActionAd = new Color(0.72f, 0.48f, 0.12f);
    static readonly Color ActionDead = new Color(0.20f, 0.21f, 0.22f);

    static readonly WarheadType[] Charges = { WarheadType.Compact, WarheadType.Standard };

    Transform root;
    Text statusText;

    GameObject homePanel;
    GameObject dronePanel;
    GameObject warheadPanel;

    Text droneNavLabel;
    Text warheadNavLabel;
    Text launchLabel;

    Button[] droneButtons;
    Image[] droneFrames;
    Text[] droneActions;

    Button[] warheadButtons;
    Image[] warheadFrames;
    Text[] warheadActions;

    Button[] mapButtons;
    Image[] mapFrames;
    Text[] mapActions;

    /// <summary>True while an ad is in flight, so nothing can be pressed twice.</summary>
    bool waitingForAd;

    void Start()
    {
        UIFactory.EnsureEventSystem();

        // The menu is a pointer-driven screen; the mission takes the cursor back
        // when it starts.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Build();
        ShowPanel(homePanel);
        Refresh();

        DroneLoadout.OnChanged += Refresh;
        MissionCatalog.OnChanged += Refresh;
        Localization.OnLanguageChanged += Refresh;

        YandexAds.NotifyGameReady();
    }

    void OnDestroy()
    {
        DroneLoadout.OnChanged -= Refresh;
        MissionCatalog.OnChanged -= Refresh;
        Localization.OnLanguageChanged -= Refresh;
    }

    // ---------- construction ----------

    void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("MainMenu", 0);
        root = canvas.transform;

        BuildBackground();

        homePanel = CreatePanel("Home");
        dronePanel = CreatePanel("Drones");
        warheadPanel = CreatePanel("Charges");

        BuildHomePanel(homePanel.transform);
        BuildDronePanel(dronePanel.transform);
        BuildWarheadPanel(warheadPanel.transform);

        // Status sits above the controls line on every panel, so a message about
        // an ad always appears in the same place whichever panel raised it.
        statusText = UIFactory.CreateText(root, "Status", "", 24, TextAnchor.MiddleCenter,
                                          new Color(0.85f, 0.72f, 0.35f),
                                          new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                          new Vector2(0f, 100f), new Vector2(1500f, 34f));
    }

    /// <summary>
    /// A dark field with a faint grid, so the screen reads as a briefing
    /// terminal rather than an empty canvas.
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

    // ---------- home ----------

    void BuildHomePanel(Transform parent)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        Vector2 bottom = new Vector2(0.5f, 0f);

        UIFactory.CreateText(parent, "Title", "DRONE STRIKE", 78, TextAnchor.MiddleCenter,
                             Ink, top, top, new Vector2(0f, TitleY), new Vector2(1400f, 96f));

        // No place name in the subtitle. The game is abstract on purpose, and
        // every phrase that sounds like a real front is one more reason for a
        // moderator to look harder at it — which the subtitle buys nothing worth.
        UIFactory.CreateText(parent, "Subtitle", "СИМУЛЯТОР УДАРНОГО FPV-ДРОНА", 26,
                             TextAnchor.MiddleCenter, InkDim, top, top,
                             new Vector2(0f, SubtitleY), new Vector2(1400f, 40f));

        const float navY = -268f;
        const float navWidth = 600f;
        float navSpan = navWidth * 2f + CardGap;

        SectionLabel(parent, "Kit", "СНАРЯЖЕНИЕ", navY + SectionGap, navSpan);

        droneNavLabel = NavButton(parent, "DroneNav",
                                  new Vector2(-(navWidth + CardGap) * 0.5f, navY),
                                  navWidth, () => ShowPanel(dronePanel));

        warheadNavLabel = NavButton(parent, "WarheadNav",
                                    new Vector2((navWidth + CardGap) * 0.5f, navY),
                                    navWidth, () => ShowPanel(warheadPanel));

        const float mapY = -480f;
        float mapSpan = MissionCatalog.Maps.Length * MapCardWidth
                        + (MissionCatalog.Maps.Length - 1) * CardGap;

        SectionLabel(parent, "Mission", "ЗАДАНИЕ", mapY + SectionGap, mapSpan);
        BuildMapCards(parent, mapY, mapSpan);

        Button launch = UIFactory.CreateButton(parent, "Launch", "В БОЙ", 40, bottom, bottom,
                                               new Vector2(0f, 168f), new Vector2(560f, 96f),
                                               Launch);
        launchLabel = launch.GetComponentInChildren<Text>();

        UIFactory.CreateText(parent, "Controls",
                             "W / S — вперёд и назад по взгляду     A / D — снос     Space / Ctrl — высота\n"
                             + "Мышь — камера     Esc — освободить курсор     Дрон детонирует при ударе",
                             22, TextAnchor.MiddleCenter, new Color(0.50f, 0.56f, 0.58f),
                             bottom, bottom, new Vector2(0f, 44f), new Vector2(1500f, 56f));
    }

    Text NavButton(Transform parent, string name, Vector2 position, float width,
                   UnityEngine.Events.UnityAction onClick)
    {
        Button button = UIFactory.CreateButton(parent, name, "", 28,
                                               new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                               position, new Vector2(width, 92f), onClick);

        var image = button.targetGraphic as Image;
        if (image != null) image.color = new Color(0.15f, 0.20f, 0.22f);

        return button.GetComponentInChildren<Text>();
    }

    void BuildMapCards(Transform parent, float y, float span)
    {
        int count = MissionCatalog.Maps.Length;

        mapButtons = new Button[count];
        mapFrames = new Image[count];
        mapActions = new Text[count];

        float startX = -span * 0.5f + MapCardWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            MissionMap map = MissionCatalog.Maps[i];
            int index = i;

            var position = new Vector2(startX + i * (MapCardWidth + CardGap), y);

            Image frame = Card(parent, "Map" + i, position,
                               new Vector2(MapCardWidth, MapCardHeight), map.accent);
            mapFrames[i] = frame;

            UIFactory.CreateText(frame.transform, "Name", map.displayName, 30,
                                 TextAnchor.MiddleCenter, Ink,
                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -52f), new Vector2(MapCardWidth - 40f, 40f));

            Text tagline = UIFactory.CreateText(frame.transform, "Tagline", map.tagline, 20,
                                                TextAnchor.UpperCenter, InkDim,
                                                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                new Vector2(0f, -88f),
                                                new Vector2(MapCardWidth - 56f, 84f));
            tagline.horizontalOverflow = HorizontalWrapMode.Wrap;

            UIFactory.CreateText(frame.transform, "Targets", "ЦЕЛЕЙ:  " + map.targetCount, 21,
                                 TextAnchor.MiddleCenter, new Color(0.55f, 0.62f, 0.60f),
                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -200f), new Vector2(MapCardWidth - 40f, 28f));

            Button button = CardButton(frame.transform, MapCardWidth, () => OnMapPressed(index));
            mapButtons[i] = button;
            mapActions[i] = button.GetComponentInChildren<Text>();
        }
    }

    // ---------- airframes ----------

    void BuildDronePanel(Transform parent)
    {
        PanelHeader(parent, "ВЫБОР БОРТА",
                    "Каждый следующий борт быстрее и бьёт сильнее предыдущего.");

        int count = DroneLoadout.Models.Length;

        droneButtons = new Button[count];
        droneFrames = new Image[count];
        droneActions = new Text[count];

        float span = count * DroneCardWidth + (count - 1) * CardGap;
        float startX = -span * 0.5f + DroneCardWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            DroneModel model = DroneLoadout.Models[i];
            int index = i;

            var position = new Vector2(startX + i * (DroneCardWidth + CardGap), -300f);

            Image frame = Card(parent, "Drone" + i, position,
                               new Vector2(DroneCardWidth, DroneCardHeight), model.accent);
            droneFrames[i] = frame;

            UIFactory.CreateText(frame.transform, "Name", model.displayName, 34,
                                 TextAnchor.MiddleCenter, Ink,
                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -56f), new Vector2(DroneCardWidth - 40f, 44f));

            Text tagline = UIFactory.CreateText(frame.transform, "Tagline", model.tagline, 20,
                                                TextAnchor.UpperCenter, InkDim,
                                                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                new Vector2(0f, -94f),
                                                new Vector2(DroneCardWidth - 56f, 84f));
            tagline.horizontalOverflow = HorizontalWrapMode.Wrap;

            StatBar(frame.transform, "ТЯГА", model.thrustFactor, DroneCardWidth, -232f);
            StatBar(frame.transform, "СКОРОСТЬ", model.speedFactor, DroneCardWidth, -276f);
            StatBar(frame.transform, "ЗАРЯД", model.damageFactor, DroneCardWidth, -320f);
            StatBar(frame.transform, "РЕСУРС", model.enduranceFactor, DroneCardWidth, -364f);

            Button button = CardButton(frame.transform, DroneCardWidth, () => OnDronePressed(index));
            droneButtons[i] = button;
            droneActions[i] = button.GetComponentInChildren<Text>();
        }

        BackButton(parent);
    }

    /// <summary>
    /// One stat as a bar. Scaled against 1.7, a little above the highest factor
    /// any airframe has, so the best drone still leaves visible headroom rather
    /// than pinning the bar and looking finished.
    /// </summary>
    void StatBar(Transform parent, string label, float factor, float cardWidth, float y)
    {
        UIFactory.CreateText(parent, "Label" + label, label, 18, TextAnchor.MiddleLeft,
                             new Color(0.56f, 0.63f, 0.61f),
                             new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                             new Vector2(28f, y), new Vector2(150f, 24f));

        const float barWidth = 176f;
        float barX = cardWidth - barWidth - 28f;

        UIFactory.CreateImage(parent, "Track" + label, new Color(0.06f, 0.08f, 0.08f),
                              new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                              new Vector2(barX, y), new Vector2(barWidth, 12f));

        float fill = Mathf.Clamp01(factor / 1.7f);
        UIFactory.CreateImage(parent, "Fill" + label, new Color(0.42f, 0.72f, 0.58f),
                              new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                              new Vector2(barX, y), new Vector2(barWidth * fill, 12f));
    }

    // ---------- charges ----------

    void BuildWarheadPanel(Transform parent)
    {
        PanelHeader(parent, "БОЕПРИПАС",
                    "Малый заряд легче — борт резвее. Стандартный бьёт вдвое сильнее.");

        warheadButtons = new Button[Charges.Length];
        warheadFrames = new Image[Charges.Length];
        warheadActions = new Text[Charges.Length];

        const float cardWidth = 470f;
        const float cardHeight = 390f;

        float span = Charges.Length * cardWidth + (Charges.Length - 1) * CardGap;
        float startX = -span * 0.5f + cardWidth * 0.5f;

        string[] blurbs =
        {
            "Штатная боевая часть. Борт с ней заметно легче и охотнее слушается.",
            "Тяжёлая боевая часть. Снимает броню с первого захода, но борт вязче."
        };

        for (int i = 0; i < Charges.Length; i++)
        {
            WarheadProfile profile = WarheadProfile.For(Charges[i]);
            int index = i;

            var position = new Vector2(startX + i * (cardWidth + CardGap), -330f);
            Color accent = i == 0 ? new Color(0.35f, 0.60f, 0.45f) : new Color(0.72f, 0.30f, 0.22f);

            Image frame = Card(parent, "Charge" + i, position,
                               new Vector2(cardWidth, cardHeight), accent);
            warheadFrames[i] = frame;

            UIFactory.CreateText(frame.transform, "Name", profile.DisplayName, 34,
                                 TextAnchor.MiddleCenter, Ink,
                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -56f), new Vector2(cardWidth - 40f, 44f));

            Text blurb = UIFactory.CreateText(frame.transform, "Blurb", blurbs[i], 21,
                                              TextAnchor.UpperCenter, InkDim,
                                              new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                              new Vector2(0f, -96f),
                                              new Vector2(cardWidth - 56f, 88f));
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;

            UIFactory.CreateText(frame.transform, "Figures",
                                 "УРОН  " + Mathf.RoundToInt(profile.damage)
                                 + "          РАДИУС  " + profile.blastRadius.ToString("0.0") + " М",
                                 21, TextAnchor.MiddleCenter, new Color(0.60f, 0.68f, 0.65f),
                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -228f), new Vector2(cardWidth - 40f, 30f));

            Button button = CardButton(frame.transform, cardWidth, () => OnChargePressed(index));
            warheadButtons[i] = button;
            warheadActions[i] = button.GetComponentInChildren<Text>();
        }

        BackButton(parent);
    }

    // ---------- shared widgets ----------

    GameObject CreatePanel(string name)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(root, false);
        UIFactory.Stretch(panel);
        return panel;
    }

    void PanelHeader(Transform parent, string title, string note)
    {
        Vector2 top = new Vector2(0.5f, 1f);

        UIFactory.CreateText(parent, "Header", title, 54, TextAnchor.MiddleCenter, Ink,
                             top, top, new Vector2(0f, -88f), new Vector2(1400f, 70f));

        UIFactory.CreateText(parent, "HeaderNote", note, 23, TextAnchor.MiddleCenter, InkDim,
                             top, top, new Vector2(0f, -152f), new Vector2(1400f, 36f));
    }

    void SectionLabel(Transform parent, string name, string text, float y, float span)
    {
        UIFactory.CreateText(parent, "Section" + name, text, 26, TextAnchor.MiddleLeft,
                             new Color(0.50f, 0.60f, 0.57f),
                             new Vector2(0.5f, 1f), new Vector2(0f, 0.5f),
                             new Vector2(-span * 0.5f, y), new Vector2(span, 34f));
    }

    /// <summary>A card body with its colour band across the top.</summary>
    Image Card(Transform parent, string name, Vector2 position, Vector2 size, Color accent)
    {
        Image frame = UIFactory.CreateImage(parent, name, Panel,
                                            new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                            position, size);

        UIFactory.CreateImage(frame.transform, "Accent", accent,
                              new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                              Vector2.zero, new Vector2(size.x, 10f));

        return frame;
    }

    Button CardButton(Transform parent, float cardWidth, UnityEngine.Events.UnityAction onClick)
    {
        return UIFactory.CreateButton(parent, "Action", "", 24,
                                      new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                      new Vector2(0f, 26f),
                                      new Vector2(cardWidth - 56f, 64f), onClick);
    }

    void BackButton(Transform parent)
    {
        Button back = UIFactory.CreateButton(parent, "Back", "НАЗАД", 30,
                                             new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                             new Vector2(0f, 168f), new Vector2(380f, 84f),
                                             () => ShowPanel(homePanel));

        var image = back.targetGraphic as Image;
        if (image != null) image.color = new Color(0.15f, 0.20f, 0.22f);
    }

    void ShowPanel(GameObject panel)
    {
        homePanel.SetActive(panel == homePanel);
        dronePanel.SetActive(panel == dronePanel);
        warheadPanel.SetActive(panel == warheadPanel);

        SetStatus("");
    }

    // ---------- state ----------

    void Refresh()
    {
        RefreshDrones();
        RefreshCharges();
        RefreshMaps();
        RefreshHome();
    }

    void RefreshHome()
    {
        if (droneNavLabel != null)
            droneNavLabel.text = "БОРТ:   " + DroneLoadout.Selected.displayName;

        if (warheadNavLabel != null)
            warheadNavLabel.text = "ЗАРЯД:   "
                                   + WarheadProfile.For(DroneLoadout.SelectedWarhead).DisplayName;

        if (launchLabel != null)
            launchLabel.text = "В БОЙ · " + MissionCatalog.Selected.displayName;
    }

    void RefreshDrones()
    {
        if (droneButtons == null) return;

        int selected = DroneLoadout.SelectedIndex;

        for (int i = 0; i < droneButtons.Length; i++)
        {
            DroneModel model = DroneLoadout.Models[i];
            bool unlocked = DroneLoadout.IsUnlocked(model);
            bool available = DroneLoadout.IsAvailable(model);
            bool active = unlocked && i == selected;

            string action = unlocked
                ? (active ? "ВЫБРАН" : "ВЫБРАТЬ")
                : available ? "ОТКРЫТЬ ЗА РЕКЛАМУ"
                            : "СНАЧАЛА «" + DroneLoadout.PrerequisiteName(model) + "»";

            Dress(droneFrames[i], droneButtons[i], droneActions[i],
                  action, unlocked, available, active);
        }
    }

    void RefreshCharges()
    {
        if (warheadButtons == null) return;

        WarheadType selected = DroneLoadout.SelectedWarhead;

        for (int i = 0; i < Charges.Length; i++)
        {
            bool unlocked = DroneLoadout.IsWarheadUnlocked(Charges[i]);
            bool active = unlocked && Charges[i] == selected;

            string action = unlocked
                ? (active ? "УСТАНОВЛЕН" : "УСТАНОВИТЬ")
                : "ОТКРЫТЬ ЗА РЕКЛАМУ";

            Dress(warheadFrames[i], warheadButtons[i], warheadActions[i],
                  action, unlocked, true, active);
        }
    }

    void RefreshMaps()
    {
        if (mapButtons == null) return;

        int selected = MissionCatalog.SelectedIndex;

        for (int i = 0; i < mapButtons.Length; i++)
        {
            bool unlocked = MissionCatalog.IsUnlocked(i);
            bool cleared = MissionCatalog.IsCleared(i);
            bool active = unlocked && i == selected;

            string action;
            if (!unlocked) action = "ОТКРЫТЬ ЗА РЕКЛАМУ";
            else if (active) action = cleared ? "ВЫБРАНА · ПРОЙДЕНА" : "ВЫБРАНА";
            else action = cleared ? "ВЫБРАТЬ · ПРОЙДЕНА" : "ВЫБРАТЬ";

            Dress(mapFrames[i], mapButtons[i], mapActions[i], action, unlocked, true, active);
        }
    }

    /// <summary>Paints one card for its state. The same rules on all three rows.</summary>
    void Dress(Image frame, Button button, Text action, string label,
               bool unlocked, bool available, bool active)
    {
        if (frame != null)
            frame.color = active ? PanelActive : available ? Panel : PanelLocked;

        if (action != null) action.text = label;

        if (button == null) return;

        var background = button.targetGraphic as Image;
        if (background != null)
        {
            background.color = unlocked
                ? (active ? ActionActive : ActionSelect)
                : available ? ActionAd : ActionDead;
        }

        button.interactable = !waitingForAd && available && !active;
    }

    // ---------- actions ----------

    void OnDronePressed(int index)
    {
        if (waitingForAd) return;

        DroneModel model = DroneLoadout.Models[index];

        if (DroneLoadout.IsUnlocked(model))
        {
            DroneLoadout.SelectedIndex = index;
            SetStatus("");
            return;
        }

        if (!DroneLoadout.IsAvailable(model))
        {
            SetStatus("Сначала откройте борт «" + DroneLoadout.PrerequisiteName(model) + "».");
            return;
        }

        WatchAd("Борт «" + model.displayName + "» открыт.", () =>
        {
            DroneLoadout.Unlock(model);
            DroneLoadout.SelectedIndex = index;
        });
    }

    void OnChargePressed(int index)
    {
        if (waitingForAd) return;

        WarheadType charge = Charges[index];

        if (DroneLoadout.IsWarheadUnlocked(charge))
        {
            DroneLoadout.SelectedWarhead = charge;
            SetStatus("");
            return;
        }

        WatchAd("Заряд «" + WarheadProfile.For(charge).DisplayName + "» открыт.", () =>
        {
            DroneLoadout.UnlockWarhead(charge);
            DroneLoadout.SelectedWarhead = charge;
        });
    }

    void OnMapPressed(int index)
    {
        if (waitingForAd) return;

        if (MissionCatalog.IsUnlocked(index))
        {
            MissionCatalog.SelectedIndex = index;
            SetStatus("");
            return;
        }

        WatchAd("Карта «" + MissionCatalog.Maps[index].displayName + "» открыта.", () =>
        {
            MissionCatalog.Unlock(index);
            MissionCatalog.SelectedIndex = index;
        });
    }

    /// <summary>
    /// Shows a rewarded ad and applies <paramref name="grant"/> only if it was
    /// watched through.
    ///
    /// Yandex reports a completed view separately from the ad merely closing, so
    /// the reward hangs off the former. A failure has to say so out loud, or a
    /// player on a blocked ad slot is left pressing a button that silently does
    /// nothing.
    /// </summary>
    void WatchAd(string successMessage, System.Action grant)
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
            // Editor only — a shipped build grants nothing without a completed view.
            watched = true;
#endif

            if (watched)
            {
                grant();
                SetStatus(successMessage);
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
        SceneManager.LoadScene(MissionCatalog.Selected.sceneName);
    }
}
