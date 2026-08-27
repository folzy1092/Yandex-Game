using System;
using System.Collections.Generic;

/// <summary>
/// Language detection (Yandex Games 2.14) plus every player-facing string.
///
/// The SDK reports the language during startup. Menu and HUD read
/// <see cref="T"/> so the English draft is actually English in-game — not
/// just detected and then ignored.
///
/// The on-screen title is always "DRONE STRIKE". That must match the console
/// title field on every language tab: "Drone Strike" (title case, no extra
/// subtitle in the name).
/// </summary>
public static class Localization
{
    public enum Language
    {
        Russian,
        English
    }

    public const string GameTitle = "DRONE STRIKE";
    public const string GameTitleCatalog = "Drone Strike";

    public static Language Current { get; private set; }

    /// <summary>True once a language code has arrived, or the wait timed out.</summary>
    public static bool IsResolved { get; private set; }

    public static event Action OnLanguageChanged;

    static readonly Dictionary<string, string> Ru = BuildRu();
    static readonly Dictionary<string, string> En = BuildEn();

    /// <summary>
    /// Applies a language code from the SDK, for example "ru", "en" or "tr".
    /// </summary>
    public static void SetFromCode(string code)
    {
        Language detected = Language.English;

        if (!string.IsNullOrEmpty(code))
        {
            string normalised = code.Trim().ToLowerInvariant();

            if (normalised.StartsWith("ru") || normalised.StartsWith("be")
                || normalised.StartsWith("kk") || normalised.StartsWith("uk")
                || normalised.StartsWith("uz"))
            {
                detected = Language.Russian;
            }
        }

        bool changed = !IsResolved || Current != detected;
        Current = detected;
        IsResolved = true;
        if (changed && OnLanguageChanged != null) OnLanguageChanged();
    }

    /// <summary>Ends the startup wait even if no code ever arrived.</summary>
    public static void MarkResolved()
    {
        if (IsResolved) return;
        IsResolved = true;
        if (OnLanguageChanged != null) OnLanguageChanged();
    }

    public static string T(string key)
    {
        Dictionary<string, string> table = Current == Language.English ? En : Ru;
        string value;
        if (table.TryGetValue(key, out value)) return value;
        if (Ru.TryGetValue(key, out value)) return value;
        return key;
    }

