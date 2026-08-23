using UnityEngine;

/// <summary>
/// Builds bot GameObjects at runtime. Bots are created in code rather than from
/// a prefab so the whole game can be produced without any manual editor work.
/// </summary>
public static class BotFactory
{
    static readonly Color[] Palette =
    {
        new Color(0.85f, 0.25f, 0.25f),
        new Color(0.25f, 0.55f, 0.90f),
        new Color(0.30f, 0.75f, 0.35f),
        new Color(0.90f, 0.70f, 0.20f),
        new Color(0.70f, 0.35f, 0.85f),
        new Color(0.25f, 0.80f, 0.80f),
        new Color(0.95f, 0.50f, 0.20f),
        new Color(0.60f, 0.60f, 0.65f),
        new Color(0.85f, 0.40f, 0.60f),
        new Color(0.45f, 0.85f, 0.55f)
    };

    public static GameObject Create(string name, Vector3 position, int colorIndex)
    {
        var bot = new GameObject(name);
        bot.transform.position = position;
        bot.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        int characterLayer = GameLayers.Character;
        if (characterLayer >= 0) bot.layer = characterLayer;

        var controller = bot.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.slopeLimit = 50f;
        controller.stepOffset = 0.4f;

        Color color = Palette[colorIndex % Palette.Length];
        Material bodyMaterial = TintedMaterial("Materials/Mat_Bot", color);
        // The head is a shade darker, which reads as a helmet and makes the
        // headshot target easy to pick out at a distance.
        Material headMaterial = TintedMaterial("Materials/Mat_Bot", color * 0.55f);

        var botController = bot.AddComponent<BotController>();

        var health = bot.AddComponent<Health>();
        health.maxHealth = 100;
        health.respawnDelay = 3f;
        health.disableOnDeath = new MonoBehaviour[] { botController };

        CharacterModel.Parts parts = CharacterModel.Build(bot, health, bodyMaterial, headMaterial, false);

        Material gunMaterial = Resources.Load<Material>("Materials/Mat_Gun");
        Material gunAccent = Resources.Load<Material>("Materials/Mat_GunAccent");
        Transform muzzle = PistolModel.Build(parts.weaponMount, gunMaterial, gunAccent, 1f);
        botController.muzzle = muzzle;

        var animator = bot.AddComponent<CharacterAnimator>();
        animator.leftLegPivot = parts.leftLegPivot;
        animator.rightLegPivot = parts.rightLegPivot;
        animator.leftArmPivot = parts.leftArmPivot;
        animator.rightArmPivot = parts.rightArmPivot;

        return bot;
    }

    /// <summary>
    /// Copies the shared bot material and recolours it. Loaded from Resources
    /// rather than built with Shader.Find so the shader is certain to be
    /// included in the WebGL build.
    /// </summary>
    static Material TintedMaterial(string resourcePath, Color color)
    {
        Material template = Resources.Load<Material>(resourcePath);
        Material material = template != null
            ? new Material(template)
            : new Material(Shader.Find("Standard"));

        material.color = color;
        return material;
    }
}
