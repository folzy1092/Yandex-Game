using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crosshair, health bar, ammo counter and the death screen. Builds itself at runtime.
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

        if (weapon != null) weapon.OnAmmoChanged -= UpdateAmmo;
    }

    void Update()
    {
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

        // Crosshair: a small dot plus two short bars.
        UIFactory.CreateImage(root, "CrosshairDot", Color.white, centre, centre, Vector2.zero, new Vector2(4f, 4f));
        UIFactory.CreateImage(root, "CrosshairH", new Color(1f, 1f, 1f, 0.7f), centre, centre,
                              Vector2.zero, new Vector2(22f, 2f));
        UIFactory.CreateImage(root, "CrosshairV", new Color(1f, 1f, 1f, 0.7f), centre, centre,
                              Vector2.zero, new Vector2(2f, 22f));

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

    void BuildDeathPanel(Transform root)
    {
        deathPanel = new GameObject("DeathPanel");
        deathPanel.transform.SetParent(root, false);

        var background = deathPanel.AddComponent<Image>();
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
