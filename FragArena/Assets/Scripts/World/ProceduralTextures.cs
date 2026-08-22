using UnityEngine;

/// <summary>
/// Generates the game's textures in code. No imported image files, which keeps
/// the WebGL download small — download size directly affects how many players
/// stay long enough to see an ad.
/// </summary>
public static class ProceduralTextures
{
    /// <summary>Rough surface such as concrete: a base colour broken up by layered noise.</summary>
    public static Texture2D CreateConcrete(int size, Color baseColor, float roughness, int seed)
    {
        var texture = NewTexture(size, "Concrete");
        Random.State previousState = Random.state;
        Random.InitState(seed);

        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float coarse = Mathf.PerlinNoise(offsetX + x * 0.04f, offsetY + y * 0.04f);
                float fine = Mathf.PerlinNoise(offsetX + x * 0.25f, offsetY + y * 0.25f);
                float grain = Random.value;

                float shade = 1f + (coarse * 0.6f + fine * 0.3f + grain * 0.1f - 0.5f) * roughness;
                texture.SetPixel(x, y, baseColor * shade);
            }
        }

        Random.state = previousState;
        texture.Apply();
        return texture;
    }

    /// <summary>Tiled surface: square tiles separated by grout lines, each tile slightly shaded.</summary>
    public static Texture2D CreateTiles(int size, int tilesPerSide, Color tileColor, Color groutColor,
                                        float variation, int seed)
    {
        var texture = NewTexture(size, "Tiles");
        Random.State previousState = Random.state;
        Random.InitState(seed);

        int tileSize = Mathf.Max(2, size / tilesPerSide);
        int groutWidth = Mathf.Max(1, tileSize / 16);

        // Pre-roll a shade per tile so every pixel of one tile shares the same tone.
        var shades = new float[tilesPerSide + 1, tilesPerSide + 1];
        for (int ty = 0; ty <= tilesPerSide; ty++)
            for (int tx = 0; tx <= tilesPerSide; tx++)
                shades[tx, ty] = 1f + Random.Range(-variation, variation);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int withinX = x % tileSize;
                int withinY = y % tileSize;
                bool isGrout = withinX < groutWidth || withinY < groutWidth;

                if (isGrout)
                {
                    texture.SetPixel(x, y, groutColor);
                    continue;
                }

                int tileX = Mathf.Min(x / tileSize, tilesPerSide);
                int tileY = Mathf.Min(y / tileSize, tilesPerSide);
                texture.SetPixel(x, y, tileColor * shades[tileX, tileY]);
            }
        }

        Random.state = previousState;
        texture.Apply();
        return texture;
    }

    /// <summary>Water surface: two blues blended by soft noise to suggest depth and ripples.</summary>
    public static Texture2D CreateWater(int size, Color shallow, Color deep, int seed)
    {
        var texture = NewTexture(size, "Water");
        Random.State previousState = Random.state;
        Random.InitState(seed);

        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float wave = Mathf.PerlinNoise(offsetX + x * 0.06f, offsetY + y * 0.06f);
                float ripple = Mathf.PerlinNoise(offsetX + x * 0.18f, offsetY + y * 0.18f) * 0.35f;
                float blend = Mathf.Clamp01(wave + ripple - 0.2f);

                texture.SetPixel(x, y, Color.Lerp(deep, shallow, blend));
            }
        }

        Random.state = previousState;
        texture.Apply();
        return texture;
    }

    static Texture2D NewTexture(int size, string name)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }
}
