using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates every texture and material the game uses and saves them as project
/// assets.
///
/// They have to be real assets, not objects made on the fly: a material created
/// at runtime and assigned into a scene leaves a broken reference the next time
/// that scene is opened.
/// </summary>
public static class DroneMaterials
{
    public const string TextureFolder = "Assets/Resources/Textures";
    public const string MaterialFolder = "Assets/Resources/Materials";

    [MenuItem("Tools/Drone Strike/1 - Generate Materials")]
    public static void Generate()
    {
        Directory.CreateDirectory(TextureFolder);
        Directory.CreateDirectory(MaterialFolder);

        BuildGroundMaterials();
        BuildStructureMaterials();
        BuildVehicleMaterials();
        BuildDroneMaterials();
        BuildEffectMaterials();
        BuildDownloadedEffectMaterials();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Drone Strike: textures and materials generated in Assets/Resources.");
    }

    static void BuildGroundMaterials()
    {
        // One ground tone per map, tiled hard because the ground mesh is
        // hundreds of metres across. Three maps built from the same generator
        // read as reskins of each other if the grass under them is identical —
        // a different tint is the cheapest thing that actually varies.
        SaveSurface("Ground",
            ProceduralTextures.CreateConcrete(512, new Color(0.32f, 0.42f, 0.24f), 0.45f, 4001, 0.5f),
            new Vector2(60f, 60f), 0.05f);

        SaveSurface("GroundForest",
            ProceduralTextures.CreateConcrete(512, new Color(0.20f, 0.32f, 0.16f), 0.55f, 4011, 0.4f),
            new Vector2(60f, 60f), 0.04f);

        SaveSurface("GroundDusk",
            ProceduralTextures.CreateConcrete(512, new Color(0.34f, 0.34f, 0.20f), 0.42f, 4012, 0.5f),
            new Vector2(60f, 60f), 0.05f);

        SaveSurface("Asphalt",
            ProceduralTextures.CreateConcrete(512, new Color(0.24f, 0.24f, 0.26f), 0.30f, 4002, 0.6f),
            new Vector2(6f, 6f), 0.12f);

        // A worn asphalt for the dusk map — older, patchier, less saturated.
        SaveSurface("AsphaltWorn",
            ProceduralTextures.CreateConcrete(512, new Color(0.18f, 0.17f, 0.17f), 0.42f, 4013, 0.5f),
            new Vector2(6f, 6f), 0.08f);

        SaveFlat("Mat_Water", new Color(0.16f, 0.30f, 0.38f), 0.85f);
    }

    static void BuildStructureMaterials()
    {
        SaveSurface("Concrete",
            ProceduralTextures.CreateConcrete(512, new Color(0.62f, 0.61f, 0.58f), 0.28f, 4003, 0.55f),
            new Vector2(4f, 4f), 0.08f);

        SaveSurface("RustMetal",
            ProceduralTextures.CreateConcrete(512, new Color(0.45f, 0.33f, 0.24f), 0.40f, 4004, 0.7f),
            new Vector2(3f, 3f), 0.35f, 0.45f);

        SaveSurface("Roof",
            ProceduralTextures.CreateTiles(512, 10, new Color(0.38f, 0.39f, 0.41f),
                                           new Color(0.26f, 0.27f, 0.29f), 0.06f, 4005, 0.5f),
            new Vector2(5f, 5f), 0.25f);

        SaveFlat("Mat_Foliage", new Color(0.20f, 0.34f, 0.17f), 0.10f);
        SaveFlat("Mat_TreeTrunk", new Color(0.28f, 0.22f, 0.16f), 0.08f);
        SaveFlat("Mat_Burnt", new Color(0.09f, 0.08f, 0.08f), 0.15f);

        // A target that survives a hit but is not dead has to look different
        // from both "untouched" and "wrecked", or a solid hit on armour reads as
        // a miss. Charcoal rather than pure black — it is scorched, not burnt out.
        SaveFlat("Mat_Damaged", new Color(0.20f, 0.19f, 0.18f), 0.10f);

        // A faint marker under every target, so a vehicle tucked in shade or
        // behind netting still catches the eye from altitude.
        SaveAdditive("Mat_Highlight", RadialGlow(96, Color.white, 0), new Color(0.55f, 0.85f, 1f));

        // Road markings. Nearly white and slightly glossy, so the lines still
        // read from altitude where the asphalt underneath has gone flat grey.
        SaveFlat("Mat_RoadLine", new Color(0.88f, 0.87f, 0.82f), 0.30f);

        // Field positions: churned earth for the berms, hessian for the
        // sandbags, and netting dark enough to read as shade from above.
        SaveSurface("Dirt",
            ProceduralTextures.CreateConcrete(512, new Color(0.34f, 0.27f, 0.19f), 0.42f, 4006, 0.65f),
            new Vector2(3f, 3f), 0.05f);

        SaveFlat("Mat_Sandbag", new Color(0.48f, 0.44f, 0.31f), 0.06f);
        SaveFlat("Mat_CamoNet", new Color(0.19f, 0.24f, 0.15f), 0.05f);
    }

