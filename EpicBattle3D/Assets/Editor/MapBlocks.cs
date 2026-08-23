using UnityEngine;

/// <summary>
/// Primitive building blocks shared by the map builders: boxes, ramps, steps and
/// the grouping helpers that keep the generated scene hierarchy readable.
///
/// Cover heights are named rather than typed as raw numbers, because how tall a
/// piece of cover is *is* its gameplay role:
///
///   Low    0.9 m — shoot over it standing, hide behind it crouched
///   Medium 1.4 m — breaks aim at range, still shootable over up close
///   High   2.6 m — cuts the sightline completely
/// </summary>
public static class MapBlocks
{
    public const float LowCover = 0.9f;
    public const float MediumCover = 1.4f;
    public const float HighCover = 2.6f;

    public static GameObject Group(string name, Transform parent = null)
    {
        var group = new GameObject(name);
        if (parent != null) group.transform.SetParent(parent, false);
        return group;
    }

    /// <summary>A box sitting on the floor, positioned by its footprint centre.</summary>
    public static GameObject Box(Transform parent, string name, Vector3 centreOnFloor,
                                 Vector3 size, Material material, float yaw = 0f)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        if (parent != null) box.transform.SetParent(parent, false);

        box.transform.position = centreOnFloor + Vector3.up * (size.y * 0.5f);
        box.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        box.transform.localScale = size;

        if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    /// <summary>A box positioned by its centre, for things not resting on the floor.</summary>
    public static GameObject BoxAt(Transform parent, string name, Vector3 centre,
                                   Vector3 size, Material material, Vector3 euler = default(Vector3))
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        if (parent != null) box.transform.SetParent(parent, false);

        box.transform.position = centre;
        box.transform.rotation = Quaternion.Euler(euler);
        box.transform.localScale = size;

        if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    public static GameObject Cylinder(Transform parent, string name, Vector3 centre,
                                      Vector3 size, Material material, Vector3 euler = default(Vector3))
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        if (parent != null) cylinder.transform.SetParent(parent, false);

        cylinder.transform.position = centre;
        cylinder.transform.rotation = Quaternion.Euler(euler);
        cylinder.transform.localScale = size;

        if (material != null) cylinder.GetComponent<Renderer>().sharedMaterial = material;
        return cylinder;
    }

    /// <summary>
    /// A walkable slope between two heights. Built from a rotated box rather than
    /// a wedge mesh, which is enough for a CharacterController to climb.
    /// </summary>
    public static GameObject Ramp(Transform parent, string name, Vector3 bottomCentre,
                                  Vector3 topCentre, float width, Material material)
    {
        Vector3 delta = topCentre - bottomCentre;
        float run = new Vector2(delta.x, delta.z).magnitude;
        float rise = delta.y;

        float length = Mathf.Sqrt(run * run + rise * rise) + 0.3f;
        float pitch = -Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = name;
        if (parent != null) ramp.transform.SetParent(parent, false);

        ramp.transform.position = (bottomCentre + topCentre) * 0.5f;
        ramp.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);
        ramp.transform.localScale = new Vector3(width, 0.3f, length);

        if (material != null) ramp.GetComponent<Renderer>().sharedMaterial = material;
        return ramp;
    }

    /// <summary>
    /// A flight of steps. Bots cannot jump, so anything they need to climb is
    /// built with a step height under the CharacterController's step offset.
    /// </summary>
    public static void Steps(Transform parent, string name, Vector3 bottomCentre, Vector3 direction,
                             int count, float stepRise, float stepRun, float width, Material material)
    {
        Vector3 forward = direction.normalized;
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        for (int i = 0; i < count; i++)
        {
            float height = stepRise * (i + 1);
            Vector3 position = bottomCentre + forward * (stepRun * (i + 0.5f));

            // Each step is a solid block down to the floor, so there is no gap
            // underneath for anything to fall into.
            BoxAt(parent, name + "_" + i,
                  new Vector3(position.x, bottomCentre.y + height * 0.5f, position.z),
                  new Vector3(width, height, stepRun), material,
                  new Vector3(0f, yaw, 0f));
        }
    }

    /// <summary>
    /// Removes the collider from purely decorative geometry, so small props never
    /// snag the player or block a bot's path.
    /// </summary>
    public static GameObject NoCollision(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        return go;
    }

    /// <summary>Shadows off for small clutter — it costs fill rate and adds nothing.</summary>
    public static GameObject NoShadows(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return go;
    }
}
