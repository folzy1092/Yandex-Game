using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns the procedural textures into real project assets. They have to be saved
/// as assets rather than created on the fly, otherwise the material references
/// stored in the scene file would break the next time the scene is opened.
/// </summary>
public static class GeneratedMaterials
{
    public const string TextureFolder = "Assets/Resources/Textures";
    public const string MaterialFolder = "Assets/Resources/Materials";

    [MenuItem("Tools/Epic Battle 3D/1 - Generate Materials")]
    public static void Generate()
    {
        Directory.CreateDirectory(TextureFolder);
        Directory.CreateDirectory(MaterialFolder);

        // Floor: large pale tiles, the kind you get around a public pool.
        SaveSurface("Floor",
            ProceduralTextures.CreateTiles(512, 8, new Color(0.72f, 0.71f, 0.68f),
                                           new Color(0.42f, 0.41f, 0.39f), 0.05f, 101),
            new Vector2(8f, 5f), 0.15f);

        // Walls: plain painted concrete.
        SaveSurface("Wall",
            ProceduralTextures.CreateConcrete(512, new Color(0.60f, 0.62f, 0.66f), 0.35f, 202),
            new Vector2(8f, 1f), 0.1f);

        // Pool basin: small bright blue tiles.
        SaveSurface("PoolTile",
            ProceduralTextures.CreateTiles(512, 16, new Color(0.25f, 0.62f, 0.88f),
                                           new Color(0.85f, 0.88f, 0.90f), 0.08f, 303),
            new Vector2(4f, 3f), 0.55f);

        // Water: glossy, so the directional light gives it a highlight.
        SaveSurface("Water",
            ProceduralTextures.CreateWater(512, new Color(0.35f, 0.75f, 0.95f),
                                           new Color(0.08f, 0.35f, 0.62f), 404),
            new Vector2(2f, 2f), 0.9f);

        // Crates and cover.
        SaveSurface("Crate",
            ProceduralTextures.CreateConcrete(256, new Color(0.55f, 0.40f, 0.24f), 0.5f, 505),
            Vector2.one, 0.1f);

        // Plain coloured materials, tinted by whoever uses them.
        SaveFlatMaterial("Mat_Bot", new Color(0.8f, 0.3f, 0.3f), 0.2f);
        SaveFlatMaterial("Mat_Player", new Color(0.25f, 0.75f, 0.45f), 0.2f);
        SaveFlatMaterial("Mat_SpawnMarker", new Color(0.95f, 0.85f, 0.25f), 0.3f);

        // Weapons: dark gunmetal with a lighter accent for barrel and sights.
        SaveFlatMaterial("Mat_Gun", new Color(0.16f, 0.17f, 0.19f), 0.55f, 0.7f);
        SaveFlatMaterial("Mat_GunAccent", new Color(0.42f, 0.44f, 0.48f), 0.75f, 0.9f);

        BuildPoolComplexMaterials();

        SaveEffectMaterials();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Epic Battle 3D: textures and materials generated in Assets/Resources.");
    }

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

