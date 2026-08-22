using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crosshair, health bar and ammo counter. Builds itself at runtime.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    public Health health;
    public WeaponController weapon;

    Image healthFill;
    Text healthText;
    Text ammoText;

    void Start()
    {
        UIFactory.EnsureEventSystem();
        Build();

        if (health != null)
        {
            health.OnHealthChanged += UpdateHealth;
            UpdateHealth(health.CurrentHealth, health.maxHealth);
        }

        if (weapon != null)
        {
            weapon.OnAmmoChanged += UpdateAmmo;
            UpdateAmmo(weapon.CurrentAmmo, weapon.magazineSize);
        }
    }

    void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= UpdateHealth;
        if (weapon != null) weapon.OnAmmoChanged -= UpdateAmmo;
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
