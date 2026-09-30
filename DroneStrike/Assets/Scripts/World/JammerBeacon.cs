using UnityEngine;

/// <summary>Blinks the jammer's red beacon and its light while the station stands.</summary>
public class JammerBeacon : MonoBehaviour
{
    public Renderer beacon;
    public Light beaconLight;
    Target target;
    Material material;
    static readonly int ColourId = Shader.PropertyToID("_Color");

    void Start()
    {
        target = GetComponent<Target>();
        Material source = Resources.Load<Material>("Materials/Mat_TargetOutline");
        if (source != null && beacon != null)
        {
            material = new Material(source) { name = "Beacon" };
            material.SetFloat("_Width", 0f);
            material.SetFloat("_DepthPush", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            beacon.sharedMaterial = material;
        }
    }

    void Update()
    {
        bool live = target == null || !target.IsDestroyed;
        bool on = live && Mathf.Repeat(Time.time, 0.9f) < 0.35f;
        if (material != null)
            material.SetColor(ColourId, on ? new Color(1f, 0.12f, 0.06f) : new Color(0.25f, 0.03f, 0.02f));
        if (beaconLight != null) beaconLight.intensity = on ? 3.5f : 0f;
        if (!live) enabled = false;
    }

    void OnDestroy() { if (material != null) Destroy(material); }
}