    public static string F(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    public static string DroneName(string id)
    {
        return T("drone." + id);
    }

    public static string DroneTagline(string id)
    {
        return T("drone." + id + ".tagline");
    }

    public static string MapName(string id)
    {
        return T("map." + id);
    }

    public static string MapTagline(string id)
    {
        return T("map." + id + ".tagline");
    }

    public static string WarheadName(WarheadType type)
    {
        switch (type)
        {
            case WarheadType.Compact: return T("warhead.compact");
            case WarheadType.Standard: return T("warhead.standard");
            default: return T("warhead.heavy");
        }
    }

    static Dictionary<string, string> BuildRu()
    {
        return new Dictionary<string, string>
        {
            { "menu.subtitle", "СИМУЛЯТОР УДАРНОГО FPV-ДРОНА" },
            { "menu.kit", "СНАРЯЖЕНИЕ" },
            { "menu.mission", "ЗАДАНИЕ" },
            { "menu.controls", "W / S — вперёд и назад по взгляду     A / D — снос     Space / Ctrl — высота\nМышь — камера     Esc — пауза     Дрон детонирует при ударе" },
            { "menu.launch", "В БОЙ" },
            { "menu.launch_map", "В БОЙ · {0}" },
            { "menu.drone", "ДРОН:   {0}" },
            { "menu.charge", "ЗАРЯД:   {0}" },
            { "menu.targets", "ЦЕЛЕЙ:  {0}" },
            { "menu.pick_drone", "ВЫБОР ДРОНА" },
            { "menu.pick_drone_note", "Каждый следующий дрон быстрее и бьёт сильнее предыдущего." },
            { "menu.pick_charge", "БОЕПРИПАС" },
            { "menu.pick_charge_note", "Малый заряд легче — дрон резвее. Тяжёлый бьёт больнее всех, но вязче в управлении." },
            { "menu.back", "НАЗАД" },
            { "stat.thrust", "ТЯГА" },
            { "stat.speed", "СКОРОСТЬ" },
            { "stat.damage", "ЗАРЯД" },
            { "stat.endurance", "РЕСУРС" },
            { "stat.figures", "УРОН  {0}          РАДИУС  {1} М" },

            { "action.selected", "ВЫБРАН" },
            { "action.select", "ВЫБРАТЬ" },
            { "action.fitted", "УСТАНОВЛЕН" },
            { "action.fit", "УСТАНОВИТЬ" },
            { "action.unlock_ad", "ОТКРЫТЬ ЗА РЕКЛАМУ" },
            { "action.need_first", "СНАЧАЛА «{0}»" },
            { "action.map_selected", "ВЫБРАНА" },
            { "action.map_selected_cleared", "ВЫБРАНА · ПРОЙДЕНА" },
            { "action.map_select_cleared", "ВЫБРАТЬ · ПРОЙДЕНА" },

            { "status.need_drone", "Сначала откройте дрон «{0}»." },
            { "status.need_charge", "Сначала откройте заряд «{0}»." },
            { "status.drone_unlocked", "Дрон «{0}» открыт." },
            { "status.charge_unlocked", "Заряд «{0}» открыт." },
            { "status.map_unlocked", "Карта «{0}» открыта." },
            { "status.ad_loading", "Загрузка рекламы..." },
            { "status.ad_failed", "Реклама недоступна. Попробуйте позже." },

            { "drone.scout", "РАЗВЕДЧИК" },
            { "drone.scout.tagline", "Базовый дрон. Лёгкий, послушный, живучая батарея. С малым зарядом бронетехнику может понадобиться подбить дважды." },
            { "drone.hornet", "ШЕРШЕНЬ" },
            { "drone.hornet.tagline", "Резче на разгоне, быстрее в пикировании, заряд плотнее." },
            { "drone.hammer", "МОЛОТ" },
            { "drone.hammer.tagline", "Топовый дрон: быстрее всех и бьёт сильнее всех." },

            { "warhead.compact", "МАЛЫЙ" },
            { "warhead.compact.blurb", "Штатная боевая часть. Дрон с ней заметно легче и охотнее слушается." },
            { "warhead.standard", "СТАНДАРТ" },
            { "warhead.standard.blurb", "Основной заряд. Снимает броню с первого захода при точном попадании." },
            { "warhead.heavy", "ТЯЖЁЛЫЙ" },
            { "warhead.heavy.blurb", "Тяжёлая боевая часть. С запасом хватит на что угодно, но дрон вязче." },

            { "map.outpost", "ОПОРНЫЙ ПУНКТ" },
            { "map.outpost.tagline", "Ровное поле, кольцевая дорога, техника под сетями." },
            { "map.woodline", "ЛЕСНАЯ ДОРОГА" },
            { "map.woodline.tagline", "Холмы и плотный лес. Цели прячутся, подлёт низкий." },
            { "map.crossroads", "ПЕРЕКРЁСТОК" },
            { "map.crossroads.tagline", "Сумерки, широкая развязка, самая насыщенная карта." },

            { "hud.kmh", "{0} КМ/Ч" },
            { "hud.alt", "{0}М" },
            { "hud.signal_lost", "СИГНАЛ ПОТЕРЯН" },
            { "hud.summary", "ЦЕЛЕЙ: {0} / {1}\nДРОНОВ: {2}\nДРОН: {3}\nЗАРЯД: {4}" },
            { "hud.pause", "ПАУЗА" },
            { "hud.resume", "ПРОДОЛЖИТЬ" },
            { "hud.menu", "В МЕНЮ" },
            { "hud.retry", "ЗАНОВО" },
            { "hud.win", "МИССИЯ ВЫПОЛНЕНА" },
            { "hud.lose", "МИССИЯ ПРОВАЛЕНА" },
            { "hud.result", "Уничтожено целей: {0} из {1}\nОчки: {2}" },
            { "hud.revive", "+1 ДРОН ЗА РЕКЛАМУ" },
            { "hud.loading", "ЗАГРУЗКА..." },
            { "hud.ad_failed", "РЕКЛАМА НЕДОСТУПНА" },
        };
    }

    static Dictionary<string, string> BuildEn()
    {
        return new Dictionary<string, string>
        {
            { "menu.subtitle", "FPV STRIKE DRONE SIMULATOR" },
            { "menu.kit", "LOADOUT" },
            { "menu.mission", "MISSION" },
            { "menu.controls", "W / S — fly along look     A / D — strafe     Space / Ctrl — altitude\nMouse — camera     Esc — pause     Drone detonates on impact" },
            { "menu.launch", "LAUNCH" },
            { "menu.launch_map", "LAUNCH · {0}" },
            { "menu.drone", "DRONE:   {0}" },
            { "menu.charge", "PAYLOAD:   {0}" },
            { "menu.targets", "TARGETS:  {0}" },
            { "menu.pick_drone", "SELECT DRONE" },
            { "menu.pick_drone_note", "Each next drone is faster and hits harder than the last." },
            { "menu.pick_charge", "WARHEAD" },
            { "menu.pick_charge_note", "A light charge flies snappier. Heavy hits hardest, but the drone feels sluggish." },
            { "menu.back", "BACK" },
            { "stat.thrust", "THRUST" },
            { "stat.speed", "SPEED" },
            { "stat.damage", "PAYLOAD" },
            { "stat.endurance", "ENDURANCE" },
            { "stat.figures", "DAMAGE  {0}          RADIUS  {1} M" },

            { "action.selected", "SELECTED" },
            { "action.select", "SELECT" },
            { "action.fitted", "FITTED" },
            { "action.fit", "FIT" },
            { "action.unlock_ad", "UNLOCK WITH AD" },
            { "action.need_first", "UNLOCK «{0}» FIRST" },
            { "action.map_selected", "SELECTED" },
            { "action.map_selected_cleared", "SELECTED · CLEARED" },
            { "action.map_select_cleared", "SELECT · CLEARED" },

            { "status.need_drone", "Unlock the «{0}» drone first." },
            { "status.need_charge", "Unlock the «{0}» warhead first." },
            { "status.drone_unlocked", "Drone «{0}» unlocked." },
            { "status.charge_unlocked", "Warhead «{0}» unlocked." },
            { "status.map_unlocked", "Map «{0}» unlocked." },
            { "status.ad_loading", "Loading ad..." },
            { "status.ad_failed", "Ad unavailable. Try again later." },

            { "drone.scout", "SCOUT" },
            { "drone.scout.tagline", "Starter drone. Light, obedient, long battery. With a light charge, armour may take two hits." },
            { "drone.hornet", "HORNET" },
            { "drone.hornet.tagline", "Snappier acceleration, faster dives, denser charge." },
            { "drone.hammer", "HAMMER" },
            { "drone.hammer.tagline", "Top drone: fastest of the three and hits the hardest." },

            { "warhead.compact", "LIGHT" },
            { "warhead.compact.blurb", "Stock warhead. The drone stays noticeably lighter and more willing." },
            { "warhead.standard", "STANDARD" },
            { "warhead.standard.blurb", "Main charge. Strips armour on a clean first hit." },
            { "warhead.heavy", "HEAVY" },
            { "warhead.heavy.blurb", "Heavy warhead. Enough for anything, but the drone feels sluggish." },

            { "map.outpost", "OUTPOST" },
            { "map.outpost.tagline", "Flat field, ring road, vehicles under nets." },
            { "map.woodline", "FOREST ROAD" },
            { "map.woodline.tagline", "Hills and dense woods. Targets hide, approach stays low." },
            { "map.crossroads", "CROSSROADS" },
            { "map.crossroads.tagline", "Dusk, a wide junction, the busiest map." },

            { "hud.kmh", "{0} KM/H" },
            { "hud.alt", "{0}M" },
            { "hud.signal_lost", "SIGNAL LOST" },
            { "hud.summary", "TARGETS: {0} / {1}\nDRONES: {2}\nDRONE: {3}\nPAYLOAD: {4}" },
            { "hud.pause", "PAUSED" },
            { "hud.resume", "RESUME" },
            { "hud.menu", "MENU" },
            { "hud.retry", "RETRY" },
            { "hud.win", "MISSION COMPLETE" },
            { "hud.lose", "MISSION FAILED" },
            { "hud.result", "Targets destroyed: {0} of {1}\nScore: {2}" },
            { "hud.revive", "+1 DRONE FOR AD" },
            { "hud.loading", "LOADING..." },
            { "hud.ad_failed", "AD UNAVAILABLE" },
        };
    }
}
