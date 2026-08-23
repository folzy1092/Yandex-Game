using UnityEngine;

/// <summary>
/// Loads the downloaded 3D models (glTF/GLB, imported via the com.unity.cloud.gltfast
/// package) and drops them into the scene.
///
/// Kept under Resources rather than referenced directly, because this same code
/// runs both from Editor scripts (placing static props while the mission scene is
/// built) and at runtime (the drone is rebuilt from scratch on every launch) —
/// Resources.Load works in both, a direct asset reference only works from Editor
/// code.
/// </summary>
public static class ModelLibrary
{
    /// <summary>
    /// Instantiates a model as a child of <paramref name="parent"/>.
    ///
    /// The scale, rotation and pivot of a downloaded model are unknown until
    /// someone has actually looked at it in the editor — these came from a
    /// public model site sight-unseen. <paramref name="scale"/> and
    /// <paramref name="yawOffset"/> exist so a model that comes in facing the
    /// wrong way or sized wrong can be corrected in one place rather than
    /// hunting through the mesh data.
    /// </summary>
    public static GameObject Instantiate(string modelName, Transform parent,
                                         float scale = 1f, float yawOffset = 0f)
    {
        GameObject prefab = Resources.Load<GameObject>("Models/" + modelName);
        if (prefab == null)
        {
            Debug.LogWarning("ModelLibrary: \"" + modelName + "\" not found under "
                             + "Assets/Resources/Models. Falling back to primitives.");
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, parent);
        instance.name = modelName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
        instance.transform.localScale = Vector3.one * scale;

        // Imported models rarely bring a collider that matches gameplay needs
        // (some bring none, some bring a dozen sub-meshes' worth); the caller's
        // own collider on the parent is what hit detection actually uses.
        //
        // This runs both while the mission scene is being built (Editor, not
        // playing — Destroy() is not legal there) and at runtime on every drone
        // launch, hence the branch: DestroyImmediate outside Play mode, Destroy
        // inside it.
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
        {
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return instance;
    }
}
