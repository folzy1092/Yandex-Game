using System;
using UnityEngine;

/// <summary>
/// One airframe the player can fly.
///
/// The numbers are multipliers on the base drone rather than absolutes, so the
/// flight model stays in one place — <see cref="DroneController"/> — and a new
/// airframe is a line in a table instead of a second copy of the physics.
/// </summary>
public struct DroneModel
{
    public string id;
    public string displayName;
    public string tagline;

    /// <summary>Multiplies thrust: how hard it accelerates and how tightly it turns.</summary>
    public float thrustFactor;

    /// <summary>Multiplies top speed.</summary>
    public float speedFactor;

    /// <summary>Multiplies how long the battery lasts.</summary>
    public float enduranceFactor;

    /// <summary>Multiplies blast damage — a bigger airframe carries a bigger charge.</summary>
    public float damageFactor;

    /// <summary>Body colour, so the three read differently on the loadout screen.</summary>
    public Color accent;

    /// <summary>False for the starter airframe, true for anything behind an ad.</summary>
    public bool needsUnlock;
}

/// <summary>
/// The drone roster, which one is selected, and which ones the player has
/// unlocked.
///
/// Unlocks are bought with attention rather than money: watching a rewarded ad
/// unlocks an airframe permanently. That is the whole monetisation model, so it
/// has to be honest — the starter drone can clear every mission on its own, and
/// the unlocks are faster and harder-hitting rather than the only way to win.
/// A paywall the player cannot pay is worse than no paywall at all.
///
/// State lives in PlayerPrefs, which on a WebGL build is browser storage, so an
/// unlock survives a reload the way the player expects it to.
/// </summary>
public static class DroneLoadout
{
    const string UnlockKeyPrefix = "drone_unlocked_";
    const string SelectedKey = "drone_selected";
    const string WarheadKey = "warhead_selected";

    public static readonly DroneModel[] Models =
    {
        new DroneModel
        {
            id = "scout",
            displayName = "РАЗВЕДЧИК",
            tagline = "Базовый борт. Лёгкий, послушный, живучая батарея.",
            thrustFactor = 1f,
            speedFactor = 1f,
            enduranceFactor = 1f,
            damageFactor = 1f,
            accent = new Color(0.15f, 0.45f, 0.75f),
            needsUnlock = false
        },
        new DroneModel
        {
            id = "hornet",
            displayName = "ШЕРШЕНЬ",
            tagline = "Разгон и скорость выше на треть. Батарея садится быстрее.",
            thrustFactor = 1.35f,
            speedFactor = 1.3f,
            enduranceFactor = 0.85f,
            damageFactor = 1f,
            accent = new Color(0.85f, 0.55f, 0.12f),
            needsUnlock = true
        },
        new DroneModel
        {
            id = "hammer",
            displayName = "МОЛОТ",
            tagline = "Тяжёлый борт: заряд мощнее в полтора раза, но разгон вялый.",
            thrustFactor = 0.85f,
            speedFactor = 0.9f,
            enduranceFactor = 1.15f,
            damageFactor = 1.55f,
            accent = new Color(0.72f, 0.22f, 0.20f),
            needsUnlock = true
        }
    };

    /// <summary>Fired when a drone is unlocked or the selection changes.</summary>
    public static event Action OnChanged;

    public static bool IsUnlocked(DroneModel model)
    {
        if (!model.needsUnlock) return true;
        return PlayerPrefs.GetInt(UnlockKeyPrefix + model.id, 0) == 1;
    }

    public static void Unlock(DroneModel model)
    {
        PlayerPrefs.SetInt(UnlockKeyPrefix + model.id, 1);
        PlayerPrefs.Save();
        Notify();
    }

    /// <summary>Index into <see cref="Models"/>, forced back to the starter if locked.</summary>
    public static int SelectedIndex
    {
        get
        {
            int index = PlayerPrefs.GetInt(SelectedKey, 0);
            if (index < 0 || index >= Models.Length) return 0;

            // A drone can be selected and then have its unlock cleared — by a
            // wiped browser profile, or by a build that adds a new airframe and
            // shifts the indices. Falling back to the starter is always safe.
            return IsUnlocked(Models[index]) ? index : 0;
        }
        set
        {
            if (value < 0 || value >= Models.Length) return;
            if (!IsUnlocked(Models[value])) return;

            PlayerPrefs.SetInt(SelectedKey, value);
            PlayerPrefs.Save();
            Notify();
        }
    }

    public static DroneModel Selected { get { return Models[SelectedIndex]; } }

    /// <summary>The charge fitted to the drone, remembered between missions.</summary>
    public static WarheadType SelectedWarhead
    {
        get
        {
            return PlayerPrefs.GetInt(WarheadKey, (int)WarheadType.Compact) == (int)WarheadType.Standard
                ? WarheadType.Standard
                : WarheadType.Compact;
        }
        set
        {
            PlayerPrefs.SetInt(WarheadKey, (int)value);
            PlayerPrefs.Save();
            Notify();
        }
    }

    static void Notify()
    {
        if (OnChanged != null) OnChanged();
    }
}
