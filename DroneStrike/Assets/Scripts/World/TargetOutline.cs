using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A thin neon line around the silhouette of a live mission target.
///
/// The glowing ring on the ground was the only cue a target had, and on the
/// sunny map it washed out against bright grass — the player saw a faint disc
/// next to a vehicle rather than the vehicle itself being marked. This traces
/// the object: every visible part gets a duplicate renderer with the outline
/// shader (a pixel-width shell drawn behind the part), so the outline follows
/// the truck, the tank or the fuel cache exactly, moves with a patrol, and
/// stays the same few pixels thick at any distance.
///
/// Only objects the player is actually asked to hit are traced. In a Mission1
/// challenge that is the required set; on the full-clearance maps it is every
/// target. Scenery is never outlined.
///
/// Colour carries state:
///   cyan    — active target
///   amber   — the jammer station (its beacon and outline pulse faster)
///   grey    — shielded by a live jammer, not yet a valid target
///   magenta — the weak-rear strip on armour (a marker, not an outline)
///
/// Built lazily on the first LateUpdate, not in Awake: the challenge runner
/// configures the scene in MissionManager.Start and may move or add parts,
/// and Start order between objects is undefined.
/// </summary>
[DisallowMultipleComponent]
public class TargetOutline : MonoBehaviour
{
    public static readonly Color ActiveColour = new Color(0.30f, 0.92f, 1.00f);
    public static readonly Color JammerColour = new Color(1.00f, 0.52f, 0.10f);
    public static readonly Color ShieldedColour = new Color(0.55f, 0.60f, 0.66f);
    public static readonly Color WeakSpotColour = new Color(1.00f, 0.28f, 0.78f);

    const float BaseWidthPixels = 2.6f;

    Target target;
    Material outlineMaterial;   // radial extrusion (primitive parts)
    Material normalMaterial;    // normal extrusion (imported models)
    Material accentMaterial;
    readonly List<Renderer> outlines = new List<Renderer>();
    readonly List<Renderer> accents = new List<Renderer>();
    bool built;
    bool shown;
    float seed;

    static readonly int ColourId = Shader.PropertyToID("_Color");
    static readonly int WidthId = Shader.PropertyToID("_Width");
    static readonly int RadialId = Shader.PropertyToID("_Radial");
    static readonly int DepthPushId = Shader.PropertyToID("_DepthPush");
    static readonly int CullId = Shader.PropertyToID("_Cull");

    /// <summary>True while the outline is drawn. The ground ring follows this.</summary>
    public bool Visible { get { return built && shown; } }
    public Color CurrentColour { get; private set; }

    /// <summary>
    /// Parts that are overlays rather than the object itself — the outline
    /// must neither trace them nor be repainted by damage along with them.
    /// </summary>
    public static bool IsOverlay(Renderer renderer)
    {
        if (renderer == null) return true;
        if (renderer.GetComponent<TargetHighlight>() != null) return true;
        string name = renderer.gameObject.name;
        return name.StartsWith("Outline") || name.Contains("Marker") || name == "WeakRear"
            || name.Contains("DangerRing") || name.Contains("JamWave") || name.Contains("Beacon");
    }

    static bool IsSheet(Renderer renderer)
    {
        string name = renderer.gameObject.name;
        if (name.Contains("Net") || name.Contains("Drape")) return true;
        Material material = renderer.sharedMaterial;
        return material != null && material.IsKeywordEnabled("_ALPHATEST_ON");
    }

    void Awake()
    {
        target = GetComponent<Target>();
        seed = Random.value * 10f;
    }

    void LateUpdate() { Refresh(); }

