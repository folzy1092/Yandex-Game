using System;
using UnityEngine;

/// <summary>
/// Something the drone is sent to destroy.
///
/// Targets differ in how much punishment they take and what they are worth,
/// which is what makes choosing an approach matter: a supply crate dies to a
/// glancing hit, an armoured vehicle needs a solid one straight into it.
/// </summary>
public class Target : MonoBehaviour
{
    public enum Kind
    {
        LightVehicle,
        ArmouredVehicle,
        SupplyDepot,
        Antenna
    }

    public Kind kind = Kind.LightVehicle;

    /// <summary>Fired when this target is destroyed, with the points it was worth.</summary>
    public event Action<Target, int> OnDestroyed;

    public bool IsDestroyed { get; private set; }

    /// <summary>True once this target has taken a hit it did not die to.</summary>
    public bool IsDamaged { get; private set; }

    public int Points
    {
        get
        {
            switch (kind)
            {
                case Kind.ArmouredVehicle: return 300;
                case Kind.SupplyDepot: return 250;
                case Kind.Antenna: return 150;
                default: return 100;
            }
        }
    }

    float health;

    /// <summary>Damage needed to destroy this target from full.</summary>
    public float MaxHealth
    {
        get
        {
            switch (kind)
            {
                case Kind.ArmouredVehicle: return 140f;
                case Kind.SupplyDepot: return 60f;
                case Kind.Antenna: return 45f;
                default: return 80f;
            }
        }
    }

    void Awake()
    {
        health = MaxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        health -= amount;

        if (health > 0f)
        {
            // Survives, but has to look like it survived something. Without this
            // the second run in looks identical to the first, and a hit dead on
            // target reads as having missed rather than as armour holding.
            if (!IsDamaged) ApplyBattleDamage();
            return;
        }

        IsDestroyed = true;
        Explode();

        if (OnDestroyed != null) OnDestroyed(this, Points);
    }

    // ---------- damaged, not dead ----------

