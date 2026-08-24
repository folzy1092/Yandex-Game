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

    void Awake()
    {
        marker = GetComponent<Renderer>();
        seed = Random.value * 100f;
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
        Color colour = marker.material.color;
        colour.a = Mathf.Lerp(0.12f, 0.32f, breathe);
        marker.material.color = colour;
    }
}
