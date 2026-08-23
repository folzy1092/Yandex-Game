using UnityEngine;

/// <summary>
/// Generates the ground as a single procedural mesh.
///
/// One mesh rather than Unity's Terrain component: a Terrain asset has to be
/// painted and configured through the editor GUI, which cannot be done from
/// code, and it is heavier than this map needs. A grid displaced by layered
/// Perlin noise gives rolling ground for a fraction of the cost.
///
/// Layered noise is what stops it looking like a rippled bedsheet — a wide,
/// tall wave for the hills, a medium one for their shoulders, and a fine one
/// for surface roughness.
/// </summary>
public static class TerrainMesh
{
    /// <summary>
    /// Height of the ground at a world position. Kept public and separate from
    /// mesh building so props can be dropped onto the surface without raycasting.
    /// </summary>
    public static float SampleHeight(float x, float z, float amplitude, int seed)
    {
        float offset = seed * 0.137f;

        float hills = Mathf.PerlinNoise(offset + x * 0.006f, offset + z * 0.006f);
        float shoulders = Mathf.PerlinNoise(offset + x * 0.021f, offset + z * 0.021f) * 0.4f;
        float rough = Mathf.PerlinNoise(offset + x * 0.09f, offset + z * 0.09f) * 0.08f;

        return (hills + shoulders + rough - 0.6f) * amplitude;
    }

    /// <summary>
    /// Builds the ground mesh.
    /// </summary>
    /// <param name="size">Side length in metres. The map is square.</param>
    /// <param name="resolution">Vertices per side. 129 keeps it well under the 65k vertex limit.</param>
    /// <param name="amplitude">How tall the hills get.</param>
    /// <param name="flatRadius">
    /// Ground within this distance of the centre is flattened, so the launch area
    /// and the target compound sit on level ground instead of a hillside.
    /// </param>
    public static Mesh Build(float size, int resolution, float amplitude, int seed, float flatRadius)
    {
        resolution = Mathf.Clamp(resolution, 2, 250);

        var vertices = new Vector3[resolution * resolution];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[(resolution - 1) * (resolution - 1) * 6];

        float step = size / (resolution - 1);
        float half = size * 0.5f;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;

                float worldX = -half + x * step;
                float worldZ = -half + z * step;
                float height = SampleHeight(worldX, worldZ, amplitude, seed);

                // Ease the hills down to nothing across the flat zone, so there
                // is no visible seam where the flattening starts.
                float distance = new Vector2(worldX, worldZ).magnitude;
                if (distance < flatRadius * 2f)
                {
                    float blend = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(flatRadius, flatRadius * 2f, distance));
                    height *= blend;
                }

                vertices[index] = new Vector3(worldX, height, worldZ);
                uvs[index] = new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1));
            }
        }

        int triangle = 0;
        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int corner = z * resolution + x;

                triangles[triangle++] = corner;
                triangles[triangle++] = corner + resolution;
                triangles[triangle++] = corner + 1;

                triangles[triangle++] = corner + 1;
                triangles[triangle++] = corner + resolution;
                triangles[triangle++] = corner + resolution + 1;
            }
        }

        var mesh = new Mesh();
        mesh.name = "Terrain";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
