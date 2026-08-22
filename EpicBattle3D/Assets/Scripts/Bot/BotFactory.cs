using UnityEngine;

/// <summary>
/// Builds bot GameObjects from primitives at runtime. Bots are created in code
/// rather than from a prefab so the whole game can be produced without any
/// manual work in the Unity editor.
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

        var controller = bot.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        Color color = Palette[colorIndex % Palette.Length];
        Material material = CreateMaterial(color);

        AddVisual(bot.transform, PrimitiveType.Capsule, "Body",
                  new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), material);

        // A small block on the front so you can tell which way a bot is facing.
        AddVisual(bot.transform, PrimitiveType.Cube, "Muzzle",
                  new Vector3(0f, 1.4f, 0.45f), new Vector3(0.18f, 0.18f, 0.5f), material);

        var botController = bot.AddComponent<BotController>();

        var health = bot.AddComponent<Health>();
        health.maxHealth = 100;
        health.respawnDelay = 3f;
        health.disableOnDeath = new MonoBehaviour[] { botController };

        return bot;
    }

    static Material CreateMaterial(Color color)
    {
        // Loaded from Resources rather than Shader.Find so the shader is
        // guaranteed to be included in the WebGL build.
        Material template = Resources.Load<Material>("Materials/Mat_Bot");
        Material material = template != null
            ? new Material(template)
            : new Material(Shader.Find("Standard"));

        material.color = color;
        return material;
    }

    static void AddVisual(Transform parent, PrimitiveType type, string name,
                          Vector3 localPosition, Vector3 localScale, Material material)
    {
        var visual = GameObject.CreatePrimitive(type);
        visual.name = name;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = localScale;

        // The CharacterController is the only collider a bot needs; leaving the
        // primitive colliders on would make shots register twice.
        Object.Destroy(visual.GetComponent<Collider>());

        visual.GetComponent<Renderer>().sharedMaterial = material;
    }
}
