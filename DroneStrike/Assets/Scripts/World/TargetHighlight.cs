using UnityEngine;

/// <summary>
/// Breathes the faint glow ring under a target. The silhouette outline
/// (<see cref="TargetOutline"/>) is now the primary cue; the ring stays as a
/// soft footing under it, tinted the same colour and shown only while the
/// outline is — so an optional or shielded object never glows like the one
/// the player is meant to hit, and a wreck stops glowing at all.
/// </summary>
public class TargetHighlight : MonoBehaviour
{
    public Target target;

    Renderer marker;
    TargetOutline outline;
    float seed;
    MaterialPropertyBlock properties;
    Color baseColour;
    static readonly int ColourId = Shader.PropertyToID("_Color");

    void Awake()
    {
        marker = GetComponent<Renderer>();
        seed = Random.value * 100f;
        properties = new MaterialPropertyBlock();
        baseColour = marker != null && marker.sharedMaterial != null ? marker.sharedMaterial.color : Color.white;
    }

    void Update()
    {
        if (marker == null) return;
        if (outline == null && target != null) outline = target.GetComponent<TargetOutline>();

        bool visible = target == null || !target.IsDestroyed;
        if (outline != null) visible &= outline.Visible;
        if (marker.enabled != visible) marker.enabled = visible;
        if (!visible) return;

        float breathe = Mathf.PerlinNoise(seed, Time.time * 0.35f);
        Color colour = outline != null && outline.Visible ? outline.CurrentColour : baseColour;
        colour.a = Mathf.Lerp(0.10f, 0.24f, breathe);
        marker.GetPropertyBlock(properties);
        properties.SetColor(ColourId, colour);
        marker.SetPropertyBlock(properties);
    }
}
