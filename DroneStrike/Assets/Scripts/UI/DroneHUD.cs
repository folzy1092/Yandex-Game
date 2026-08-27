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

    GameObject pausePanel;
    Text pauseSummary;

    Text signalLostBanner;
    float signalLostUntil;

    /// <summary>
    /// The stretched child that actually holds the video-feed overlay
    /// (scanlines, static, compass, crosshair, telemetry). The signal-loss
    /// glitch nudges this, not the canvas root — a root Screen Space Overlay
    /// canvas has its RectTransform recomputed to fit the screen every frame,
    /// so writing to its anchoredPosition has no visible effect at all.
    /// </summary>
    RectTransform feedRoot;

    /// <summary>Link strength below which the picture starts to glitch — a bit above the point the signal bar itself turns red, so the glitching is the first warning rather than simultaneous with it.</summary>
    const float GlitchThreshold = 0.5f;

    /// <summary>Time.time of the next scheduled glitch tick.</summary>
    float nextGlitchTime;

    /// <summary>Time.time the current glitch jolt holds until.</summary>
    float glitchEndTime;

    /// <summary>Static-overlay alpha to hold for the rest of the current glitch.</summary>
    float glitchAlpha;

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
        HandleEscape();
        UpdateSignalLostBanner();

        MissionManager mission = MissionManager.Instance;
        DroneRig drone = mission != null ? mission.ActiveDrone : null;

        if (drone == null || drone.Controller == null)
        {
            if (staticOverlay != null) staticOverlay.color = new Color(1f, 1f, 1f, 0.35f);
            // No drone to lose signal to, so nothing should be mid-glitch —
            // otherwise the next drone could launch into a leftover jolt.
            if (feedRoot != null) feedRoot.anchoredPosition = Vector2.zero;
            glitchEndTime = 0f;
            return;
        }

        UpdateTelemetry(drone);
        UpdateSignal(drone);
    }

    /// <summary>
    /// DroneHUD is the sole owner of Esc during a mission — see the matching
    /// comment in MissionManager.Update(). It only opens the pause panel while
    /// a mission is actually in progress and the result screen is not already
    /// covering it, and it is a toggle so the same key closes the panel again.
    /// </summary>
    void HandleEscape()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        MissionManager mission = MissionManager.Instance;
        if (mission == null || !mission.IsRunning) return;
        if (resultPanel != null && resultPanel.activeSelf) return;

        TogglePause();
    }

    void TogglePause()
    {
        if (pausePanel == null) return;

        if (pausePanel.activeSelf)
        {
            ClosePause();
            return;
        }

        Time.timeScale = 0f;
        RefreshPauseSummary();
        pausePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ClosePause()
    {
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UpdateTelemetry(DroneRig drone)
    {
        speedText.text = Localization.F("hud.kmh", Mathf.RoundToInt(drone.Controller.SpeedKmh));
        altitudeText.text = Localization.F("hud.alt", Mathf.RoundToInt(drone.Controller.AltitudeMetres));

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

        UpdateGlitch(strength);
    }

    /// <summary>
    /// A weak link does not fade smoothly, it drops out — a horizontal jolt of
    /// the feed, a cut to near-solid snow, and the noise pattern jumping to a
    /// new scale, held for a couple of frames and then snapped back. That is
    /// what a real broken downlink looks like; a uniform alpha fade never
    /// reads as a dropout, only as a dimmer picture.
    /// </summary>
    void UpdateGlitch(float strength)
    {
        if (Time.time < glitchEndTime)
        {
            // Hold the "cut to snow" look for the rest of the jolt — the
            // flicker above already overwrote staticOverlay's colour this
            // frame, so it has to be re-applied every held frame, not just
            // the one that triggered it.
            staticOverlay.color = new Color(1f, 1f, 1f, glitchAlpha);
            return;
        }

        feedRoot.anchoredPosition = Vector2.zero;

        if (strength >= GlitchThreshold)
        {
            // Healthy link: keep the schedule pinned to "now" so a glitch
            // fires immediately if the link degrades again, rather than
            // waiting out an interval that was rolled while it was still bad.
            nextGlitchTime = Time.time;
            return;
        }

        if (Time.time < nextGlitchTime) return;

        // 0 at the threshold, 1 at total loss — drives both how often glitches
        // fire and how rough each one is, so a dying link glitches almost
        // continuously instead of ticking over at a fixed rate.
        float severity = Mathf.InverseLerp(GlitchThreshold, 0f, strength);
        float interval = Mathf.Lerp(1.1f, 0.06f, severity);

        nextGlitchTime = Time.time + interval * (0.5f + Random.value);
        glitchEndTime = Time.time + Mathf.Lerp(0.03f, 0.12f, severity);
        glitchAlpha = Mathf.Lerp(0.85f, 1f, severity);

        feedRoot.anchoredPosition = new Vector2(Random.Range(-16f, 16f) * (0.35f + severity), 0f);
        staticOverlay.color = new Color(1f, 1f, 1f, glitchAlpha);

        // Cheap stand-in for a UV jump: Image has no exposed tile offset, but
        // rescaling how large each tile reads makes the same noise texture
        // jump to a different-looking pattern without touching the texture
        // itself.
        staticOverlay.pixelsPerUnitMultiplier = Random.Range(0.7f, 1.8f);
    }

    // ---------- construction ----------

    void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("DroneHUD", 0);
        Transform canvasRoot = canvas.transform;

        // A stretched child rather than the canvas root itself: Screen Space
        // Overlay canvases recompute their own RectTransform to fit the
        // screen every frame, so the signal-loss glitch below has nothing to
        // nudge unless the video-feed overlay lives one level down from it.
        // The pause and result panels stay on canvasRoot so a glitch never
        // shakes them.
        var feedGO = new GameObject("Feed");
        feedGO.transform.SetParent(canvasRoot, false);
        feedRoot = UIFactory.Stretch(feedGO);
        Transform root = feedRoot;

        Vector2 centre = new Vector2(0.5f, 0.5f);
        Vector2 topCentre = new Vector2(0.5f, 1f);
        Vector2 topRight = new Vector2(1f, 1f);
        Vector2 topLeft = new Vector2(0f, 1f);

        BuildScanlines(root);
        staticOverlay = BuildStatic(root);

        BuildCompass(root, topCentre);
        BuildCrosshair(root, centre);

        speedText = UIFactory.CreateText(root, "Speed", Localization.F("hud.kmh", 0), 46, TextAnchor.UpperRight,
                                         Color.white, topRight, topRight,
                                         new Vector2(-60f, -150f), new Vector2(400f, 60f));

        // An outline and a dark track behind the fill — without them there was
        // nothing marking how much bar there used to be, so a half-empty
        // battery and a nearly-full one both just looked like "a green bar",
        // legible only by reading the percentage next to it.
        UIFactory.CreateImage(root, "BatteryOutline", new Color(1f, 1f, 1f, 0.55f),
                             topRight, topRight, new Vector2(-258f, -230f),
                             new Vector2(56f, 28f));

        UIFactory.CreateImage(root, "BatteryTrack", new Color(0.05f, 0.06f, 0.05f, 0.9f),
                             topRight, topRight, new Vector2(-260f, -232f),
                             new Vector2(52f, 24f));

        batteryFill = UIFactory.CreateImage(root, "BatteryFill", Color.green,
                                            topRight, topRight, new Vector2(-260f, -232f),
                                            new Vector2(52f, 24f));
        batteryFill.type = Image.Type.Filled;
        batteryFill.fillMethod = Image.FillMethod.Horizontal;

        // Right-aligned rather than left-aligned, and its box sits entirely
        // left of the fill pill instead of straddling it — a left-aligned box
        // that overlapped the pill only worked by coincidence for short
        // values ("9%"); "100%" is wide enough to draw straight across the
        // green fill it is meant to sit beside. Right-aligning means the text
        // grows further left as it gets wider, never any closer to the pill.
        batteryText = UIFactory.CreateText(root, "Battery", "100%", 30, TextAnchor.UpperRight,
                                           Color.white, topRight, topRight,
                                           new Vector2(-322f, -220f), new Vector2(140f, 40f));

        altitudeText = UIFactory.CreateText(root, "Altitude", Localization.F("hud.alt", 0), 30, TextAnchor.UpperRight,
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

        signalLostBanner = UIFactory.CreateText(root, "SignalLost", Localization.T("hud.signal_lost"), 54,
                                                TextAnchor.MiddleCenter,
                                                new Color(0.95f, 0.35f, 0.3f),
                                                centre, centre, new Vector2(0f, 90f),
                                                new Vector2(900f, 80f));
        signalLostBanner.gameObject.SetActive(false);

        BuildResultPanel(canvasRoot);
        BuildPausePanel(canvasRoot);
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
                                              Localization.T("hud.revive"), 30,
                                              centre, centre, new Vector2(0f, -100f),
                                              new Vector2(460f, 74f), RequestExtraDrone);

        var reviveImage = reviveButton.targetGraphic as Image;
        if (reviveImage != null) reviveImage.color = new Color(0.72f, 0.48f, 0.12f);
        reviveLabel = reviveButton.GetComponentInChildren<Text>();

        UIFactory.CreateButton(resultPanel.transform, "RetryButton", Localization.T("hud.retry"), 30,
                               centre, centre, new Vector2(-180f, -196f), new Vector2(330f, 66f),
                               () =>
                               {
                                   if (MissionManager.Instance != null) MissionManager.Instance.Restart();
                               });

        UIFactory.CreateButton(resultPanel.transform, "MenuButton", Localization.T("hud.menu"), 30,
                               centre, centre, new Vector2(180f, -196f), new Vector2(330f, 66f),
                               () =>
                               {
                                   if (MissionManager.Instance != null) MissionManager.Instance.ReturnToMenu();
                               });

        resultPanel.SetActive(false);
    }

    /// <summary>
    /// Same visual language as <see cref="BuildResultPanel"/> — dark
    /// translucent backdrop, same title/body sizes, same button footprint —
    /// so pausing does not look like a different screen bolted onto the HUD.
    /// </summary>
    void BuildPausePanel(Transform root)
    {
        pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(root, false);

        var background = pausePanel.AddComponent<Image>();
        background.sprite = UIFactory.BlankSprite;
        background.color = new Color(0f, 0f, 0f, 0.85f);
        UIFactory.Stretch(pausePanel);

        Vector2 centre = new Vector2(0.5f, 0.5f);

        UIFactory.CreateText(pausePanel.transform, "PauseTitle", Localization.T("hud.pause"), 60,
                             TextAnchor.MiddleCenter, Color.white,
                             centre, centre, new Vector2(0f, 160f), new Vector2(900f, 90f));

        pauseSummary = UIFactory.CreateText(pausePanel.transform, "PauseSummary", "", 32,
                                            TextAnchor.MiddleCenter, Color.white,
                                            centre, centre, new Vector2(0f, 40f), new Vector2(900f, 160f));

        UIFactory.CreateButton(pausePanel.transform, "ResumeButton", Localization.T("hud.resume"), 30,
                               centre, centre, new Vector2(-180f, -140f), new Vector2(330f, 66f),
                               ClosePause);

        UIFactory.CreateButton(pausePanel.transform, "PauseMenuButton", Localization.T("hud.menu"), 30,
                               centre, centre, new Vector2(180f, -140f), new Vector2(330f, 66f),
                               () =>
                               {
                                   // The mission scene is paused, but the menu is not — it must not
                                   // inherit a frozen clock from the screen it was opened over.
                                   Time.timeScale = 1f;
                                   if (MissionManager.Instance != null) MissionManager.Instance.ReturnToMenu();
                               });

        pausePanel.SetActive(false);
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

    /// <summary>The same targets/drones/airframe/charge readout used by both the corner HUD text and the pause summary — one format, so the two never drift apart.</summary>
    static string BuildMissionSummary(MissionManager mission)
    {
        string warhead = WarheadProfile.For(mission.warhead).DisplayName;

        return Localization.F("hud.summary",
            mission.TargetsDestroyed,
            mission.TargetsTotal,
            mission.DronesRemaining,
            Localization.DroneName(DroneLoadout.Selected.id),
            warhead);
    }

    void RefreshMission()
    {
        MissionManager mission = MissionManager.Instance;
        if (mission == null || missionText == null) return;

        missionText.text = BuildMissionSummary(mission);
    }

    void RefreshPauseSummary()
    {
        MissionManager mission = MissionManager.Instance;
        if (mission == null || pauseSummary == null) return;

        pauseSummary.text = BuildMissionSummary(mission);
    }

    void ShowResult(bool won)
    {
        MissionManager mission = MissionManager.Instance;

        // The mission cannot normally end while paused — Time.timeScale being
        // 0 stops the physics and the WaitForSeconds coroutines that end it —
        // but if that ever changes, the pause panel must not be left sitting
        // on top of the result screen.
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;

        resultTitle.text = won ? Localization.T("hud.win") : Localization.T("hud.lose");
        resultTitle.color = won ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.95f, 0.4f, 0.35f);

        resultDetail.text = Localization.F("hud.result",
            mission.TargetsDestroyed, mission.TargetsTotal, mission.Score);

        // Nothing to revive into once every target is down, and no offer left
        // once the per-mission cap is spent.
        if (reviveButton != null)
        {
            reviveButton.gameObject.SetActive(!won && mission.CanRequestExtraDrone);
            reviveButton.interactable = true;
        }

        if (reviveLabel != null) reviveLabel.text = Localization.T("hud.revive");

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
        if (reviveLabel != null) reviveLabel.text = Localization.T("hud.loading");

        mission.RequestExtraDrone(granted =>
        {
            if (granted)
            {
                resultPanel.SetActive(false);
                return;
            }

            reviveButton.interactable = true;
            if (reviveLabel != null) reviveLabel.text = Localization.T("hud.ad_failed");
        });
    }
}
