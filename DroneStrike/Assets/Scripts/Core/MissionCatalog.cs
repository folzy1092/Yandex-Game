using System;
using UnityEngine;

/// <summary>One mission the player can fly.</summary>
public struct MissionMap
{
    public string id;
    public string sceneName;
    public string displayName;
    public string tagline;

    /// <summary>Roughly how many targets it holds, for the card.</summary>
    public int targetCount;

    /// <summary>Colour band on the card, so the three read apart at a glance.</summary>
    public Color accent;
}

/// <summary>
/// The three maps, which are unlocked, and which have been cleared.
///
/// A map opens either by clearing the one before it or by watching an ad. Both
/// doors matter: the ad is the point of the whole thing, but a player who is
/// good at the game must never be forced through it, and a player who is stuck
/// must never be walled off from the rest of the content. Offering both means
/// the ad is a shortcut rather than a toll.
/// </summary>
public static class MissionCatalog
{
    const string UnlockKeyPrefix = "map_unlocked_";
    const string ClearedKeyPrefix = "map_cleared_";
    const string SelectedKey = "map_selected";

    public static readonly MissionMap[] Maps =
    {
        new MissionMap
        {
            id = "outpost",
            sceneName = "Mission1",
            displayName = "ОПОРНЫЙ ПУНКТ",
            tagline = "Ровное поле, кольцевая дорога, техника под сетями.",
            targetCount = 13,
            accent = new Color(0.35f, 0.60f, 0.45f)
        },
        new MissionMap
        {
            id = "woodline",
            sceneName = "Mission2",
            displayName = "ЛЕСНАЯ ДОРОГА",
            tagline = "Холмы и плотный лес. Цели прячутся, подлёт низкий.",
            targetCount = 14,
            accent = new Color(0.45f, 0.52f, 0.25f)
        },
        new MissionMap
        {
            id = "crossroads",
            sceneName = "Mission3",
            displayName = "ПЕРЕКРЁСТОК",
            tagline = "Сумерки, широкая развязка, самая насыщенная карта.",
            targetCount = 16,
            accent = new Color(0.62f, 0.38f, 0.28f)
        }
    };

    public static event Action OnChanged;

    /// <summary>The first map is always open; the rest are earned or unlocked.</summary>
    public static bool IsUnlocked(int index)
    {
        if (index <= 0) return true;
        if (index >= Maps.Length) return false;

        if (PlayerPrefs.GetInt(UnlockKeyPrefix + Maps[index].id, 0) == 1) return true;

        // Clearing the previous map opens the next one without an ad.
        return IsCleared(index - 1);
    }

    public static bool IsCleared(int index)
    {
        if (index < 0 || index >= Maps.Length) return false;
        return PlayerPrefs.GetInt(ClearedKeyPrefix + Maps[index].id, 0) == 1;
    }

    public static void Unlock(int index)
    {
        if (index < 0 || index >= Maps.Length) return;

        PlayerPrefs.SetInt(UnlockKeyPrefix + Maps[index].id, 1);
        PlayerPrefs.Save();
        Notify();
    }

    /// <summary>Called by the mission when every target is down.</summary>
    public static void MarkCleared(string sceneName)
    {
        for (int i = 0; i < Maps.Length; i++)
        {
            if (Maps[i].sceneName != sceneName) continue;

            PlayerPrefs.SetInt(ClearedKeyPrefix + Maps[i].id, 1);
            PlayerPrefs.Save();
            Notify();
            return;
        }
    }

    public static int SelectedIndex
    {
        get
        {
            int index = PlayerPrefs.GetInt(SelectedKey, 0);
            if (index < 0 || index >= Maps.Length) return 0;

            return IsUnlocked(index) ? index : 0;
        }
        set
        {
            if (value < 0 || value >= Maps.Length) return;
            if (!IsUnlocked(value)) return;

            PlayerPrefs.SetInt(SelectedKey, value);
            PlayerPrefs.Save();
            Notify();
        }
    }

    public static MissionMap Selected { get { return Maps[SelectedIndex]; } }

    /// <summary>Name of the map that has to be cleared to open this one.</summary>
    public static string PrerequisiteName(int index)
    {
        if (index <= 0 || index >= Maps.Length) return string.Empty;
        return Maps[index - 1].displayName;
    }

    static void Notify()
    {
        if (OnChanged != null) OnChanged();
    }
}
