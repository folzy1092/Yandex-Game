using UnityEngine;

/// <summary>
/// Builds a small pistol out of boxes. Used both for the weapon held in front of
/// the player's camera and for the one in each bot's hand, so they always match.
///
/// Side view, roughly 0.22 m long:
///
///        +==========+   slide
///        |  +----+
///        +--+  |
///           |  |         grip
///           +--+
/// </summary>
public static class PistolModel
{
    /// <returns>The muzzle transform — where flashes spawn and shots originate.</returns>
    public static Transform Build(Transform parent, Material bodyMaterial, Material accentMaterial,
                                  float scale)
    {
        var root = new GameObject("Pistol");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * scale;

        AddBox(root.transform, "Slide", bodyMaterial,
               new Vector3(0f, 0.035f, 0.02f), new Vector3(0.038f, 0.052f, 0.20f));

        AddBox(root.transform, "Frame", bodyMaterial,
               new Vector3(0f, 0f, 0f), new Vector3(0.034f, 0.030f, 0.16f));

        // Grip, raked back the way a pistol grip is.
        var grip = AddBox(root.transform, "Grip", bodyMaterial,
                          new Vector3(0f, -0.075f, -0.045f), new Vector3(0.032f, 0.125f, 0.050f));
        grip.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

        AddBox(root.transform, "TriggerGuard", bodyMaterial,
               new Vector3(0f, -0.032f, -0.005f), new Vector3(0.020f, 0.030f, 0.014f));

        AddBox(root.transform, "Sight", accentMaterial,
               new Vector3(0f, 0.065f, 0.10f), new Vector3(0.008f, 0.010f, 0.010f));

        var barrel = AddCylinder(root.transform, "Barrel", accentMaterial,
                                 new Vector3(0f, 0.035f, 0.115f), new Vector3(0.020f, 0.018f, 0.020f));
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.035f, 0.14f);

        return muzzle.transform;
    }

    static GameObject AddBox(Transform parent, string name, Material material,
                             Vector3 localPosition, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;

        Strip(box, material);
        return box;
    }

    static GameObject AddCylinder(Transform parent, string name, Material material,
                                  Vector3 localPosition, Vector3 size)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localScale = size;

        Strip(cylinder, material);
        return cylinder;
    }

    /// <summary>
    /// Primitives arrive with colliders attached; a weapon model must not have
    /// any, or it would block the very shots it fires.
    /// </summary>
    static void Strip(GameObject go, Material material)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }

        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
    }
}