    static void BuildVehicleMaterials()
    {
        // Military olive, and a lighter shade so vehicle details read at distance.
        SaveFlat("Mat_Vehicle", new Color(0.27f, 0.30f, 0.20f), 0.20f);
        SaveFlat("Mat_VehicleDark", new Color(0.16f, 0.18f, 0.13f), 0.25f);
        SaveFlat("Mat_Crate", new Color(0.42f, 0.36f, 0.22f), 0.12f);
    }

    static void BuildDroneMaterials()
    {
        SaveFlat("Mat_DroneFrame", new Color(0.12f, 0.12f, 0.13f), 0.45f, 0.5f);
        SaveFlat("Mat_DroneAccent", new Color(0.15f, 0.45f, 0.75f), 0.55f, 0.3f);

        // Olive drab, the way a real ordnance body is painted.
        SaveFlat("Mat_Warhead", new Color(0.24f, 0.26f, 0.16f), 0.2f);
        SaveFlat("Mat_WarheadBand", new Color(0.55f, 0.1f, 0.08f), 0.15f);

        // Props are near-invisible while spinning, so they only need to be dark
        // and slightly translucent-looking.
        SaveFlat("Mat_Propeller", new Color(0.20f, 0.20f, 0.22f), 0.35f);
    }

    /// <summary>
    /// Effect materials, copied in behaviour from the shooter: unlit and additive,
    /// because an explosion that responds to scene lighting looks dull and a
    /// muzzle flash has to read as its own light source.
    /// </summary>
    static void BuildEffectMaterials()
    {
        Texture2D flash = RadialGlow(128, new Color(1f, 0.92f, 0.65f), 6);
        SaveAdditive("Mat_Muzzle", flash, new Color(1f, 0.85f, 0.5f));

        // A fresh texture per material, not one shared instance: CreateAsset takes
        // ownership of the object it is given, so saving the same Texture2D under
        // a second path fails outright.
        SaveAdditive("Mat_Tracer", RadialGlow(64, Color.white, 0), new Color(1f, 0.9f, 0.6f));
        SaveAdditive("Mat_Spark", RadialGlow(64, Color.white, 0), new Color(1f, 0.78f, 0.3f));

        // Dust, not blood — the targets here are vehicles.
        SaveTransparent("Mat_Blood", RadialGlow(64, Color.white, 0), new Color(0.25f, 0.22f, 0.20f));
        SaveTransparent("Mat_BulletHole", ScorchMark(64), new Color(0.06f, 0.05f, 0.05f));
    }

    const string ParticleFolder = "Assets/Resources/Textures/Particles";

    /// <summary>
    /// Materials built from real sprites (Kenney's Particle Pack, CC0 — see
    /// DroneStrike/CREDITS.txt) rather than the procedural radial gradient.
    ///
    /// A 64px generated glow reads fine as a spark, but blown up to the size a
    /// death cloud needs it shows its pixels and looks like a flat coloured
    /// square rather than smoke. A painted sprite with an irregular, feathered
    /// edge is what actually reads as a puff at that scale.
    /// </summary>
    static void BuildDownloadedEffectMaterials()
    {
        SaveAdditiveFromFile("Mat_FireReal", "fire_01", new Color(1f, 0.55f, 0.2f));
        SaveTransparentFromFile("Mat_SmokeReal", "smoke_04", new Color(0.30f, 0.29f, 0.28f));
        SaveTransparentFromFile("Mat_ScorchGround", "scorch_01", new Color(0.05f, 0.05f, 0.05f));
    }