        string materialPath = MaterialFolder + "/Mat_" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
    }

    static void SaveFlatMaterial(string name, Color color, float glossiness, float metallic = 0f)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Glossiness", glossiness);
        material.SetFloat("_Metallic", metallic);

        string path = MaterialFolder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(material, path);
    }

    /// <summary>
    /// Surfaces for the pool complex itself: its furniture, plant room and decor.
    ///
    /// The palette is the faded seaside one the design calls for — turquoise,
    /// white, grey, yellow and washed-out orange — chosen so the players' saturated
    /// team colours read clearly against it rather than competing with it.
    /// </summary>
    static void BuildPoolComplexMaterials()
    {
        // Weathered poured concrete for kerbs, benches and the stands.
        SaveSurface("Concrete",
            ProceduralTextures.CreateConcrete(512, new Color(0.66f, 0.65f, 0.62f), 0.28f, 606),
            new Vector2(3f, 3f), 0.08f);

        // Brushed metal for lockers, pumps, pipework and railings.
        SaveSurface("Metal",
            ProceduralTextures.CreateConcrete(256, new Color(0.55f, 0.58f, 0.62f), 0.16f, 707),
            new Vector2(2f, 2f), 0.62f, 0.55f);

        // Wall tiling for the showers, finer than the basin's.
        SaveSurface("WallTile",
            ProceduralTextures.CreateTiles(512, 12, new Color(0.80f, 0.83f, 0.82f),
                                           new Color(0.55f, 0.57f, 0.56f), 0.05f, 808),
            new Vector2(4f, 2f), 0.35f);

        SaveFlatMaterial("Mat_Plastic", new Color(0.88f, 0.89f, 0.87f), 0.45f);       // loungers
        SaveFlatMaterial("Mat_Fabric", new Color(0.90f, 0.52f, 0.22f), 0.12f);        // parasols, canopy
        SaveFlatMaterial("Mat_Wood", new Color(0.52f, 0.38f, 0.24f), 0.18f);          // benches, tower
        SaveFlatMaterial("Mat_Plant", new Color(0.24f, 0.46f, 0.22f), 0.14f);         // foliage
        SaveFlatMaterial("Mat_Accent", new Color(0.16f, 0.62f, 0.62f), 0.35f);        // turquoise fittings
        SaveFlatMaterial("Mat_LaneMarking", new Color(0.09f, 0.20f, 0.34f), 0.5f);    // pool lane stripes

        // Clerestory glazing. Emissive so the windows read as the daylight
        // source in a hall that is otherwise closed in by its roof.
        SaveEmissiveMaterial("Mat_Window", new Color(0.78f, 0.88f, 0.95f),
                             new Color(0.55f, 0.68f, 0.78f));
    }

    static void SaveEmissiveMaterial(string name, Color color, Color emission)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Glossiness", 0.6f);
        material.SetFloat("_Metallic", 0f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        string path = MaterialFolder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(material, path);
    }

    /// <summary>
    /// Materials for muzzle flashes, tracers, sparks and bullet holes.
    ///
    /// These use unlit shaders on purpose: an effect that reacts to scene lighting
    /// looks dull, and a muzzle flash needs to read as its own light source. The
    /// flash, tracer and spark materials are additive, so they brighten whatever
    /// is behind them; the bullet hole is a normal cutout that darkens the wall.
    /// </summary>
    static void SaveEffectMaterials()
    {
        Texture2D flashTexture = RadialGlow(128, new Color(1f, 0.95f, 0.7f), 6);
        SaveAdditiveMaterial("Mat_Muzzle", flashTexture, new Color(1f, 0.88f, 0.55f));

        Texture2D softTexture = RadialGlow(64, Color.white, 0);
        SaveAdditiveMaterial("Mat_Tracer", softTexture, new Color(1f, 0.92f, 0.6f));
        SaveAdditiveMaterial("Mat_Spark", softTexture, new Color(1f, 0.82f, 0.35f));

        // Blood is not additive — additive red on a dark wall would glow pink.
        SaveTransparentMaterial("Mat_Blood", softTexture, new Color(0.65f, 0.05f, 0.05f));

        Texture2D holeTexture = BulletHole(64);
        SaveTransparentMaterial("Mat_BulletHole", holeTexture, new Color(0.05f, 0.05f, 0.06f));
    }

    static void SaveAdditiveMaterial(string name, Texture2D texture, Color tint)
    {
        SaveTexture(name, texture);

        // Particles/Standard Unlit ships with Unity and supports additive blending
        // through its blend-mode keyword, so no custom shader is needed.
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var material = new Material(shader);
        material.mainTexture = texture;
        material.color = tint;

        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 4f);       // additive
        if (material.HasProperty("_BlendOp")) material.SetFloat("_BlendOp", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);

        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;

        SaveMaterialAsset(name, material);
    }

    static void SaveTransparentMaterial(string name, Texture2D texture, Color tint)
    {
        SaveTexture(name, texture);

        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var material = new Material(shader);
        material.mainTexture = texture;
        material.color = tint;

        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);       // fade
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);

        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;

        SaveMaterialAsset(name, material);
    }

    static void SaveTexture(string materialName, Texture2D texture)
    {
        string path = TextureFolder + "/Tex_" + materialName + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(texture, path);
    }

    static void SaveMaterialAsset(string name, Material material)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(material, path);
    }

    /// <summary>
    /// A soft circular glow, optionally with spikes radiating out of it so a
    /// muzzle flash looks like a starburst rather than a plain dot.
    /// </summary>
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

    /// <summary>A dark ragged dot with a soft rim, standing in for a bullet hole.</summary>
    static Texture2D BulletHole(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "BulletHole";
        texture.wrapMode = TextureWrapMode.Clamp;

        Random.State previous = Random.state;
        Random.InitState(909);

        float centre = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - centre) / centre;
                float dy = (y - centre) / centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // Wobble the edge so holes do not all look like identical circles.
                float angle = Mathf.Atan2(dy, dx);
                float wobble = 0.82f + Mathf.Sin(angle * 5f) * 0.06f + Mathf.Sin(angle * 11f) * 0.04f;

                float alpha = distance < wobble
                    ? Mathf.Clamp01(1f - distance / wobble)
                    : 0f;

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }

        Random.state = previous;
        texture.Apply();
        return texture;
    }

    public static Material Load(string materialName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + materialName + ".mat");
        if (material == null)
            Debug.LogWarning("Epic Battle 3D: material " + materialName + " not found. Run Tools > Epic Battle 3D > 1 - Generate Materials first.");
        return material;
    }
}
