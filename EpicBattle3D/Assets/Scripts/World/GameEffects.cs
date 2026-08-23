using UnityEngine;

/// <summary>
/// Muzzle flashes, bullet tracers, impact sparks and bullet holes.
///
/// Everything is pooled and reused. Spawning and destroying objects on every
/// shot would produce garbage collection stutter, which is far more noticeable
/// in a WebGL build than in the editor.
/// </summary>
public class GameEffects : MonoBehaviour
{
    public static GameEffects Instance { get; private set; }

    const int TracerCount = 16;
    const int FlashCount = 8;
    const int DecalCount = 48;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("GameEffects");
        Instance = go.AddComponent<GameEffects>();
        DontDestroyOnLoad(go);
    }

    class TracerLine
    {
        public LineRenderer line;
        public float remaining;
        public float duration;
    }

    class Flash
    {
        public Transform root;
        public Renderer quad;
        public Light glow;
        public float remaining;
        public float duration;
        public float baseIntensity;
    }

    readonly TracerLine[] tracers = new TracerLine[TracerCount];
    readonly Flash[] flashes = new Flash[FlashCount];
    readonly Transform[] decals = new Transform[DecalCount];

    ParticleSystem sparks;
    ParticleSystem blood;

    int nextTracer;
    int nextFlash;
    int nextDecal;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BuildTracers();
        BuildFlashes();
        BuildDecals();
        BuildParticles();
    }

    void Update()
    {
        for (int i = 0; i < tracers.Length; i++)
        {
            TracerLine tracer = tracers[i];
            if (tracer.remaining <= 0f) continue;

            tracer.remaining -= Time.deltaTime;
            float fade = Mathf.Clamp01(tracer.remaining / tracer.duration);

            if (fade <= 0f)
            {
                tracer.line.enabled = false;
                continue;
            }

            Color color = tracer.line.startColor;
            color.a = fade;
            tracer.line.startColor = color;
            tracer.line.endColor = new Color(color.r, color.g, color.b, fade * 0.25f);
        }

        for (int i = 0; i < flashes.Length; i++)
        {
            Flash flash = flashes[i];
            if (flash.remaining <= 0f) continue;

            flash.remaining -= Time.deltaTime;
            float fade = Mathf.Clamp01(flash.remaining / flash.duration);

            if (fade <= 0f)
            {
                flash.root.gameObject.SetActive(false);
                continue;
            }

            flash.glow.intensity = flash.baseIntensity * fade;
            flash.root.localScale = Vector3.one * (0.18f + 0.22f * fade);
        }
    }

    // ---------- public API ----------

    /// <summary>Bright flare at the muzzle. Parented so it follows a moving gun.</summary>
    public void MuzzleFlash(Vector3 position, Vector3 forward, Transform attachTo)
    {
        Flash flash = flashes[nextFlash];
        nextFlash = (nextFlash + 1) % FlashCount;

        flash.root.SetParent(attachTo, false);
        flash.root.position = position;
        flash.root.rotation = Quaternion.LookRotation(forward);
        flash.root.localScale = Vector3.one * 0.4f;

        // A little spin so repeated shots do not look like the same frozen image.
        flash.quad.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        flash.duration = 0.055f;
        flash.remaining = flash.duration;
        flash.glow.intensity = flash.baseIntensity;
        flash.root.gameObject.SetActive(true);
    }

    /// <summary>Streak marking the bullet's path.</summary>
    public void Tracer(Vector3 from, Vector3 to, Color color)
    {
        TracerLine tracer = tracers[nextTracer];
        nextTracer = (nextTracer + 1) % TracerCount;

        tracer.line.SetPosition(0, from);
        tracer.line.SetPosition(1, to);
        tracer.line.startColor = color;
        tracer.line.endColor = new Color(color.r, color.g, color.b, 0.25f);
        tracer.line.enabled = true;

        tracer.duration = 0.05f;
        tracer.remaining = tracer.duration;
    }

    /// <summary>Sparks and a bullet hole where a shot met a wall.</summary>
    public void HardImpact(Vector3 point, Vector3 normal)
    {
        EmitBurst(sparks, point, normal, 10);
        PlaceDecal(point, normal);
    }

    /// <summary>Blood puff where a shot met a person. No decal — bodies move.</summary>
    public void FleshImpact(Vector3 point, Vector3 normal)
    {
        EmitBurst(blood, point, normal, 14);
    }

    // ---------- construction ----------

    void BuildTracers()
    {
        Material material = LoadMaterial("Mat_Tracer");

        for (int i = 0; i < TracerCount; i++)
        {
            var go = new GameObject("Tracer" + i);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.035f;
            line.endWidth = 0.01f;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (material != null) line.sharedMaterial = material;
            line.enabled = false;

            tracers[i] = new TracerLine { line = line };
        }
    }

    void BuildFlashes()
    {
        Material material = LoadMaterial("Mat_Muzzle");

        for (int i = 0; i < FlashCount; i++)
        {
            var root = new GameObject("Flash" + i);
            root.transform.SetParent(transform, false);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Quad";
            quad.transform.SetParent(root.transform, false);
            Destroy(quad.GetComponent<Collider>());

            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;

            var lightGO = new GameObject("Glow");
            lightGO.transform.SetParent(root.transform, false);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.5f);
            light.range = 7f;
            light.intensity = 3.2f;
            light.shadows = LightShadows.None;

            root.SetActive(false);

            flashes[i] = new Flash
            {
                root = root.transform,
                quad = renderer,
                glow = light,
                baseIntensity = 3.2f
            };
        }
    }

    void BuildDecals()
    {
        Material material = LoadMaterial("Mat_BulletHole");

        for (int i = 0; i < DecalCount; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BulletHole" + i;
            quad.transform.SetParent(transform, false);
            Destroy(quad.GetComponent<Collider>());

            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;

            quad.SetActive(false);
            decals[i] = quad.transform;
        }
    }

    void BuildParticles()
    {
        sparks = CreateParticleSystem("Sparks", LoadMaterial("Mat_Spark"),
                                      new Color(1f, 0.82f, 0.35f), 0.05f, 0.45f, 6f);

        blood = CreateParticleSystem("Blood", LoadMaterial("Mat_Blood"),
                                     new Color(0.75f, 0.08f, 0.08f), 0.07f, 0.4f, 3.5f);
    }

    ParticleSystem CreateParticleSystem(string name, Material material, Color color,
                                        float size, float lifetime, float speed)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var system = go.AddComponent<ParticleSystem>();

        // Configure before the system ever plays, otherwise Unity warns about
        // modifying a running system.
        var main = system.main;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.gravityModifier = 1.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 400;

        var emission = system.emission;
        emission.enabled = false;   // bursts only, via Emit()

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.02f;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (material != null) renderer.sharedMaterial = material;

        return system;
    }

    // ---------- helpers ----------

    void EmitBurst(ParticleSystem system, Vector3 point, Vector3 normal, int count)
    {
        if (system == null) return;

        // Aim the cone back along the surface normal, so debris flies out of the wall.
        system.transform.position = point;
        system.transform.rotation = Quaternion.LookRotation(normal);
        system.Emit(count);
    }

    void PlaceDecal(Vector3 point, Vector3 normal)
    {
        Transform decal = decals[nextDecal];
        nextDecal = (nextDecal + 1) % DecalCount;

        // Lifted slightly off the surface so it does not fight the wall for depth.
        decal.position = point + normal * 0.012f;
        decal.rotation = Quaternion.LookRotation(-normal, Vector3.up);
        decal.Rotate(0f, 0f, Random.Range(0f, 360f), Space.Self);
        decal.localScale = Vector3.one * Random.Range(0.11f, 0.16f);
        decal.gameObject.SetActive(true);
    }

    static Material LoadMaterial(string name)
    {
        return Resources.Load<Material>("Materials/" + name);
    }
}
