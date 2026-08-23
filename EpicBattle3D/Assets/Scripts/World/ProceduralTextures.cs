using UnityEngine;

/// <summary>
/// Generates the game's textures in code. No imported image files, which keeps
/// the WebGL download small — download size directly affects how many players
/// stay long enough to see an ad.
/// </summary>
public static class ProceduralTextures
{
    /// <summary>
    /// Rough surface such as concrete: layered noise for the grain, plus damp
    /// staining and hairline cracks so a large wall does not read as flat fuzz.
    ///
    /// The cracks come from ridged noise — take Perlin noise, fold it around its
    /// midpoint and keep only the sharp valley — which is the standard cheap way
    /// to get a branching line out of a smooth noise field.
    /// </summary>
    public static Texture2D CreateConcrete(int size, Color baseColor, float roughness, int seed,
                                           float weathering = 0.4f)
    {
        var texture = NewTexture(size, "Concrete");
        Random.State previousState = Random.state;
        Random.InitState(seed);

        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);
        float crackOffsetX = Random.Range(0f, 1000f);
        float crackOffsetY = Random.Range(0f, 1000f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float coarse = Mathf.PerlinNoise(offsetX + x * 0.04f, offsetY + y * 0.04f);
                float fine = Mathf.PerlinNoise(offsetX + x * 0.25f, offsetY + y * 0.25f);
                float grain = Random.value;

                float shade = 1f + (coarse * 0.6f + fine * 0.3f + grain * 0.1f - 0.5f) * roughness;

                // Damp patches: broad, soft, and darker than the surrounding wall.
                float damp = Mathf.PerlinNoise(offsetX + x * 0.008f, offsetY + y * 0.008f);
                shade *= 1f - Mathf.Clamp01((damp - 0.55f) * 2.2f) * weathering * 0.35f;

                // Ridged noise, thresholded down to a thin dark line.
                float ridge = 1f - Mathf.Abs(
                    Mathf.PerlinNoise(crackOffsetX + x * 0.02f, crackOffsetY + y * 0.02f) - 0.5f) * 2f;
                if (ridge > 0.985f)
                    shade *= 1f - weathering * 0.55f;

                Color colour = baseColor * shade;
                colour.a = 1f;
                texture.SetPixel(x, y, colour);
            }
        }

        Random.state = previousState;
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Tiled surface: square tiles separated by grout lines.
    ///
    /// Three things stop it reading as a flat checkerboard, which is what a naive
    /// tile texture always looks like:
    ///  - each tile gets its own shade, so the grid has tonal variety;
    ///  - grimy staining pools along the grout, the way it does on real wet tile;
    ///  - a few tiles are chipped or discoloured, so the eye finds irregularity.
    /// </summary>
    public static Texture2D CreateTiles(int size, int tilesPerSide, Color tileColor, Color groutColor,
                                        float variation, int seed, float wear = 0.35f)
    {
        var texture = NewTexture(size, "Tiles");
        Random.State previousState = Random.state;
        Random.InitState(seed);

        int tileSize = Mathf.Max(2, size / tilesPerSide);
        int groutWidth = Mathf.Max(1, tileSize / 14);

        // Pre-roll per-tile values so every pixel of one tile agrees with itself.
        var shades = new float[tilesPerSide + 1, tilesPerSide + 1];
        var damaged = new bool[tilesPerSide + 1, tilesPerSide + 1];

        for (int ty = 0; ty <= tilesPerSide; ty++)
        {
            for (int tx = 0; tx <= tilesPerSide; tx++)
            {
                shades[tx, ty] = 1f + Random.Range(-variation, variation);
                damaged[tx, ty] = Random.value < wear * 0.25f;
            }
        }

        float grimeOffsetX = Random.Range(0f, 500f);
        float grimeOffsetY = Random.Range(0f, 500f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int withinX = x % tileSize;
                int withinY = y % tileSize;

                int tileX = Mathf.Min(x / tileSize, tilesPerSide);
                int tileY = Mathf.Min(y / tileSize, tilesPerSide);

                // Large-scale grime, independent of the tile grid.
                float grime = Mathf.PerlinNoise(grimeOffsetX + x * 0.012f, grimeOffsetY + y * 0.012f);
                float grimeStrength = Mathf.Clamp01((grime - 0.45f) * 2f) * wear;

                if (withinX < groutWidth || withinY < groutWidth)
                {
                    // Grout collects dirt, so it darkens faster than the tile face.
                    Color grout = groutColor * (1f - grimeStrength * 0.45f);
                    grout.a = 1f;
                    texture.SetPixel(x, y, grout);
                    continue;
                }

                Color colour = tileColor * shades[tileX, tileY];

                if (damaged[tileX, tileY])
                    colour *= 0.82f;

                // Edges of a tile wear faster than the middle.
                float edgeDistance = Mathf.Min(
                    Mathf.Min(withinX, tileSize - withinX),
                    Mathf.Min(withinY, tileSize - withinY)) / (float)tileSize;
                float edgeWear = Mathf.Clamp01(1f - edgeDistance * 6f) * wear * 0.25f;

                colour *= 1f - grimeStrength * 0.3f - edgeWear;
                colour.a = 1f;

                texture.SetPixel(x, y, colour);
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