    /// <summary>
    /// Repaints every material slot to the scorched look, the same reliable
    /// technique <see cref="Explode"/> uses for the wrecked state — a
    /// MaterialPropertyBlock tint was tried here first and silently did nothing
    /// on some shaders, because the colour property a PropertyBlock overwrites
    /// is not the same name on every shader a downloaded model can arrive with.
    /// Swapping the whole material asset does not depend on knowing that name.
    /// </summary>
    void ApplyBattleDamage()
    {
        IsDamaged = true;

        if (GameEffects.Instance != null)
            GameEffects.Instance.HardImpact(transform.position + Vector3.up, Vector3.up);

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayHardImpact(transform.position);

        Material damaged = Resources.Load<Material>("Materials/Mat_Damaged");
        if (damaged != null)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                int slotCount = renderer.sharedMaterials.Length;
                var slots = new Material[slotCount];
                for (int i = 0; i < slotCount; i++) slots[i] = damaged;
                renderer.sharedMaterials = slots;
            }
        }

        // A light plume, not a fire — smoke without flame is what keeps
        // "wounded" from being confused with "dead" at a glance.
        SpawnSmokePlume(0.6f);

        transform.rotation *= Quaternion.Euler(
            UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
    }

    /// <summary>
    /// Turns the target into wreckage in place rather than deleting it. A
    /// destroyed target has to stay on the map, or the player loses track of
    /// which ones they have already hit.
    /// </summary>
    void Explode()
    {
        if (GameEffects.Instance != null)
            GameEffects.Instance.HardImpact(transform.position + Vector3.up, Vector3.up);

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayHardImpact(transform.position);

        Material burnt = Resources.Load<Material>("Materials/Mat_Burnt");
        if (burnt != null)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                // A downloaded model typically has several material slots (hull,
                // tracks, glass...). Setting sharedMaterial alone only replaces
                // slot 0, leaving the rest showing their original texture — every
                // slot has to be overwritten for the whole thing to read as burnt.
                int slotCount = renderer.sharedMaterials.Length;
                var burntSlots = new Material[slotCount];
                for (int i = 0; i < slotCount; i++) burntSlots[i] = burnt;
                renderer.sharedMaterials = burntSlots;
            }
        }

        // Settle and lean the wreck so it reads as destroyed at a glance.
        transform.position += Vector3.down * 0.25f;
        transform.rotation *= Quaternion.Euler(
            UnityEngine.Random.Range(-8f, 8f), 0f, UnityEngine.Random.Range(-8f, 8f));

        SpawnGroundScorch();
        SpawnFire();
    }

    // ---------- fire ----------

    float FootprintRadius
    {
        get
        {
            var box = GetComponent<BoxCollider>();
            return box != null ? Mathf.Max(0.8f, Mathf.Max(box.size.x, box.size.z) * 0.3f) : 1.5f;
        }
    }

    Vector3 FootprintCentre
    {
        get
        {
            var box = GetComponent<BoxCollider>();
            return box != null ? box.center : Vector3.up;
        }
    }

    /// <summary>
    /// A scorch mark burned into the ground under the wreck. Cheap, and it is
    /// the one cue that still reads after the flame has burned out — the ground
    /// itself stays marked as long as the wreck does.
    /// </summary>
    void SpawnGroundScorch()
    {
        Material scorch = Resources.Load<Material>("Materials/Mat_ScorchGround");
        if (scorch == null) return;

        float radius = FootprintRadius;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "GroundScorch";
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = FootprintCentre + Vector3.down * (FootprintCentre.y - 0.02f);
        quad.transform.localRotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
        quad.transform.localScale = Vector3.one * radius * 2.6f;

        Object.Destroy(quad.GetComponent<Collider>());
        var renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = scorch;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// <summary>
    /// The wreck burns.
    ///
    /// A one-frame spark burst and a black repaint is not enough to read as
    /// "destroyed" from two hundred metres up. Fire is the one cue that carries
    /// at altitude, which is exactly the range the player judges their own
    /// progress from, so it keeps burning instead of playing once.
    /// </summary>
    void SpawnFire()
    {
        float radius = FootprintRadius;

        var holder = new GameObject("Fire");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = FootprintCentre;

        BuildFlames(holder.transform, radius);
        SpawnSmokePlume(radius / 0.8f, holder.transform);

        var lightGO = new GameObject("FireLight");
        lightGO.transform.SetParent(holder.transform, false);
        lightGO.transform.localPosition = Vector3.up * radius;

        var fireLight = lightGO.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.55f, 0.18f);
        fireLight.range = radius * 9f;
        fireLight.intensity = 2.4f;

        lightGO.AddComponent<FireFlicker>();
    }

    static void BuildFlames(Transform parent, float radius)
    {
        var go = new GameObject("Flames");
        go.transform.SetParent(parent, false);

        var system = go.AddComponent<ParticleSystem>();

        var main = system.main;
        main.loop = true;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.9f, radius * 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 1.1f, radius * 2.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.72f, 0.25f), new Color(1f, 0.35f, 0.08f));
        main.gravityModifier = -0.20f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var emission = system.emission;
        emission.rateOverTime = 16f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.6f;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 0.85f));

        var colourOverLifetime = system.colorOverLifetime;
        colourOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.32f, 0.06f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colourOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var rotationOverLifetime = system.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-40f, 40f);

        ApplyParticleMaterial(go, "Mat_FireReal", "Mat_Spark");
    }

    /// <summary>
    /// A rising smoke column, shared between the persistent wreck fire and the
    /// lighter one-shot plume a survived hit gets.
    /// </summary>
    void SpawnSmokePlume(float radius, Transform parent = null)
    {
        var go = new GameObject("Smoke");
        go.transform.SetParent(parent != null ? parent : transform, false);
        if (parent == null) go.transform.localPosition = FootprintCentre + Vector3.up * radius;
        else go.transform.localPosition = Vector3.up * radius;

        var system = go.AddComponent<ParticleSystem>();

        var main = system.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 4.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.7f, radius * 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 1.6f, radius * 3.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.16f, 0.15f, 0.14f, 0.8f), new Color(0.32f, 0.30f, 0.29f, 0.55f));
        main.gravityModifier = -0.10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var emission = system.emission;
        emission.rateOverTime = 6f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = radius * 0.4f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));

        var rotationOverLifetime = system.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-15f, 15f);

        var colourOverLifetime = system.colorOverLifetime;
        colourOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.6f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        colourOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ApplyParticleMaterial(go, "Mat_SmokeReal", "Mat_Spark");

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) renderer.sortingOrder = -1;
    }

    /// <summary>
    /// Applies the named material if it exists, falling back to a second name
    /// rather than leaving the renderer with Unity's default. The default
    /// particle material is fully opaque, so a missing material does not read
    /// as "a bit wrong" — it reads as a solid coloured square standing in for
    /// smoke, which is worse than any fallback.
    /// </summary>
    static void ApplyParticleMaterial(GameObject go, string preferred, string fallback)
    {
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;

        Material material = Resources.Load<Material>("Materials/" + preferred);
        if (material == null) material = Resources.Load<Material>("Materials/" + fallback);

        if (material != null) renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
