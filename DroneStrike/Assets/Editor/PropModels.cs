using UnityEngine;

/// <summary>
/// Places small downloaded props (crates, pallets) by their measured
/// geometry rather than by the file's pivot: each model is scaled uniformly
/// to a real-world size and shifted so its footprint is centred on the
/// holder and its lowest point sits exactly on it. Stacking then uses the
/// measured top of whatever is underneath, so nothing floats or sinks
/// whatever units or origin the source file used.
///
/// Falls back to a plain cube in the given material when a model is missing,
/// so a project without the GLBs still builds complete maps.
/// </summary>
public static class PropModels
{
    /// <summary>
    /// A holder at <paramref name="localFoot"/> (bottom-centre) under
    /// <paramref name="parent"/>, containing the model scaled so its longest
    /// horizontal side is <paramref name="longest"/> metres.
    /// </summary>
    public static GameObject Place(string model, string holderName, Transform parent, Vector3 localFoot,
                                   float yaw, float longest, Material fallback, bool collider)
    {
        var holder = new GameObject(holderName);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = localFoot;
        holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        GameObject instance = ModelLibrary.Instantiate(model, holder.transform);
        if (instance == null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = model + "Fallback";
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(holder.transform, false);
            cube.transform.localScale = Vector3.one * longest;
            cube.transform.localPosition = Vector3.up * longest * 0.5f;
            if (fallback != null) cube.GetComponent<Renderer>().sharedMaterial = fallback;
            instance = cube;
        }
        else
        {
            Bounds raw = LocalBounds(holder.transform);
            float horizontal = Mathf.Max(raw.size.x, raw.size.z);
            if (horizontal > 0.0001f) instance.transform.localScale *= longest / horizontal;
            Bounds scaled = LocalBounds(holder.transform);
            instance.transform.localPosition -= new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
        }

        if (collider)
        {
            Bounds final = LocalBounds(holder.transform);
            var box = holder.AddComponent<BoxCollider>();
            box.center = final.center;
            box.size = final.size;
        }
        return holder;
    }

    /// <summary>Height of the top of a placed holder, in its parent's local space.</summary>
    public static float TopOf(GameObject holder)
    {
        Bounds bounds = LocalBounds(holder.transform);
        return holder.transform.localPosition.y + bounds.max.y;
    }

    /// <summary>Union of the renderer bounds under <paramref name="root"/>, in root space (root squared up while measured).</summary>
    public static Bounds LocalBounds(Transform root)
    {
        Vector3 position = root.position;
        Quaternion rotation = root.rotation;
        Vector3 scale = root.localScale;
        Transform parent = root.parent;
        int sibling = root.GetSiblingIndex();
        root.SetParent(null, false);
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.localScale = Vector3.one;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        root.SetParent(parent, false);
        root.SetSiblingIndex(sibling);
        root.SetPositionAndRotation(position, rotation);
        root.localScale = scale;
        return bounds;
    }
}
