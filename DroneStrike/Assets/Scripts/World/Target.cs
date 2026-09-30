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
        SignalJammer,
        FuelDepot
    }

    public Kind kind = Kind.LightVehicle;

    /// <summary>Fired when this target is destroyed, with the points it was worth.</summary>
    public event Action<Target, int> OnDestroyed;

    public bool IsDestroyed { get; private set; }

    /// <summary>True once this target has taken a hit it did not die to.</summary>
    public bool IsDamaged { get; private set; }
    public bool WeakRear { get; set; }
    public bool ProtectedByJammer { get; set; }
    public bool IsPriority { get; set; }
    public bool DestroyedViaWeakSpot { get; private set; }
    public float Health { get { return Mathf.Max(0f, health); } }

    public int Points
    {
        get
        {
            switch (kind)
            {
                case Kind.ArmouredVehicle: return 300;
                case Kind.SupplyDepot: return 250;
                case Kind.SignalJammer: return 200;
                case Kind.FuelDepot: return 150;
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
                case Kind.SignalJammer: return 70f;
                case Kind.FuelDepot: return 50f;
                default: return 80f;
            }
        }
    }

    void Awake()
    {
        health = MaxHealth;
    }

    void Start()
    {
        // Runtime-built targets (jammer station, fuel cache) and scene-built
        // ones alike get their silhouette outline from here.
        if (GetComponent<TargetOutline>() == null) gameObject.AddComponent<TargetOutline>();
    }

    public void SetKind(Kind value)
    {
        kind = value;
        health = MaxHealth;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, transform.position);
    }

    public void TakeDamage(float amount, Vector3 source)
    {
        if (IsDestroyed || amount <= 0f) return;

        // A live jammer spoofs the terminal guidance: the hit lands, but only
        // scratches the target, and the target cannot be finished while the
        // jammer stands. The mission does not count it and does not fail — the
        // player sees "ТАНК: N HP · СНАЧАЛА ПОМЕХИ" and goes for the station.
        if (ProtectedByJammer) amount = Mathf.Min(amount * 0.1f, Mathf.Max(0f, health - 1f));
        if (amount <= 0f) return;

        bool weakHit = IsWeakHit(source);
        if (weakHit)
        {
            // The marker is on the rear (-Z) face. Front and flank attacks
            // still work, but a rear approach saves a drone with the free kit.
            amount *= 2f;
        }

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
        DestroyedViaWeakSpot = weakHit;
        Explode();

        if (OnDestroyed != null) OnDestroyed(this, Points);
    }

    public bool IsWeakHit(Vector3 source)
    {
        if (!WeakRear || kind != Kind.ArmouredVehicle) return false;
        Vector3 localSource = transform.InverseTransformPoint(source);
        return localSource.z < -0.5f && Mathf.Abs(localSource.z) > Mathf.Abs(localSource.x);
    }

    // ---------- damaged, not dead ----------

    /// <summary>
    /// A hit the target survived. It used to repaint every slot orange, which
    /// read as a texture error and told the player nothing about hitting it
    /// again. Now the vehicle keeps its own colours, darkened with soot; a
    /// dark smoke column rises (smoke without flame keeps "wounded" apart
    /// from the burning wreck); and a health bar appears over it, which is
    /// what actually says "still alive — finish it".
    /// </summary>
    void ApplyBattleDamage()
    {
        IsDamaged = true;

        if (GameEffects.Instance != null)
            GameEffects.Instance.HardImpact(transform.position + Vector3.up, Vector3.up);

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayHardImpact(transform.position);

        Soot(0.55f);
        SpawnSmokePlume(0.75f);
        TargetHealthBar.Attach(this);

        transform.rotation *= Quaternion.Euler(
            UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
    }

    /// <summary>
    /// Darkens every material with per-target copies. The colour is set under
    /// every name a shader here might use for it — Standard's _Color, the
    /// glTF importer's baseColorFactor, URP-style _BaseColor — which is why a
    /// single MaterialPropertyBlock tint did nothing on the downloaded models.
    /// </summary>
    void Soot(float factor)
    {
        var copies = new System.Collections.Generic.Dictionary<Material, Material>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (TargetOutline.IsOverlay(renderer) || renderer is ParticleSystemRenderer) continue;
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                Material original = slots[i];
                if (original == null) continue;
                if (!copies.TryGetValue(original, out Material copy))
                {
                    copy = new Material(original) { name = original.name + " (soot)" };
                    foreach (string property in new[] { "_Color", "baseColorFactor", "_BaseColor" })
                    {
                        if (!copy.HasProperty(property)) continue;
                        Color colour = copy.GetColor(property);
                        copy.SetColor(property, new Color(colour.r * factor, colour.g * factor,
                                                          colour.b * factor, colour.a));
                    }
                    copies[original] = copy;
                }
                slots[i] = copy;
            }
            renderer.sharedMaterials = slots;
        }
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

        // The warhead plays the blast; this is only the target confirmation.
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayTargetDestroyed();

        // Burnt, but still recognisably the same vehicle: darken the
        // existing materials rather than flood every slot with one flat black,
        // which erased every panel line and read as a hole in the picture.
        Soot(IsDamaged ? 0.3f : 0.18f);

        // The wreck stays exactly where it stood. Tilting the whole object a
        // random few degrees and sinking it 25 cm lifted a depot's posts off
        // the ground on one side and buried them on the other - the destroyed
        // state looked like a physics bug. Damage is shown on the object and
        // around it instead: soot, heavier smoke, a burn patch on the ground
        // and debris thrown out on the grass. (Decals on the hull were tried
        // and dropped: placed on the collider box, they floated off the
        // actual model surface.)
        ScorchGround();
        ScatterDebris();

        SpawnFire();
    }

    /// <summary>A large burn patch on the ground under and around the wreck.</summary>
    void ScorchGround()
    {
        Material scorch = ScorchMaterial();
        if (scorch == null) return;
        Vector3 point = GroundBelow(transform.position);
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "ScorchMarkerGround";
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null) FieldProps.Discard(collider);
        quad.transform.SetParent(transform, true);
        quad.transform.position = point + Vector3.up * 0.03f;
        quad.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
        float size = FootprintRadius * 5f;
        quad.transform.localScale = new Vector3(size, size, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = scorch;
    }

    /// <summary>
    /// Charred chunks lying on the grass round the wreck. Each one is set on
    /// the ground by its own half-height, so none floats or sinks.
    /// </summary>
    void ScatterDebris()
    {
        Material burnt = Resources.Load<Material>("Materials/Mat_Burnt");
        float reach = FootprintRadius + 1.5f;
        int pieces = kind == Kind.ArmouredVehicle ? 9 : 7;
        for (int i = 0; i < pieces; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = UnityEngine.Random.Range(reach * 0.8f, reach * 1.8f);
            Vector3 around = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            var size = new Vector3(UnityEngine.Random.Range(0.25f, 0.8f), UnityEngine.Random.Range(0.08f, 0.25f),
                                   UnityEngine.Random.Range(0.25f, 0.7f));

            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "DebrisMarker";
            Collider collider = chunk.GetComponent<Collider>();
            if (collider != null) FieldProps.Discard(collider);
            chunk.transform.SetParent(transform, true);
            chunk.transform.position = GroundBelow(around) + Vector3.up * (size.y * 0.45f);
            chunk.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-6f, 6f),
                UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-6f, 6f));
            chunk.transform.localScale = size;
            if (burnt != null) chunk.GetComponent<Renderer>().sharedMaterial = burnt;
        }
    }

    static Material ScorchMaterial()
    {
        Material material = Resources.Load<Material>("Materials/Mat_Scorch");
        return material != null ? material : Resources.Load<Material>("Materials/Mat_BulletHole");
    }

    static Vector3 GroundBelow(Vector3 position)
    {
        GameObject terrain = GameObject.Find("Terrain");
        Collider ground = terrain != null ? terrain.GetComponent<Collider>() : null;
        RaycastHit hit;
        if (ground != null && ground.Raycast(new Ray(position + Vector3.up * 30f, Vector3.down), out hit, 80f))
            return hit.point;
        return new Vector3(position.x, 0f, position.z);
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
        SpawnSmokePlume(radius / 0.55f, holder.transform);

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

        // AddComponent starts the system playing immediately (playOnAwake
        // defaults true), and ParticleSystemMainModule.duration refuses to be
        // set on a system that is already playing — Stop() first, configure
        // everything, then Play() once it is actually ready.
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

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

        system.Play();
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
