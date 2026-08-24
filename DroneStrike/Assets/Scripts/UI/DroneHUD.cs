using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pilot's view: telemetry over the camera feed, plus the scanlines and
/// interference that make it read as a video downlink rather than a game camera.
///
/// The interference is driven by the actual link strength, so degrading picture
/// quality is information — it tells the pilot they are near the edge of range
/// before the drone is lost, without a single line of text.
/// </summary>
public class DroneHUD : MonoBehaviour
{
    Text speedText;
    Text altitudeText;
    Text batteryText;
    Text headingText;
    Text missionText;
    Image batteryFill;
    Image signalFill;
    Image staticOverlay;
    RectTransform compassStrip;

    GameObject resultPanel;
    Text resultTitle;
    Text resultDetail;
    Button reviveButton;
    Text reviveLabel;

    Text signalLostBanner;
    float signalLostUntil;

    /// <summary>Pixels of tape per degree of heading.</summary>
    const float CompassScale = 4f;

    /// <summary>Width of one full 360 degree lap of the tape, in pixels.</summary>
    const float CompassLap = 360f * CompassScale;

    void Start()
    {
        UIFactory.EnsureEventSystem();
        Build();

        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnStateChanged += RefreshMission;
            MissionManager.Instance.OnMissionEnded += ShowResult;
            MissionManager.Instance.OnSignalLost += ShowSignalLost;
            RefreshMission();
        }

