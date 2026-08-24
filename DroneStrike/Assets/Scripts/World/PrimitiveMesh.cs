using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Meshes Unity does not ship as primitives.
///
/// Unity gives you a cube, a sphere, a capsule and a cylinder, and every one of
/// those has parallel sides or is round at both ends. Anything that tapers — a
/// warhead's nose, a propeller blade — has to be built, and faking it by
/// squashing a capsule is what makes ordnance read as something else entirely.
/// </summary>
public static class PrimitiveMesh
{
    /// <summary>
    /// A frustum: a cylinder whose two ends have different radii. A top radius
    /// of zero gives a true cone with a sharp point.
    ///
    /// Built around the origin along Y, the same axis Unity's own cylinder uses,
    /// so the two can be stacked without thinking about it.
    /// </summary>
    public static Mesh Frustum(float bottomRadius, float topRadius, float height, int segments = 16)
    {
        segments = Mathf.Max(3, segments);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        float half = height * 0.5f;
        bool pointed = topRadius <= 0.0001f;

        // Side wall. The normal leans by the taper angle, or a cone lights like
        // a cylinder and the point disappears into the body.
        float slope = Mathf.Atan2(bottomRadius - topRadius, height);
        float normalY = Mathf.Sin(slope);
        float normalXZ = Mathf.Cos(slope);

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            var normal = new Vector3(cos * normalXZ, normalY, sin * normalXZ).normalized;

            vertices.Add(new Vector3(cos * bottomRadius, -half, sin * bottomRadius));
            normals.Add(normal);

            vertices.Add(new Vector3(cos * topRadius, half, sin * topRadius));
            normals.Add(normal);
        }

        for (int i = 0; i < segments; i++)
        {
            int bottom = i * 2;
            int top = bottom + 1;
            int nextBottom = bottom + 2;
            int nextTop = bottom + 3;

            triangles.Add(bottom); triangles.Add(top); triangles.Add(nextTop);
            triangles.Add(bottom); triangles.Add(nextTop); triangles.Add(nextBottom);
        }

        AddCap(vertices, normals, triangles, bottomRadius, -half, Vector3.down, segments);
        if (!pointed) AddCap(vertices, normals, triangles, topRadius, half, Vector3.up, segments);