    /// <summary>Builds on first call, then updates visibility and colour. Public for the editor review capture.</summary>
    public void Refresh()
    {
        if (target == null) target = GetComponent<Target>();
        if (!built) Build();
        if (outlineMaterial == null) return;

        bool wanted = ShouldShow();
        if (wanted != shown)
        {
            shown = wanted;
            foreach (Renderer renderer in outlines)
                if (renderer != null) renderer.enabled = wanted;
            foreach (Renderer renderer in accents)
                if (renderer != null) renderer.enabled = wanted;
        }
        if (!shown) return;

        Color colour = StateColour();
        float rate = target.kind == Target.Kind.SignalJammer ? 3.2f : 1.4f;
        float pulse = 0.78f + 0.22f * Mathf.Sin((Time.time + seed) * rate);
        CurrentColour = colour;
        float width = BaseWidthPixels * Mathf.Max(1f, Screen.height / 1080f);
        foreach (Material material in new[] { outlineMaterial, normalMaterial })
        {
            material.SetColor(ColourId, colour * pulse);
            material.SetFloat(WidthId, width);
        }

        if (accentMaterial != null)
        {
            float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin((Time.time + seed) * 2.6f));
            accentMaterial.SetColor(ColourId, WeakSpotColour * blink);
        }
    }

    bool ShouldShow()
    {
        if (target == null || target.IsDestroyed || !target.isActiveAndEnabled) return false;
        MissionManager mission = MissionManager.Instance;
        if (mission != null && mission.HasChallenge) return target.IsPriority;
        return true;
    }

    Color StateColour()
    {
        if (target.kind == Target.Kind.SignalJammer) return JammerColour;
        if (target.ProtectedByJammer) return ShieldedColour;
        return ActiveColour;
    }

    void Build()
    {
        built = true;
        Material source = Resources.Load<Material>("Materials/Mat_TargetOutline");
        if (source == null) return;

        outlineMaterial = new Material(source) { name = "TargetOutline (" + name + ")" };
        outlineMaterial.SetFloat(CullId, (float)UnityEngine.Rendering.CullMode.Front);
        outlineMaterial.SetFloat(DepthPushId, 0.7f);

        normalMaterial = new Material(outlineMaterial) { name = outlineMaterial.name + " normals" };
        outlineMaterial.SetFloat(RadialId, 1f);
        normalMaterial.SetFloat(RadialId, 0f);

        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer == null || filter.sharedMesh == null || IsOverlay(renderer)) continue;
            if (!(renderer is MeshRenderer)) continue;
            // Thin double-sided sheets (the anti-drone net) have no inside
            // for a shell to hide behind: the whole sheet filled solid cyan.
            // The net is not the target anyway — its posts and cargo are.
            if (IsSheet(renderer)) continue;

            Mesh mesh = filter.sharedMesh;
            Bounds bounds = mesh.bounds;
            // Primitive cubes and cylinders are centred on their own origin:
            // radial extrusion keeps their shells closed at the corners.
            bool radial = bounds.center.magnitude < 0.05f * Mathf.Max(0.001f, bounds.extents.magnitude);

            var shell = new GameObject("Outline");
            shell.transform.SetParent(filter.transform, false);
            shell.layer = filter.gameObject.layer;
            shell.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shellRenderer = shell.AddComponent<MeshRenderer>();
            shellRenderer.sharedMaterial = radial ? outlineMaterial : normalMaterial;
            shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shellRenderer.receiveShadows = false;
            shellRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            shellRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            shellRenderer.enabled = false;
            outlines.Add(shellRenderer);
        }

        // The weak-rear strip is a solid marker in its own accent colour, not
        // an outline: it has to read as "hit here", distinct from "this one".
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "WeakRear") continue;
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer == null) continue;
            if (accentMaterial == null)
            {
                accentMaterial = new Material(source) { name = "WeakSpotMarker" };
                accentMaterial.SetFloat(WidthId, 0f);
                accentMaterial.SetFloat(DepthPushId, 0f);
                accentMaterial.SetFloat(CullId, (float)UnityEngine.Rendering.CullMode.Back);
                accentMaterial.SetColor(ColourId, WeakSpotColour);
            }
            renderer.sharedMaterial = accentMaterial;
            renderer.enabled = false;
            accents.Add(renderer);
        }
        shown = false;
    }

    void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
        if (normalMaterial != null) Destroy(normalMaterial);
        if (accentMaterial != null) Destroy(accentMaterial);
    }
}
