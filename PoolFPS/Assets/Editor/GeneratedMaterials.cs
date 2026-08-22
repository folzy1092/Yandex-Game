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

    [MenuItem("Tools/Pool FPS/1 - Generate Materials")]
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

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Pool FPS: textures and materials generated in Assets/Resources.");
    }

    static void SaveSurface(string name, Texture2D texture, Vector2 tiling, float glossiness)
    {
        string texturePath = TextureFolder + "/Tex_" + name + ".asset";
        AssetDatabase.DeleteAsset(texturePath);
        AssetDatabase.CreateAsset(texture, texturePath);

        var material = new Material(Shader.Find("Standard"));
        material.mainTexture = texture;
        material.mainTextureScale = tiling;
        material.SetFloat("_Glossiness", glossiness);
        material.SetFloat("_Metallic", 0f);

        string materialPath = MaterialFolder + "/Mat_" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
    }

    static void SaveFlatMaterial(string name, Color color, float glossiness)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Glossiness", glossiness);
        material.SetFloat("_Metallic", 0f);

        string path = MaterialFolder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(material, path);
    }

    public static Material Load(string materialName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + materialName + ".mat");
        if (material == null)
            Debug.LogWarning("Pool FPS: material " + materialName + " not found. Run Tools > Pool FPS > 1 - Generate Materials first.");
        return material;
    }
}