    static void SaveAdditiveFromFile(string materialName, string fileName, Color tint)
    {
        Texture2D texture = LoadDownloadedTexture(fileName);
        if (texture == null) return;

        var material = new Material(EffectShader());
        material.mainTexture = texture;
        material.color = tint;

        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 4f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;

        Save(material, materialName);
    }

    static void SaveTransparentFromFile(string materialName, string fileName, Color tint)
    {
        Texture2D texture = LoadDownloadedTexture(fileName);
        if (texture == null) return;

        var material = new Material(EffectShader());
        material.mainTexture = texture;
        material.color = tint;

        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;

        Save(material, materialName);
    }

    static Texture2D LoadDownloadedTexture(string fileName)
    {
        string path = ParticleFolder + "/" + fileName + ".png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        if (texture == null)
            Debug.LogWarning("Drone Strike: " + path + " not found — the fire/smoke sprites did not "
                             + "come through the sync. Falling back to the procedural glow.");

        return texture;
    }

    // ---------- asset writing ----------

    static void SaveSurface(string name, Texture2D texture, Vector2 tiling, float glossiness,
                            float metallic = 0f)
    {
        string texturePath = TextureFolder + "/Tex_" + name + ".asset";
        AssetDatabase.DeleteAsset(texturePath);
        AssetDatabase.CreateAsset(texture, texturePath);

        var material = new Material(Shader.Find("Standard"));
        material.mainTexture = texture;
        material.mainTextureScale = tiling;
        material.SetFloat("_Glossiness", glossiness);
        material.SetFloat("_Metallic", metallic);

        Save(material, "Mat_" + name);
    }

    static void SaveFlat(string name, Color color, float glossiness, float metallic = 0f)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Glossiness", glossiness);
        material.SetFloat("_Metallic", metallic);

        Save(material, name);
    }

    static void SaveAdditive(string name, Texture2D texture, Color tint)
    {
        SaveTexture(name, texture);

        var material = new Material(EffectShader());
        material.mainTexture = texture;
        material.color = tint;

        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 4f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);

        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;

        Save(material, name);
    }

    static void SaveTransparent(string name, Texture2D texture, Color tint)
    {
        SaveTexture(name, texture);

        var material = new Material(EffectShader());
        material.mainTexture = texture;
        material.color = tint;

        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);

        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;

        Save(material, name);
    }

    static Shader EffectShader()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");
        return shader != null ? shader : Shader.Find("Sprites/Default");
    }

    static void SaveTexture(string materialName, Texture2D texture)
    {
        string path = TextureFolder + "/Tex_" + materialName + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(texture, path);
    }

    static void Save(Material material, string name)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(material, path);
    }

    public static Material Load(string materialName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + materialName + ".mat");
        if (material == null)
            Debug.LogWarning("Drone Strike: material " + materialName + " not found. "
                             + "Run Tools > Drone Strike > 1 - Generate Materials first.");
        return material;
    }

    // ---------- procedural textures ----------

    static Texture2D RadialGlow(int size, Color color, int spikes)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Glow";
        texture.wrapMode = TextureWrapMode.Clamp;

        float centre = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - centre) / centre;
                float dy = (y - centre) / centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float falloff = Mathf.Clamp01(1f - distance);
                float alpha = falloff * falloff;

                if (spikes > 0)
                {
                    float angle = Mathf.Atan2(dy, dx);
                    float star = Mathf.Abs(Mathf.Cos(angle * spikes * 0.5f));
                    alpha = Mathf.Clamp01(alpha + falloff * star * 0.55f);
                }

                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    static Texture2D ScorchMark(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Scorch";
        texture.wrapMode = TextureWrapMode.Clamp;

        float centre = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - centre) / centre;
                float dy = (y - centre) / centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float angle = Mathf.Atan2(dy, dx);
                float wobble = 0.8f + Mathf.Sin(angle * 5f) * 0.08f + Mathf.Sin(angle * 11f) * 0.05f;

                float alpha = distance < wobble ? Mathf.Clamp01(1f - distance / wobble) : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }

        texture.Apply();
        return texture;
    }
}
