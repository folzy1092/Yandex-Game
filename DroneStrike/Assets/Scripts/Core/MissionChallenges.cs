using UnityEngine;

public enum ChallengeKind
{
    FirstFlight,
    Intercept,
    WeakSpot,
    Jammer,
    GroupStrike,
    FreePlan
}

/// <summary>Mission rules; the existing scene remains the environment.</summary>
public struct MissionDefinition
{
    public ChallengeKind kind;
    public string id;
    public string titleRu;
    public string titleEn;
    public string briefRu;
    public string briefEn;
    public int droneBudget;
    public float goldSeconds;
    public float silverSeconds;
    public int goldDrones;
    public int silverDrones;

    public string Title { get { return Localization.Current == Localization.Language.English ? titleEn : titleRu; } }
    public string Brief { get { return Localization.Current == Localization.Language.English ? briefEn : briefRu; } }
}

/// <summary>Campaign progress, stored separately from legacy map unlock keys.</summary>
public static class MissionChallenges
{
    const string SelectedKey = "challenge_selected";
    const string MedalPrefix = "challenge_medal_";
    const string BestTimePrefix = "challenge_time_";
    const string BestDronesPrefix = "challenge_drones_";
    const string VariantPrefix = "challenge_variant_";

    public static readonly MissionDefinition[] Definitions =
    {
        new MissionDefinition { kind = ChallengeKind.FirstFlight, id = "first_flight",
            titleRu = "ПЕРВЫЙ ВЫЛЕТ", titleEn = "FIRST FLIGHT",
            briefRu = "Поднимись, опустись, осмотрись и уничтожь грузовик.",
            briefEn = "Climb, descend, look around and strike the truck.",
            droneBudget = 4, goldSeconds = 45f, silverSeconds = 90f, goldDrones = 1, silverDrones = 2 },
        new MissionDefinition { kind = ChallengeKind.Intercept, id = "intercept",
            titleRu = "ПЕРЕХВАТ", titleEn = "INTERCEPT",
            briefRu = "Перехвати патрульный грузовик до выхода из сектора.",
            briefEn = "Intercept the patrol truck before it leaves the sector.",
            droneBudget = 4, goldSeconds = 50f, silverSeconds = 90f, goldDrones = 1, silverDrones = 2 },
        new MissionDefinition { kind = ChallengeKind.WeakSpot, id = "weak_spot",
            titleRu = "СЛАБОЕ МЕСТО", titleEn = "WEAK SPOT",
            briefRu = "Зайди к бронецели с отмеченной кормы: там урон выше.",
            briefEn = "Attack the marked rear of the armoured target for extra damage.",
            droneBudget = 4, goldSeconds = 55f, silverSeconds = 100f, goldDrones = 1, silverDrones = 2 },
        new MissionDefinition { kind = ChallengeKind.Jammer, id = "jammer",
            titleRu = "УБРАТЬ ПОМЕХИ", titleEn = "CLEAR THE JAMMER",
            briefRu = "Сначала уничтожь антенну помех, затем бронецель.",
            briefEn = "Destroy the jammer mast, then the armoured target.",
            droneBudget = 5, goldSeconds = 90f, silverSeconds = 150f, goldDrones = 2, silverDrones = 3 },
        new MissionDefinition { kind = ChallengeKind.GroupStrike, id = "group_strike",
            titleRu = "ОДИН УДАР", titleEn = "ONE STRIKE",
            briefRu = "Подорви топливо между грузовиками. На всё есть два дрона.",
            briefEn = "Detonate the fuel between the trucks. You have two drones.",
            droneBudget = 2, goldSeconds = 55f, silverSeconds = 100f, goldDrones = 1, silverDrones = 2 },
        new MissionDefinition { kind = ChallengeKind.FreePlan, id = "free_plan",
            titleRu = "СВОБОДНЫЙ ПЛАН", titleEn = "FREE PLAN",
            briefRu = "Уничтожь три отмеченные цели. Остальные необязательны.",
            briefEn = "Destroy the three marked targets. The others are optional.",
            droneBudget = 7, goldSeconds = 120f, silverSeconds = 210f, goldDrones = 3, silverDrones = 5 }
    };

    public static int SelectedIndex
    {
        get
        {
            int index = PlayerPrefs.GetInt(SelectedKey, 0);
            return IsUnlocked(index) ? index : 0;
        }
        set
        {
            if (!IsUnlocked(value)) return;
            PlayerPrefs.SetInt(SelectedKey, value);
            PlayerPrefs.Save();
        }
    }

    public static MissionDefinition Selected { get { return Definitions[SelectedIndex]; } }

    static int VariantCount(int index)
    {
        // The outpost has two patrols and three choices for the other
        // authored targets. Keep variants bounded so records stay comparable.
        return Definitions[index].kind == ChallengeKind.Intercept ? 2 : 3;
    }

    static int VariantFor(int index)
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(VariantPrefix + Definitions[index].id, 0)) % VariantCount(index);
    }

    public static int Variant { get { return VariantFor(SelectedIndex); } }

    public static void AdvanceVariant()
    {
        PlayerPrefs.SetInt(VariantPrefix + Selected.id, (Variant + 1) % VariantCount(SelectedIndex));
        PlayerPrefs.Save();
    }

    public static bool IsUnlocked(int index)
    {
        if (index < 0 || index >= Definitions.Length) return false;
        return index == 0 || BestMedal(index - 1) > 0;
    }

    public static int BestMedal(int index)
    {
        if (index < 0 || index >= Definitions.Length) return 0;
        return PlayerPrefs.GetInt(MedalPrefix + Definitions[index].id, 0);
    }

    public static int TotalStars
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Definitions.Length; i++) total += BestMedal(i);
            return total;
        }
    }

    public static float BestTime(int index)
    {
        return PlayerPrefs.GetFloat(BestTimePrefix + RecordScope(index), 0f);
    }

    public static int BestDrones(int index)
    {
        return PlayerPrefs.GetInt(BestDronesPrefix + RecordScope(index), 0);
    }

    static string RecordScope(int index)
    {
        return Definitions[index].id + "_v" + VariantFor(index) + "_" +
            DroneLoadout.Selected.id + "_" + DroneLoadout.SelectedWarhead;
    }

    public static int MedalFor(int index, float seconds, int dronesUsed)
    {
        MissionDefinition definition = Definitions[index];
        if (seconds <= definition.goldSeconds && dronesUsed <= definition.goldDrones) return 3;
        if (seconds <= definition.silverSeconds && dronesUsed <= definition.silverDrones) return 2;
        return 1;
    }

    public static void RecordWin(int index, float seconds, int dronesUsed)
    {
        if (index < 0 || index >= Definitions.Length) return;
        string id = Definitions[index].id;
        string scope = RecordScope(index);
        int medal = MedalFor(index, seconds, dronesUsed);
        // Aggregate stars open equipment and later missions; scoped results
        // compare only the same layout and kit.
        PlayerPrefs.SetInt(MedalPrefix + id, Mathf.Max(BestMedal(index), medal));
        PlayerPrefs.SetInt(MedalPrefix + scope,
            Mathf.Max(PlayerPrefs.GetInt(MedalPrefix + scope, 0), medal));
        float previousTime = BestTime(index);
        if (previousTime <= 0f || seconds < previousTime) PlayerPrefs.SetFloat(BestTimePrefix + scope, seconds);
        int previousDrones = BestDrones(index);
        if (previousDrones <= 0 || dronesUsed < previousDrones) PlayerPrefs.SetInt(BestDronesPrefix + scope, dronesUsed);
        PlayerPrefs.Save();
    }
}
