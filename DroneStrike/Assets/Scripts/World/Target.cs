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

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        health -= amount;
        if (health > 0f) return;

        IsDestroyed = true;
        Explode();

        if (OnDestroyed != null) OnDestroyed(this, Points);
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
    }
}