        var mesh = new Mesh();
        mesh.name = pointed ? "Cone" : "Frustum";
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
                       float radius, float y, Vector3 normal, int segments)
    {
        int centre = vertices.Count;
        vertices.Add(new Vector3(0f, y, 0f));
        normals.Add(normal);

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
            normals.Add(normal);
        }

        bool upward = normal.y > 0f;
        for (int i = 0; i < segments; i++)
        {
            int a = centre + 1 + i;
            int b = centre + 2 + i;

            if (upward) { triangles.Add(centre); triangles.Add(b); triangles.Add(a); }
            else { triangles.Add(centre); triangles.Add(a); triangles.Add(b); }
        }
    }

    /// <summary>
    /// A smooth solid of revolution through a profile of (radius, height) rings,
    /// ordered from the base upward. Where <see cref="Frustum"/> gives one
    /// straight taper between two radii, this gives a curved one through as many
    /// as the profile has — which is the difference between an ogive nose cone
    /// and a warhead that looks like it was built from a stack of lampshades.
    ///
    /// A radius of zero at either end closes to a point there instead of a flat
    /// cap, so the last ring of a nose profile is the tip itself.
    /// </summary>
    public static Mesh Revolve(Vector2[] profile, int segments = 20)
    {
        segments = Mathf.Max(3, segments);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        int rings = profile.Length;
        var ringStart = new int[rings];

        for (int r = 0; r < rings; r++)
        {
            ringStart[r] = vertices.Count;

            float radius = profile[r].x;
            float y = profile[r].y;

            // The normal follows the profile's own tangent (a central difference
            // against its neighbours), not the ring's own tiny local slope — that
            // is what makes a curved profile shade as a curve instead of a
            // sequence of flat, faceted steps.
            Vector2 previous = r > 0 ? profile[r - 1] : profile[r];
            Vector2 next = r < rings - 1 ? profile[r + 1] : profile[r];
            Vector2 tangent = (next - previous).normalized;

            var outward = new Vector2(tangent.y, -tangent.x);
            if (outward.x < 0f) outward = -outward;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(cos * radius, y, sin * radius));
                normals.Add(new Vector3(cos * outward.x, outward.y, sin * outward.x).normalized);
            }
        }

        for (int r = 0; r < rings - 1; r++)
        {
            for (int i = 0; i < segments; i++)
            {
                int a = ringStart[r] + i;
                int b = a + 1;
                int c = ringStart[r + 1] + i;
                int d = c + 1;

                triangles.Add(a); triangles.Add(c); triangles.Add(d);
                triangles.Add(a); triangles.Add(d); triangles.Add(b);
            }
        }

        if (profile[0].x > 0.0001f)
            AddCap(vertices, normals, triangles, profile[0].x, profile[0].y, Vector3.down, segments);

        if (profile[rings - 1].x > 0.0001f)
            AddCap(vertices, normals, triangles, profile[rings - 1].x, profile[rings - 1].y,
                  Vector3.up, segments);

        var mesh = new Mesh();
        mesh.name = "Revolve";
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// A sagging sheet: a grid that hangs low in the middle and high at its
    /// edges, the way netting strung between corner poles actually sits,
    /// rather than a rigid flat plane. A flat quad the size of a camouflage
    /// net reads as a solid painted rhombus floating over whatever it is
    /// meant to be draped across — nothing about a taut, perfectly planar
    /// surface says "fabric".
    ///
    /// Built double-sided (both triangle windings at every quad) so it still
    /// reads correctly seen from underneath, which a drone flying beneath one
    /// of these will do.
    /// </summary>
    public static Mesh Drape(float width, float depth, float sag, int resolution, int seed)
    {
        resolution = Mathf.Max(2, resolution);

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float u = (float)x / resolution;
                float v = (float)z / resolution;

                float px = (u - 0.5f) * width;
                float pz = (v - 0.5f) * depth;

                // 0 at the edge, 1 in the middle — the sheet is pinned at its
                // corners and sags furthest from them.
                float edgeFactor = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 2f;
                float bowl = edgeFactor * edgeFactor;

                float jitter = (Mathf.PerlinNoise(seed + x * 0.6f, seed + z * 0.6f) - 0.5f) * sag * 0.3f;
                float py = -bowl * sag + jitter;

                vertices.Add(new Vector3(px, py, pz));
                uvs.Add(new Vector2(u, v));
            }
        }

        int stride = resolution + 1;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int a = z * stride + x;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;

                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);

                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }
        }

        var mesh = new Mesh();
        mesh.name = "Drape";
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// A propeller blade: a thin aerofoil that tapers towards the tip and twists
    /// along its length.
    ///
    /// Built lying along +X from the hub, so a rotor is this mesh repeated at
    /// even angles about Y. A flat disc is what a spinning prop blurs into, but
    /// a stationary drone wearing four discs looks like it runs on wheels.
    /// </summary>
    public static Mesh Blade(float length, float rootChord, float tipChord,
                             float thickness, float twist)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        const int spans = 5;

        for (int i = 0; i <= spans; i++)
        {
            float t = (float)i / spans;
            float x = Mathf.Lerp(length * 0.12f, length, t);
            float chord = Mathf.Lerp(rootChord, tipChord, t);
            float halfThickness = Mathf.Lerp(thickness, thickness * 0.35f, t) * 0.5f;

            // Pitch washes out towards the tip, the way a real blade is twisted.
            float pitch = Mathf.Lerp(twist, twist * 0.35f, t) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(pitch);
            float sin = Mathf.Sin(pitch);

            // Four corners of the section, rotated about the blade's own axis.
            AddSectionCorner(vertices, x, -chord * 0.5f, halfThickness, cos, sin);
            AddSectionCorner(vertices, x, chord * 0.5f, halfThickness, cos, sin);
            AddSectionCorner(vertices, x, chord * 0.5f, -halfThickness, cos, sin);
            AddSectionCorner(vertices, x, -chord * 0.5f, -halfThickness, cos, sin);
        }

        for (int i = 0; i < spans; i++)
        {
            int a = i * 4;
            int b = a + 4;

            for (int side = 0; side < 4; side++)
            {
                int next = (side + 1) % 4;

                triangles.Add(a + side); triangles.Add(b + side); triangles.Add(b + next);
                triangles.Add(a + side); triangles.Add(b + next); triangles.Add(a + next);
            }
        }

        // Cap the tip so the blade is not an open tube.
        int last = spans * 4;
        triangles.Add(last); triangles.Add(last + 1); triangles.Add(last + 2);
        triangles.Add(last); triangles.Add(last + 2); triangles.Add(last + 3);

        var mesh = new Mesh();
        mesh.name = "Blade";
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddSectionCorner(List<Vector3> vertices, float x, float chord, float thickness,
                                 float cos, float sin)
    {
        vertices.Add(new Vector3(x,
                                 chord * sin + thickness * cos,
                                 chord * cos - thickness * sin));
    }
}
