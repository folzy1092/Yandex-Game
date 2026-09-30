using UnityEngine;

/// <summary>
/// A short health bar floating over a target that survived a hit.
///
/// A damaged-but-alive vehicle used to be repainted orange, which read as a
/// texture bug rather than "hit it again". The vehicle now keeps its own
/// colours (sooted and scorched), and this bar says the rest: it is still a
/// target, and how much is left. It faces the drone camera, keeps roughly the
/// same size on screen at any range, and disappears once the target dies.
/// </summary>
public class TargetHealthBar : MonoBehaviour
{
    Target target;
    TargetOutline outline;
    Transform holder;
    Transform fill;
    Material fillMaterial;
    Material backMaterial;
    float top;

    static readonly int ColourId = Shader.PropertyToID("_Color");

    public static TargetHealthBar Attach(Target target)
    {
        var bar = target.GetComponent<TargetHealthBar>();
        if (bar == null) bar = target.gameObject.AddComponent<TargetHealthBar>();
        return bar;
    }

    void Awake() { Init(); }

    // Lazy as well as in Awake: components added in edit mode (the review
    // capture) never get Awake.
    void Init()
    {
        if (target != null) return;
        target = GetComponent<Target>();
        outline = GetComponent<TargetOutline>();
        var box = GetComponent<BoxCollider>();
        top = box != null ? box.center.y + box.size.y * 0.5f : 3f;

        Material source = Resources.Load<Material>("Materials/Mat_TargetOutline");
        if (source == null) return;
        backMaterial = Solid(source, new Color(0.05f, 0.05f, 0.05f));
        fillMaterial = Solid(source, new Color(1f, 0.55f, 0.1f));

        holder = new GameObject("HealthBarMarker").transform;
        holder.SetParent(transform, false);
        Quad(holder, "HealthBarMarkerBack", backMaterial, 0f);
        fill = Quad(holder, "HealthBarMarkerFill", fillMaterial, -0.02f);
    }

    static Material Solid(Material source, Color colour)
    {
        var material = new Material(source);
        material.SetFloat("_Width", 0f);
        material.SetFloat("_DepthPush", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.SetColor(ColourId, colour);
        return material;
    }

    static Transform Quad(Transform parent, string name, Material material, float z)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null) FieldProps.Discard(collider);
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(0f, 0f, z);
        var renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return quad.transform;
    }

    void LateUpdate()
    {
        MissionManager mission = MissionManager.Instance;
        Camera view = mission != null && mission.ActiveDrone != null ? mission.ActiveDrone.View : Camera.main;
        Refresh(view);
    }

    /// <summary>Positions and fills the bar for <paramref name="view"/>. Public for the review capture.</summary>
    public void Refresh(Camera view)
    {
        Init();
        if (holder == null) return;
        bool visible = target != null && target.IsDamaged && !target.IsDestroyed && view != null
                       && (outline == null || outline.Visible || !Application.isPlaying);
        if (holder.gameObject.activeSelf != visible) holder.gameObject.SetActive(visible);
        if (!visible) return;

        Vector3 anchor = transform.position + Vector3.up * (top + 0.8f);
        float distance = Vector3.Distance(view.transform.position, anchor);
        float width = Mathf.Clamp(distance * 0.07f, 1.8f, 14f);
        float height = width * 0.14f;

        holder.position = anchor + Vector3.up * height;
        holder.rotation = view.transform.rotation;
        holder.localScale = Vector3.one;
        holder.GetChild(0).localScale = new Vector3(width, height, 1f);

        float health = Mathf.Clamp01(target.Health / target.MaxHealth);
        float inner = width * 0.94f;
        fill.localScale = new Vector3(Mathf.Max(0.001f, inner * health), height * 0.7f, 1f);
        fill.localPosition = new Vector3(-inner * 0.5f + inner * health * 0.5f, 0f, -0.02f * width);
        fillMaterial.SetColor(ColourId, Color.Lerp(new Color(0.95f, 0.15f, 0.1f), new Color(1f, 0.62f, 0.1f), health));
    }

    void OnDestroy()
    {
        if (fillMaterial != null) Destroy(fillMaterial);
        if (backMaterial != null) Destroy(backMaterial);
    }
}
