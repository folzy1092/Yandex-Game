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

    class Blast
    {
        public Transform root;
        public ParticleSystem fire, smoke, debris;
        public Light light;
        public float remaining;
    }
    readonly Blast[] blasts = new Blast[4];
    int nextBlast;

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
        BuildExplosions();
    }

    void Update()
    {
        foreach (var blast in blasts)
        {
            if (blast.remaining <= 0f) continue;
            blast.remaining = Mathf.Max(0f, blast.remaining - Time.deltaTime);
            blast.light.intensity = 5f * (blast.remaining / 0.3f);
            blast.light.enabled = blast.remaining > 0f;
        }
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

    /// <summary>
    /// Bright flare at the muzzle. Placed at a world position rather than parented
    /// to the gun: GameEffects survives scene reloads (DontDestroyOnLoad), and
    /// parenting a pooled flash onto a gun transform would move it into that
    /// gameplay scene — the next reload then destroys it along with the scene,
    /// and the pool starts handing out MissingReferenceExceptions. A flash only
    /// lives 55 ms, so it does not need to track a moving gun anyway.
    /// </summary>
    public void MuzzleFlash(Vector3 position, Vector3 forward)
    {
        Flash flash = flashes[nextFlash];
        nextFlash = (nextFlash + 1) % FlashCount;

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

    /// <summary>Bounded, reusable fireball, rising dust and ballistic sparks.</summary>
    public void Explosion(Vector3 position, float blastRadius)
    {
        Blast blast = blasts[nextBlast];
        nextBlast = (nextBlast + 1) % blasts.Length;
        blast.fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        blast.smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        blast.debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        blast.root.position = position;
        float scale = Mathf.Clamp(blastRadius * 0.35f, 1.2f, 3f);
        var fire = blast.fire.main;
        fire.startSize = new ParticleSystem.MinMaxCurve(scale * 0.7f, scale * 1.3f);
        fire.startSpeed = new ParticleSystem.MinMaxCurve(scale, scale * 2.5f);
        var smoke = blast.smoke.main;
        smoke.startSize = new ParticleSystem.MinMaxCurve(scale * 0.5f, scale);
        smoke.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.4f, scale);
        blast.fire.Emit(12);
        blast.smoke.Emit(16);
        blast.debris.Emit(28);
        blast.light.range = scale * 7f;
        blast.light.intensity = 5f;
        blast.light.enabled = true;
        blast.remaining = 0.3f;
    }

    void BuildExplosions()
    {
        for (int i = 0; i < blasts.Length; i++)
        {
            var root = new GameObject("Explosion" + i);
            root.transform.SetParent(transform, false);
            var blast = new Blast { root = root.transform };
            blast.fire = BlastParticles(root.transform, "Fireball", "Mat_FireReal", Color.white,
                0.55f, 2f, 5f, 0f, false);
            blast.smoke = BlastParticles(root.transform, "Dust", "Mat_SmokeReal",
                new Color(0.9f, 0.85f, 0.75f, 0.65f), 2.2f, 1.5f, 2f, -0.06f, true);
            blast.debris = BlastParticles(root.transform, "Debris", "Mat_Spark",
                new Color(1f, 0.8f, 0.35f), 0.8f, 0.09f, 11f, 1.1f, false);
            blast.light = root.AddComponent<Light>();
            blast.light.type = LightType.Point;
            blast.light.color = new Color(1f, 0.55f, 0.18f);
            blast.light.shadows = LightShadows.None;
            blast.light.enabled = false;
            blasts[i] = blast;
        }
    }

    ParticleSystem BlastParticles(Transform parent, string name, string materialName, Color colour,
                                   float lifetime, float size, float speed, float gravity, bool expand)
    {
        var system = CreateParticleSystem(name, LoadMaterial(materialName), colour, size, lifetime, speed);
        system.transform.SetParent(parent, false);
        var main = system.main;
        main.loop = false;
        main.maxParticles = 64;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;
        var sizes = system.sizeOverLifetime;
        sizes.size = new ParticleSystem.MinMaxCurve(1f, expand
            ? AnimationCurve.Linear(0f, 0.6f, 1f, 2.8f)
            : AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0f, 1f) });
        var colours = system.colorOverLifetime;
        colours.enabled = true;
        colours.color = new ParticleSystem.MinMaxGradient(gradient);
        return system;
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
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

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
