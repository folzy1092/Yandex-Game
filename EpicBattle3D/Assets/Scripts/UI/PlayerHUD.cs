using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crosshair, hitmarker, health bar, ammo counter and the death screen.
/// Builds itself at runtime.
///
/// The death screen carries the game's rewarded-ad offer: watching a video
/// respawns you immediately instead of waiting out the timer. The offer is
/// optional in the strict sense — the timer keeps running behind it, so a player
/// who ignores the button loses nothing.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    public Health health;
    public WeaponController weapon;

    Image healthFill;
    Text healthText;
    Text ammoText;

    RectTransform hitmarker;
    Image[] hitmarkerArms;
    float hitmarkerRemaining;
    float hitmarkerDuration;

    GameObject deathPanel;
    Text deathText;
    Button rewardButton;
    bool rewardOfferUsed;

    void Start()
    {
        UIFactory.EnsureEventSystem();
        Build();

        if (health != null)
        {
            health.OnHealthChanged += UpdateHealth;
            health.OnDied += HandleDied;
            health.OnRespawned += HandleRespawned;
            UpdateHealth(health.CurrentHealth, health.maxHealth);
        }

        if (weapon != null)
        {
            weapon.OnAmmoChanged += UpdateAmmo;
            weapon.OnHitConfirmed += ShowHitmarker;
            weapon.OnKillConfirmed += ShowKillMarker;
            UpdateAmmo(weapon.CurrentAmmo, weapon.magazineSize);
        }

        // The first playable frame is the right moment to tell Yandex the game
        // has loaded, so its loading indicator goes away.
        YandexAds.NotifyGameReady();
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= UpdateHealth;
            health.OnDied -= HandleDied;
            health.OnRespawned -= HandleRespawned;
        }

        if (weapon != null)
        {
            weapon.OnAmmoChanged -= UpdateAmmo;
            weapon.OnHitConfirmed -= ShowHitmarker;
            weapon.OnKillConfirmed -= ShowKillMarker;
        }
    }

    void Update()
    {
        UpdateHitmarker();

        if (deathPanel == null || !deathPanel.activeSelf || health == null) return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, health.RespawnTimeRemaining));
        deathText.text = Localization.Get("you_died") + "\n\n" + Localization.Get("respawn_in") + " " + seconds;
    }

    void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas("PlayerHUD", 0);
        Transform root = canvas.transform;

        Vector2 centre = new Vector2(0.5f, 0.5f);
        Vector2 bottomLeft = new Vector2(0f, 0f);
        Vector2 bottomRight = new Vector2(1f, 0f);

        BuildCrosshair(root, centre);
        BuildHitmarker(root, centre);

        UIFactory.CreateImage(root, "HealthBackground", new Color(0f, 0f, 0f, 0.55f),
                              bottomLeft, bottomLeft, new Vector2(40f, 40f), new Vector2(320f, 34f));

        healthFill = UIFactory.CreateImage(root, "HealthFill", new Color(0.85f, 0.25f, 0.25f),
                                           bottomLeft, bottomLeft, new Vector2(44f, 44f), new Vector2(312f, 26f));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        healthText = UIFactory.CreateText(root, "HealthText", "100", 22, TextAnchor.MiddleLeft, Color.white,
                                          bottomLeft, bottomLeft, new Vector2(56f, 44f), new Vector2(200f, 26f));

        ammoText = UIFactory.CreateText(root, "AmmoText", "12 / 12", 30, TextAnchor.LowerRight, Color.white,
                                        bottomRight, bottomRight, new Vector2(-40f, 40f), new Vector2(260f, 40f));

        BuildDeathPanel(root);
    }

    void BuildCrosshair(Transform root, Vector2 centre)
    {
        var faint = new Color(1f, 1f, 1f, 0.75f);

        UIFactory.CreateImage(root, "CrosshairDot", Color.white, centre, centre,
                              Vector2.zero, new Vector2(3f, 3f));

        UIFactory.CreateImage(root, "CrosshairLeft", faint, centre, centre,
                              new Vector2(-13f, 0f), new Vector2(10f, 2f));
        UIFactory.CreateImage(root, "CrosshairRight", faint, centre, centre,
                              new Vector2(13f, 0f), new Vector2(10f, 2f));
        UIFactory.CreateImage(root, "CrosshairUp", faint, centre, centre,
                              new Vector2(0f, 13f), new Vector2(2f, 10f));
        UIFactory.CreateImage(root, "CrosshairDown", faint, centre, centre,
                              new Vector2(0f, -13f), new Vector2(2f, 10f));
    }

    /// <summary>
    /// Four short diagonal strokes forming an X, the shape shooters have used for
    /// hit confirmation for decades. Hidden until a shot connects.
    /// </summary>
    void BuildHitmarker(Transform root, Vector2 centre)
    {
        var holder = new GameObject("Hitmarker");
        holder.transform.SetParent(root, false);
        hitmarker = UIFactory.Place(holder, centre, centre, Vector2.zero, new Vector2(60f, 60f));

        hitmarkerArms = new Image[4];
        float[] angles = { 45f, 135f, 225f, 315f };

        for (int i = 0; i < angles.Length; i++)
        {
            Image arm = UIFactory.CreateImage(holder.transform, "Arm" + i, Color.white,
                                              centre, centre, Vector2.zero, new Vector2(12f, 3f));

            // Push each stroke out along its own diagonal, leaving a gap in the middle.
            float radians = angles[i] * Mathf.Deg2Rad;
            arm.rectTransform.anchoredPosition =
                new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 15f;
            arm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);

            hitmarkerArms[i] = arm;
        }

        holder.SetActive(false);
    }

    void BuildDeathPanel(Transform root)
    {
        deathPanel = new GameObject("DeathPanel");
        deathPanel.transform.SetParent(root, false);

        var background = deathPanel.AddComponent<Image>();
        background.sprite = UIFactory.BlankSprite;
        background.color = new Color(0.35f, 0f, 0f, 0.55f);
        UIFactory.Stretch(deathPanel);

        Vector2 centre = new Vector2(0.5f, 0.5f);

        deathText = UIFactory.CreateText(deathPanel.transform, "DeathText", "", 46, TextAnchor.MiddleCenter,
                                         Color.white, centre, centre, new Vector2(0f, 80f), new Vector2(900f, 200f));

        rewardButton = UIFactory.CreateButton(deathPanel.transform, "RewardButton",
                                              Localization.Get("respawn_now"), 26,
                                              centre, centre, new Vector2(0f, -80f), new Vector2(520f, 70f),
                                              WatchAdToRespawn);

        deathPanel.SetActive(false);
    }

    // ---------- hitmarker ----------

    void ShowHitmarker(bool headshot)
    {
        FlashHitmarker(headshot ? new Color(1f, 0.85f, 0.2f) : Color.white,
                       headshot ? 0.35f : 0.25f);
    }

    void ShowKillMarker()
    {
        FlashHitmarker(new Color(1f, 0.25f, 0.2f), 0.5f);
    }

    void FlashHitmarker(Color color, float duration)
    {
        if (hitmarker == null) return;

        for (int i = 0; i < hitmarkerArms.Length; i++)
            hitmarkerArms[i].color = color;

        hitmarkerDuration = duration;
        hitmarkerRemaining = duration;
        hitmarker.localScale = Vector3.one * 1.4f;
        hitmarker.gameObject.SetActive(true);
    }

    void UpdateHitmarker()
    {
        if (hitmarker == null || hitmarkerRemaining <= 0f) return;

        hitmarkerRemaining -= Time.deltaTime;
        if (hitmarkerRemaining <= 0f)
        {
            hitmarker.gameObject.SetActive(false);
            return;
        }

        float fade = hitmarkerRemaining / hitmarkerDuration;

        // Snaps in large and settles to normal size, which reads as an impact.
        hitmarker.localScale = Vector3.one * Mathf.Lerp(1f, 1.4f, fade);

        for (int i = 0; i < hitmarkerArms.Length; i++)
        {
            Color color = hitmarkerArms[i].color;
            color.a = Mathf.Clamp01(fade * 1.6f);
            hitmarkerArms[i].color = color;
        }
    }

    // ---------- death screen ----------

    void WatchAdToRespawn()
    {
        if (rewardOfferUsed) return;
        rewardOfferUsed = true;
        rewardButton.interactable = false;

        YandexAds.ShowRewarded(watched =>
        {
            if (watched && health != null) health.SkipRespawnWait();
        });
    }

    void HandleDied(GameObject killer)
    {
        if (deathPanel == null) return;

        rewardOfferUsed = false;

        // Hide the offer when there is no ad to show, rather than dangling a
        // button that does nothing.
        rewardButton.gameObject.SetActive(YandexAds.IsAvailable);
        rewardButton.interactable = true;

        deathPanel.SetActive(true);
    }

    void HandleRespawned()
    {
        if (deathPanel != null) deathPanel.SetActive(false);
        if (GameAudio.Instance != null) GameAudio.Instance.PlayRespawn(transform.position);
    }

    void UpdateHealth(int current, int max)
    {
        if (healthFill != null) healthFill.fillAmount = max > 0 ? (float)current / max : 0f;
        if (healthText != null) healthText.text = current.ToString();
    }

    void UpdateAmmo(int current, int max)
    {
        if (ammoText != null) ammoText.text = current + " / " + max;
    }
}
