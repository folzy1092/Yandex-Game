using UnityEngine;

/// <summary>
/// Makes the view read as the drone's own camera, not a game camera floating
/// in the scene: a wide-angle lens (barrel distortion, edge colour fringing,
/// vignette, analog grain — see FpvLens.shader). The camera motion half of
/// the effect (motor vibration, banking with the airframe) lives in
/// DroneCameraGimbal.
///
/// Under jamming the fringing and grain climb with the field intensity, so
/// the lens itself looks strained, on top of the snow the HUD draws.
/// Can be switched off in the pause menu (<see cref="Enabled"/>).
/// </summary>
[RequireComponent(typeof(Camera))]
public class FpvCameraFx : MonoBehaviour
{
    const string PrefKey = "camera_lens_fx";

    public static bool Enabled
    {
        get { return PlayerPrefs.GetInt(PrefKey, 1) == 1; }
        set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    Material material;
    static readonly int ChromaId = Shader.PropertyToID("_Chroma");
    static readonly int GrainId = Shader.PropertyToID("_Grain");

    void Awake()
    {
        Material source = Resources.Load<Material>("Materials/Mat_FpvLens");
        if (source != null) material = new Material(source);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null || !Enabled)
        {
            Graphics.Blit(source, destination);
            return;
        }

        float jam = SignalJammer.Active != null ? SignalJammer.Active.Intensity(transform.position) : 0f;
        material.SetFloat(ChromaId, Mathf.Lerp(0.004f, 0.02f, jam));
        material.SetFloat(GrainId, Mathf.Lerp(0.05f, 0.16f, jam));
        Graphics.Blit(source, destination, material);
    }

    void OnDestroy() { if (material != null) Destroy(material); }
}
