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
    public float dragFactor;

    /// <summary>Multiplies blast damage — a bigger airframe carries a bigger charge.</summary>
    public float damageFactor;

    /// <summary>Body colour, so the three read differently on the loadout screen.</summary>
    public Color accent;

    /// <summary>False for the starter airframe, true for later unlocks.</summary>
    public bool needsUnlock;

    /// <summary>
    /// Id of the airframe that has to be unlocked before this one is even
    /// offered, or null for one that can be unlocked straight away.
    /// </summary>
    public string requiresId;
}

/// <summary>
/// The drone roster, which one is selected, and which ones the player has
/// unlocked.
///
/// Mission medals unlock airframes through play. Rewarded ads can unlock them
/// earlier, while the starter can clear every mission without an ad.
///
/// Each airframe has a role: Scout brakes and lasts longer, Hornet intercepts,
/// Hammer carries the hardest hit. Mission medals unlock them through play;
/// existing rewarded unlock keys remain valid as an optional shortcut.
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
            tagline = "Базовый дрон. Лёгкий, послушный, живучая батарея. С малым "
                     + "зарядом бронетехнику может понадобиться подбить дважды.",
            thrustFactor = 1f,
            speedFactor = 1f,
            enduranceFactor = 1.15f,
            dragFactor = 1.3f,
            damageFactor = 1f,
            accent = new Color(0.15f, 0.45f, 0.75f),
            needsUnlock = false
        },
        new DroneModel
        {
            id = "hornet",
            displayName = "ШЕРШЕНЬ",
            tagline = "Резче на разгоне, быстрее в пикировании, заряд плотнее.",
            thrustFactor = 1.25f,
            speedFactor = 1.35f,
            enduranceFactor = 0.85f,
            dragFactor = 0.95f,
            damageFactor = 1.1f,
            accent = new Color(0.85f, 0.55f, 0.12f),
            needsUnlock = true,
            requiresId = null
        },
        new DroneModel
        {
            id = "hammer",
            displayName = "МОЛОТ",
            tagline = "Тяжёлый удар по укреплениям; ниже скорость и меньше запас батареи.",
            thrustFactor = 0.9f,
            speedFactor = 0.92f,
            enduranceFactor = 0.8f,
            dragFactor = 0.85f,
            damageFactor = 1.7f,
            accent = new Color(0.72f, 0.22f, 0.20f),
            needsUnlock = true,

            // The heavy airframe is the last one on the ladder rather than a
            // second thing to buy on day one. Offering both at once means a
            // player picks whichever sounds better and never sees the other
            // ad, and a roster with no order to it reads as a shop rather than
            // as progress.
            requiresId = "hornet"
        }
    };

    /// <summary>Fired when a drone is unlocked or the selection changes.</summary>
    public static event Action OnChanged;

    public static bool IsUnlocked(DroneModel model)
    {
        if (!model.needsUnlock) return true;
        if (PlayerPrefs.GetInt(UnlockKeyPrefix + model.id, 0) == 1) return true;
        return model.id == "hornet" ? MissionChallenges.TotalStars >= 2
            : model.id == "hammer" && MissionChallenges.TotalStars >= 7;
    }

    /// <summary>
    /// Whether this airframe's unlock can even be offered yet. False while the
    /// one it is gated behind is still locked.
    /// </summary>
    public static bool IsAvailable(DroneModel model)
    {
        if (string.IsNullOrEmpty(model.requiresId)) return true;

        foreach (DroneModel other in Models)
            if (other.id == model.requiresId) return IsUnlocked(other);

        // Gated behind an airframe that is not in the roster any more: treat it
        // as open rather than permanently unreachable.
        return true;
    }

    /// <summary>Display name of what has to be unlocked first, or an empty string.</summary>
    public static string PrerequisiteName(DroneModel model)
    {
        if (string.IsNullOrEmpty(model.requiresId)) return string.Empty;

        foreach (DroneModel other in Models)
            if (other.id == model.requiresId) return other.displayName;

        return string.Empty;
    }

    public static void Unlock(DroneModel model)
    {
        if (!IsAvailable(model)) return;

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

    // ---------- charges ----------

    // The same ladder as the airframes: compact is free, standard opens after
    // the first medal, and heavy after six stars. Rewarded ads are shortcuts;
    // the old keys still grant the same unlocks for returning players.

    const string WarheadUnlockKeyPrefix = "warhead_unlocked_";

    public static bool IsWarheadUnlocked(WarheadType charge)
    {
        if (charge == WarheadType.Compact) return true;
        if (PlayerPrefs.GetInt(WarheadUnlockKeyPrefix + charge, 0) == 1) return true;
        return charge == WarheadType.Standard ? MissionChallenges.TotalStars >= 1
            : charge == WarheadType.Heavy && MissionChallenges.TotalStars >= 6;
    }

    /// <summary>Whether this charge's unlock can even be offered yet — the same idea as DroneLoadout.IsAvailable.</summary>
    public static bool IsWarheadAvailable(WarheadType charge)
    {
        if (charge != WarheadType.Heavy) return true;
        return IsWarheadUnlocked(WarheadType.Standard);
    }

    /// <summary>Display name of the charge that has to be unlocked first, or an empty string.</summary>
    public static string WarheadPrerequisiteName(WarheadType charge)
    {
        if (charge != WarheadType.Heavy) return string.Empty;
        return WarheadProfile.For(WarheadType.Standard).DisplayName;
    }

    public static void UnlockWarhead(WarheadType charge)
    {
        if (charge == WarheadType.Compact) return;
        if (!IsWarheadAvailable(charge)) return;

        PlayerPrefs.SetInt(WarheadUnlockKeyPrefix + charge, 1);
        PlayerPrefs.Save();
        Notify();
    }

    /// <summary>The charge fitted to the drone, remembered between missions.</summary>
    public static WarheadType SelectedWarhead
    {
        get
        {
            var stored = (WarheadType)PlayerPrefs.GetInt(WarheadKey, (int)WarheadType.Compact);

            // Same guard as the airframe: a selection can outlive its unlock if
            // browser storage is wiped between sessions.
            return IsWarheadUnlocked(stored) ? stored : WarheadType.Compact;
        }
        set
        {
            if (!IsWarheadUnlocked(value)) return;

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
