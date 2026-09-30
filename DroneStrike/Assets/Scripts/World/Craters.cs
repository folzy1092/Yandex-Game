using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blast craters left in the ground: a low earth rim thrown up round a
/// scorched hollow, laid along the terrain's own slope.
///
/// A detonation close to the ground — flying into the field, or a hit low on
/// a tank's tracks — leaves one, so the ground keeps a record of every run and
/// a near miss is visibly a near miss. An airburst (the lost-link
/// self-destruct high up) leaves nothing, and water leaves nothing.
///
/// Kept to a fixed number: the oldest is recycled once the cap is reached.
/// </summary>
public static class Craters
{
    const int MaxCraters = 24;
    static readonly Queue<GameObject> live = new Queue<GameObject>();
    static Mesh rimMesh;

    /// <summary>
    /// Leaves a crater under <paramref name="origin"/> if the ground is within
    /// reach of the blast, sized by the blast and by how close it went off.
    /// </summary>
    public static void TrySpawn(Vector3 origin, float blastRadius)
    {
        GameObject terrain = GameObject.Find("Terrain");
        Collider ground = terrain != null ? terrain.GetComponent<Collider>() : null;
        if (ground == null) return;

        float reach = Mathf.Max(1.5f, blastRadius * 0.6f);
        RaycastHit hit;
        if (!ground.Raycast(new Ray(origin + Vector3.up * 0.5f, Vector3.down), out hit, reach + 0.5f))
            return;

        float closeness = 1f - Mathf.Clamp01((hit.distance - 0.5f) / reach);
        float radius = Mathf.Lerp(0.45f, 1f, closeness) * Mathf.Clamp(blastRadius * 0.32f, 0.9f, 2.4f);
        Spawn(hit.point, hit.normal, radius);
    }

    public static GameObject Spawn(Vector3 point, Vector3 normal, float radius)
    {
        if (rimMesh == null) rimMesh = BuildRim();

        while (live.Count >= MaxCraters)
        {
            GameObject old = live.Dequeue();
            if (old != null) Object.Destroy(old);
        }

        var crater = new GameObject("Crater");
        crater.transform.position = point;
        crater.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal)
                                    * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        crater.transform.localScale = new Vector3(radius, radius * Random.Range(0.8f, 1f), radius);

        var rim = new GameObject("CraterRim");
        rim.transform.SetParent(crater.transform, false);
        rim.AddComponent<MeshFilter>().sharedMesh = rimMesh;
        var rimRenderer = rim.AddComponent<MeshRenderer>();
        rimRenderer.sharedMaterial = Resources.Load<Material>("Materials/Mat_Dirt");
        rimRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Material soot = Resources.Load<Material>("Materials/Mat_Scorch");
        if (soot == null) soot = Resources.Load<Material>("Materials/Mat_BulletHole");
        if (soot != null)
        {
            // The hollow, and a wider fan of soot thrown out over the grass.
            Decal(crater.transform, "CraterScorch", soot, 0.08f, 2.1f);
            Material fan = Resources.Load<Material>("Materials/Mat_ScorchFan");
            Decal(crater.transform, "CraterFan", fan != null ? fan : soot, 0.02f, 4.2f);
        }

        live.Enqueue(crater);
        return crater;
    }

    static void Decal(Transform parent, string name, Material material, float lift, float size)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null) FieldProps.Discard(collider);
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = Vector3.up * lift;
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(size, size, 1f);
        var renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// Unit-radius rim of revolution: up from just under the ground to the lip
    /// at r = 1, then down into the hollow, closing below the surface so the
    /// terrain itself hides the bottom.
    /// </summary>
    static Mesh BuildRim()
    {
        var profile = new[]
        {
            new Vector2(1.6f, -0.04f),
            new Vector2(1.3f, 0.04f),
            new Vector2(1.05f, 0.1f),
            new Vector2(0.85f, 0.07f),
            new Vector2(0.6f, -0.04f),
            new Vector2(0f, -0.2f)
        };
        Mesh mesh = PrimitiveMesh.Revolve(profile, 18);
        mesh.name = "CraterRim";
        return mesh;
    }
}
