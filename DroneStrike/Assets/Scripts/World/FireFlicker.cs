using UnityEngine;

/// <summary>
/// Wobbles a fire's light. A point light held at a constant intensity reads as
/// a lamp; the flicker is what makes it read as burning.
/// </summary>
public class FireFlicker : MonoBehaviour
{
    public float baseIntensity = 2.4f;

    Light pointLight;
    float seed;

    void Awake()
    {
        pointLight = GetComponent<Light>();
        if (pointLight != null) baseIntensity = pointLight.intensity;
        seed = UnityEngine.Random.value * 100f;
    }

    void Update()
    {
        if (pointLight == null) return;

        float noise = Mathf.PerlinNoise(seed, Time.time * 5.5f);
        pointLight.intensity = baseIntensity * Mathf.Lerp(0.55f, 1.25f, noise);
    }
}
