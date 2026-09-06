using UnityEngine;

/// <summary>
/// Breathes the faint glow ring under a target and turns it off once the
/// target is destroyed — a wreck does not need finding, it is already found.
/// </summary>
public class TargetHighlight : MonoBehaviour
{
    public Target target;

    Renderer marker;
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

        if (target != null && target.IsDestroyed)
        {
            if (marker.enabled) marker.enabled = false;
            return;
        }

        float breathe = Mathf.PerlinNoise(seed, Time.time * 0.35f);
        Color colour = baseColour;
        colour.a = Mathf.Lerp(0.12f, 0.32f, breathe);
        marker.GetPropertyBlock(properties);
        properties.SetColor(ColourId, colour);
        marker.SetPropertyBlock(properties);
    }
}
