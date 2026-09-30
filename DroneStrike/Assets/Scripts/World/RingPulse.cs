using UnityEngine;

/// <summary>Drives the fuel danger ring (breathing) and the jammer's field wave (expanding).</summary>
public class RingPulse : MonoBehaviour
{
    public enum Mode { Breathe, Expand }
    public Mode mode;
    public float maxRadius = 30f;
    public float period = 2.4f;

    Renderer ring;
    Target owner;
    Color baseColour;

    void Start()
    {
        ring = GetComponent<Renderer>();
        owner = GetComponentInParent<Target>();
        if (ring != null && ring.sharedMaterial != null) baseColour = ring.sharedMaterial.color;
    }

    void Update()
    {
        if (ring == null || ring.sharedMaterial == null) return;
        if (owner != null && owner.IsDestroyed)
        {
            ring.enabled = false;
            enabled = false;
            return;
        }

        Color colour = baseColour;
        if (mode == Mode.Breathe)
        {
            colour.a = baseColour.a * (0.55f + 0.45f * Mathf.Sin(Time.time * 2.2f));
        }
        else
        {
            float t = Mathf.Repeat(Time.time / period, 1f);
            float radius = Mathf.Lerp(2f, maxRadius * 0.6f, t);
            transform.localScale = new Vector3(radius, 1f, radius);
            colour.a = baseColour.a * (1f - t);
        }
        ring.sharedMaterial.color = colour;
    }
}