        YandexAds.NotifyGameReady();
    }

    void OnDestroy()
    {
        if (MissionManager.Instance == null) return;
        MissionManager.Instance.OnStateChanged -= RefreshMission;
        MissionManager.Instance.OnMissionEnded -= ShowResult;
        MissionManager.Instance.OnSignalLost -= ShowSignalLost;
    }

    void Update()
    {
        UpdateSignalLostBanner();

        MissionManager mission = MissionManager.Instance;
        DroneRig drone = mission != null ? mission.ActiveDrone : null;

        if (drone == null || drone.Controller == null)
        {
            if (staticOverlay != null) staticOverlay.color = new Color(1f, 1f, 1f, 0.35f);
            return;
        }

        UpdateTelemetry(drone);
        UpdateSignal(drone);
    }

    void UpdateTelemetry(DroneRig drone)
    {
        speedText.text = Mathf.RoundToInt(drone.Controller.SpeedKmh) + " КМ/Ч";
        altitudeText.text = Mathf.RoundToInt(drone.Controller.AltitudeMetres) + "М";

        float charge = drone.Battery != null ? drone.Battery.Charge : 1f;
        batteryFill.fillAmount = charge;
        batteryText.text = Mathf.CeilToInt(charge * 100f) + "%";
        // Turns amber then red as it empties, so a glance is enough.
        batteryFill.color = charge > 0.4f
            ? new Color(0.55f, 0.85f, 0.55f)
            : Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.75f, 0.25f), charge / 0.4f);

        float heading = drone.Controller.Heading;
        headingText.text = Mathf.RoundToInt(heading).ToString("000");

        // The compass strip slides opposite the heading, so the marks pass the
        // fixed centre index the way a real HSI tape does.
        if (compassStrip != null)
            compassStrip.anchoredPosition =
                new Vector2(-heading * CompassScale, compassStrip.anchoredPosition.y);
    }

    void UpdateSignal(DroneRig drone)
    {
        float strength = drone.SignalLink != null ? drone.SignalLink.Strength : 1f;
        signalFill.fillAmount = strength;
        signalFill.color = strength > 0.35f
            ? new Color(0.8f, 0.85f, 0.9f)
            : new Color(0.95f, 0.35f, 0.3f);

        // Static grows as the link weakens, and flickers so it does not look
        // like a flat grey sheet laid over the screen.
        float noise = (1f - strength) * 0.5f;
        float flicker = Mathf.PerlinNoise(Time.time * 14f, 0f) * 0.35f + 0.65f;
        staticOverlay.color = new Color(1f, 1f, 1f, noise * flicker);
    }

    // ---------- construction ----------

    void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("DroneHUD", 0);
        Transform root = canvas.transform;

        Vector2 centre = new Vector2(0.5f, 0.5f);
        Vector2 topCentre = new Vector2(0.5f, 1f);
        Vector2 topRight = new Vector2(1f, 1f);
        Vector2 topLeft = new Vector2(0f, 1f);

        BuildScanlines(root);
        staticOverlay = BuildStatic(root);

        BuildCompass(root, topCentre);
        BuildCrosshair(root, centre);

        speedText = UIFactory.CreateText(root, "Speed", "0 КМ/Ч", 46, TextAnchor.UpperRight,
                                         Color.white, topRight, topRight,
                                         new Vector2(-60f, -150f), new Vector2(400f, 60f));

        batteryFill = UIFactory.CreateImage(root, "BatteryFill", Color.green,
                                            topRight, topRight, new Vector2(-260f, -232f),
                                            new Vector2(52f, 24f));
        batteryFill.type = Image.Type.Filled;
        batteryFill.fillMethod = Image.FillMethod.Horizontal;

        batteryText = UIFactory.CreateText(root, "Battery", "100%", 30, TextAnchor.UpperLeft,
                                           Color.white, topRight, topRight,
                                           new Vector2(-196f, -220f), new Vector2(140f, 40f));

        altitudeText = UIFactory.CreateText(root, "Altitude", "0М", 30, TextAnchor.UpperRight,
                                            Color.white, topRight, topRight,
                                            new Vector2(-60f, -220f), new Vector2(140f, 40f));

        signalFill = UIFactory.CreateImage(root, "SignalFill", Color.white,
                                           topRight, topRight, new Vector2(-70f, -80f),
                                           new Vector2(60f, 34f));
        signalFill.type = Image.Type.Filled;
        signalFill.fillMethod = Image.FillMethod.Horizontal;

        missionText = UIFactory.CreateText(root, "Mission", "", 28, TextAnchor.UpperLeft,
                                           Color.white, topLeft, topLeft,
                                           new Vector2(50f, -50f), new Vector2(600f, 100f));

        signalLostBanner = UIFactory.CreateText(root, "SignalLost", "СИГНАЛ ПОТЕРЯН", 54,
                                                TextAnchor.MiddleCenter,
                                                new Color(0.95f, 0.35f, 0.3f),
                                                centre, centre, new Vector2(0f, 90f),
                                                new Vector2(900f, 80f));
        signalLostBanner.gameObject.SetActive(false);

        BuildResultPanel(root);
    }

    /// <summary>
    /// Horizontal scanlines drawn as a stack of thin translucent bars. Cheap,
    /// and it does more for the "this is a video feed" read than anything else.
    /// </summary>
    void BuildScanlines(Transform root)
    {
        var holder = new GameObject("Scanlines");
        holder.transform.SetParent(root, false);
        UIFactory.Stretch(holder);

        const int lines = 90;
        for (int i = 0; i < lines; i++)
        {
            var line = new GameObject("Line" + i);
            line.transform.SetParent(holder.transform, false);

            var image = line.AddComponent<Image>();
            image.sprite = UIFactory.BlankSprite;
            image.color = new Color(0f, 0f, 0f, 0.10f);
            image.raycastTarget = false;

            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, (float)i / lines);
            rect.anchorMax = new Vector2(1f, (float)i / lines + 0.5f / lines);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    Image BuildStatic(Transform root)
    {
        var go = new GameObject("Static");
        go.transform.SetParent(root, false);

        var image = go.AddComponent<Image>();
        image.sprite = BuildNoiseSprite();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;
        image.type = Image.Type.Tiled;

        UIFactory.Stretch(go);
        return image;
    }

    /// <summary>A small tile of random grey, tiled across the screen as interference.</summary>
    Sprite BuildNoiseSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = Random.value;
                texture.SetPixel(x, y, new Color(value, value, value, value * 0.85f));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void BuildCompass(Transform root, Vector2 topCentre)
    {
        var window = new GameObject("CompassWindow");
        window.transform.SetParent(root, false);
        UIFactory.Place(window, topCentre, topCentre, new Vector2(0f, -30f), new Vector2(520f, 46f));

        var mask = window.AddComponent<Image>();
        mask.sprite = UIFactory.BlankSprite;
        mask.color = new Color(0f, 0f, 0f, 0.25f);
        window.AddComponent<RectMask2D>();

        var strip = new GameObject("Strip");
        strip.transform.SetParent(window.transform, false);

        // Three copies of the tape end to end, and every mark measured from the
        // strip's own centre.
        //
        // One copy anchored to the strip's left edge is what made the compass
        // break at both ends: the marks were offset half a tape's width from
        // where the scroll expected them, so the window showed the heading
        // opposite the one being flown, and past either end of the single copy
        // there was simply nothing left to show — the tape went blank rather
        // than wrapping. A compass has no ends, so it needs the copies either
        // side to roll onto.
        compassStrip = UIFactory.Place(strip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       Vector2.zero, new Vector2(CompassLap * 3f, 46f));

        string[] cardinals = { "N", "E", "S", "W" };

        for (int copy = -1; copy <= 1; copy++)
        {
            for (int degrees = 0; degrees < 360; degrees += 15)
            {
                bool cardinal = degrees % 90 == 0;
                float x = (degrees + copy * 360) * CompassScale;
                string id = degrees + "_" + copy;

                if (cardinal)
                {
                    UIFactory.CreateText(strip.transform, "C" + id, cardinals[degrees / 90], 22,
                                         TextAnchor.MiddleCenter, Color.white,
                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                         new Vector2(x, 0f), new Vector2(30f, 30f));
                    continue;
                }

                var tick = UIFactory.CreateImage(strip.transform, "T" + id,
                                                 new Color(1f, 1f, 1f, 0.6f),
                                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                 new Vector2(x, 6f), new Vector2(2f, 12f));
                tick.raycastTarget = false;
            }
        }

        // The index the tape reads against. Without it the numbers slide past
        // nothing in particular.
        var index = UIFactory.CreateImage(root, "CompassIndex", new Color(1f, 0.85f, 0.35f),
                                          topCentre, topCentre, new Vector2(0f, -30f),
                                          new Vector2(2f, 46f));
        index.raycastTarget = false;

        headingText = UIFactory.CreateText(root, "Heading", "000", 24, TextAnchor.MiddleCenter,
                                           Color.white, topCentre, topCentre,
                                           new Vector2(0f, -76f), new Vector2(120f, 30f));
    }

    void BuildCrosshair(Transform root, Vector2 centre)
    {
        var ring = UIFactory.CreateImage(root, "CrosshairRing", new Color(1f, 1f, 1f, 0.85f),
                                         centre, centre, Vector2.zero, new Vector2(58f, 3f));
        ring.raycastTarget = false;

        UIFactory.CreateImage(root, "CrosshairV", new Color(1f, 1f, 1f, 0.85f),
                              centre, centre, Vector2.zero, new Vector2(3f, 58f));

        UIFactory.CreateImage(root, "CrosshairDot", Color.white,
                              centre, centre, Vector2.zero, new Vector2(6f, 6f));
    }

    void BuildResultPanel(Transform root)
    {
        resultPanel = new GameObject("ResultPanel");
        resultPanel.transform.SetParent(root, false);

        var background = resultPanel.AddComponent<Image>();
        background.sprite = UIFactory.BlankSprite;
        background.color = new Color(0f, 0f, 0f, 0.85f);
        UIFactory.Stretch(resultPanel);

        Vector2 centre = new Vector2(0.5f, 0.5f);

        resultTitle = UIFactory.CreateText(resultPanel.transform, "ResultTitle", "", 60,
                                           TextAnchor.MiddleCenter, Color.white,
                                           centre, centre, new Vector2(0f, 160f), new Vector2(900f, 90f));

        resultDetail = UIFactory.CreateText(resultPanel.transform, "ResultDetail", "", 32,
                                            TextAnchor.MiddleCenter, Color.white,
                                            centre, centre, new Vector2(0f, 40f), new Vector2(900f, 160f));

        // The revive offer sits above the restart button, because it is the one
        // the player actually wants after a loss — running the rack dry one
        // drone short of the last target is exactly the moment an extra drone is
        // worth watching something for.
        reviveButton = UIFactory.CreateButton(resultPanel.transform, "ReviveButton",
                                              "+1 ДРОН ЗА РЕКЛАМУ", 30,
                                              centre, centre, new Vector2(0f, -100f),
                                              new Vector2(460f, 74f), RequestExtraDrone);

        var reviveImage = reviveButton.targetGraphic as Image;
        if (reviveImage != null) reviveImage.color = new Color(0.72f, 0.48f, 0.12f);
        reviveLabel = reviveButton.GetComponentInChildren<Text>();

        UIFactory.CreateButton(resultPanel.transform, "RetryButton", "ЗАНОВО", 30,
                               centre, centre, new Vector2(-180f, -196f), new Vector2(330f, 66f),
                               () =>
                               {
                                   if (MissionManager.Instance != null) MissionManager.Instance.Restart();
                               });

        UIFactory.CreateButton(resultPanel.transform, "MenuButton", "В МЕНЮ", 30,
                               centre, centre, new Vector2(180f, -196f), new Vector2(330f, 66f),
                               () =>
                               {
                                   if (MissionManager.Instance != null) MissionManager.Instance.ReturnToMenu();
                               });

        resultPanel.SetActive(false);
    }

    // ---------- state ----------

    /// <summary>
    /// Flashes the lost-link warning. It stays up for the couple of seconds
    /// between the link dropping and the payload self-destructing, so the player
    /// understands why the drone just died rather than seeing it fall for no
    /// visible reason.
    /// </summary>
    void ShowSignalLost()
    {
        if (signalLostBanner == null) return;

        signalLostUntil = Time.time + 2f;
        signalLostBanner.gameObject.SetActive(true);
    }

    void UpdateSignalLostBanner()
    {
        if (signalLostBanner == null || !signalLostBanner.gameObject.activeSelf) return;

        if (Time.time >= signalLostUntil)
        {
            signalLostBanner.gameObject.SetActive(false);
            return;
        }

        // Blink, so it reads as an alarm rather than a label.
        float blink = Mathf.PingPong(Time.time * 6f, 1f);
        Color colour = signalLostBanner.color;
        colour.a = Mathf.Lerp(0.35f, 1f, blink);
        signalLostBanner.color = colour;
    }

    void RefreshMission()
    {
        MissionManager mission = MissionManager.Instance;
        if (mission == null || missionText == null) return;

        string warhead = WarheadProfile.For(mission.warhead).DisplayName;

        missionText.text = "ЦЕЛЕЙ: " + mission.TargetsDestroyed + " / " + mission.TargetsTotal
                           + "\nДРОНОВ: " + mission.DronesRemaining
                           + "\nБОРТ: " + DroneLoadout.Selected.displayName
                           + "\nЗАРЯД: " + warhead;
    }

    void ShowResult(bool won)
    {
        MissionManager mission = MissionManager.Instance;

        resultTitle.text = won ? "МИССИЯ ВЫПОЛНЕНА" : "МИССИЯ ПРОВАЛЕНА";
        resultTitle.color = won ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.95f, 0.4f, 0.35f);

        resultDetail.text = "Уничтожено целей: " + mission.TargetsDestroyed + " из " + mission.TargetsTotal
                            + "\nОчки: " + mission.Score;

        // Nothing to revive into once every target is down, and no offer left
        // once the per-mission cap is spent.
        if (reviveButton != null)
        {
            reviveButton.gameObject.SetActive(!won && mission.CanRequestExtraDrone);
            reviveButton.interactable = true;
        }

        if (reviveLabel != null) reviveLabel.text = "+1 ДРОН ЗА РЕКЛАМУ";

        resultPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Asks for the extra drone and closes the results screen if it was granted.
    /// The mission carries on from where it stopped, so every target already
    /// destroyed stays destroyed — which is what makes the offer worth taking
    /// rather than just restarting.
    /// </summary>
    void RequestExtraDrone()
    {
        MissionManager mission = MissionManager.Instance;
        if (mission == null || reviveButton == null) return;

        reviveButton.interactable = false;
        if (reviveLabel != null) reviveLabel.text = "ЗАГРУЗКА...";

        mission.RequestExtraDrone(granted =>
        {
            if (granted)
            {
                resultPanel.SetActive(false);
                return;
            }

            reviveButton.interactable = true;
            if (reviveLabel != null) reviveLabel.text = "РЕКЛАМА НЕДОСТУПНА";
        });
    }
}
