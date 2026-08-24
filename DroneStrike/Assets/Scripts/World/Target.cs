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

    /// <summary>True once this target has taken a hit it did not die to.</summary>
    public bool IsDamaged { get; private set; }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        health -= amount;

        if (health > 0f)
        {
            // A tank that survives a hit has to look like it survived a hit.
            // Without this the second run in looks identical to the first, and
            // a player who put a drone dead on target has no way to tell
            // whether it did anything at all — which reads as the hit not
            // registering rather than as armour doing its job.
            if (!IsDamaged) ApplyBattleDamage();
            return;
        }

        IsDestroyed = true;
        Explode();

        if (OnDestroyed != null) OnDestroyed(this, Points);
    }

    /// <summary>
    /// The wounded state: scorched paint, a smoke plume and a shunt off level.
    /// Deliberately smaller than <see cref="SpawnFire"/> — smoke without flame,
    /// so "hurt" and "dead" never get confused at a distance.
    /// </summary>
    void ApplyBattleDamage()
    {
        IsDamaged = true;

        if (GameEffects.Instance != null)
            GameEffects.Instance.HardImpact(transform.position + Vector3.up, Vector3.up);

        // Darkened through a property block rather than by swapping materials:
        // it keeps the model's own textures and leaves no material instances
        // behind, which swapping in a scorched material would do to every
        // renderer on every damaged target.
        var block = new MaterialPropertyBlock();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", new Color(0.52f, 0.48f, 0.44f));
            renderer.SetPropertyBlock(block);
        }

        var box = GetComponent<BoxCollider>();
        float radius = box != null
            ? Mathf.Max(0.6f, Mathf.Max(box.size.x, box.size.z) * 0.2f)
            : 1f;
        Vector3 centre = box != null ? box.center : Vector3.up;

        var holder = new GameObject("BattleDamage");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = centre;

        BuildSmoke(holder.transform, Resources.Load<Material>("Materials/Mat_Spark"), radius);

        // Knocked off level, but only slightly — the wreck tilt is far harder,
        // so the two states stay distinguishable.
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

        SpawnFire();
    }

    /// <summary>
    /// The wreck burns.
    ///
    /// A one-frame spark burst and a black repaint is not enough to read as
    /// "destroyed" from two hundred metres up — from that range the target only
    /// changes colour slightly. Fire is the one cue that carries at altitude,
    /// which is exactly the range the player judges their own progress from, so
    /// it keeps burning instead of playing once.
    ///
    /// Sized from the target's own collider, so a burning tank throws a bigger
    /// column than a burning crate stack without a per-kind table.
    /// </summary>
    void SpawnFire()
    {
        var box = GetComponent<BoxCollider>();
        float radius = box != null
            ? Mathf.Max(0.8f, Mathf.Max(box.size.x, box.size.z) * 0.3f)
            : 1.5f;
        Vector3 centre = box != null ? box.center : Vector3.up;

        var holder = new GameObject("Fire");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = centre;

        Material spark = Resources.Load<Material>("Materials/Mat_Spark");

        BuildFlames(holder.transform, spark, radius);
        BuildSmoke(holder.transform, spark, radius);

        // A light, so the fire throws colour onto the wreck and the ground
        // around it instead of sitting on top of the scene like a decal.
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

    static void BuildFlames(Transform parent, Material material, float radius)
    {
        var go = new GameObject("Flames");
        go.transform.SetParent(parent, false);

        var system = go.AddComponent<ParticleSystem>();

        var main = system.main;
        main.loop = true;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.2f, radius * 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.7f, radius * 1.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.72f, 0.25f), new Color(1f, 0.35f, 0.08f));
        main.gravityModifier = -0.22f;      // flame rises
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;

        var emission = system.emission;
        emission.rateOverTime = 26f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.75f;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f));

        var colourOverLifetime = system.colorOverLifetime;
        colourOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.32f, 0.06f), 0.55f),
                new GradientColorKey(new Color(0.25f, 0.12f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.70f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colourOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ApplyParticleMaterial(go, material);
    }

    static void BuildSmoke(Transform parent, Material material, float radius)
    {
        var go = new GameObject("Smoke");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * radius;

        var system = go.AddComponent<ParticleSystem>();

        var main = system.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 1.4f, radius * 3.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.14f, 0.13f, 0.12f, 0.75f), new Color(0.30f, 0.29f, 0.28f, 0.5f));
        main.gravityModifier = -0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var emission = system.emission;
        emission.rateOverTime = 11f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 16f;
        shape.radius = radius * 0.5f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // straight up

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.9f));

        var colourOverLifetime = system.colorOverLifetime;
        colourOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.65f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        colourOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ApplyParticleMaterial(go, material);

        // Smoke draws behind the flame: the additive fire material would wash
        // the column out completely if it rendered on top of it.
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) renderer.sortingOrder = -1;
    }

    static void ApplyParticleMaterial(GameObject go, Material material)
    {
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;

        if (material != null) renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
