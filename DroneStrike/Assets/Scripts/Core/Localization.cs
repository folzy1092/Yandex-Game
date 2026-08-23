using System;
using System.Collections.Generic;

/// <summary>
/// Game text in the languages the store listing declares.
///
/// Yandex Games requirement 2.14 makes automatic language detection mandatory:
/// the language must be read from the SDK at startup, before gameplay begins,
/// or the game is rejected at moderation. <see cref="YandexAds"/> reports the
/// detected code here as soon as the SDK answers.
/// </summary>
public static class Localization
{
    public enum Language
    {
        Russian,
        English
    }

    public static Language Current { get; private set; }

    /// <summary>True once a language code has arrived, or the wait timed out.</summary>
    public static bool IsResolved { get; private set; }

    public static event Action OnLanguageChanged;

    static readonly Dictionary<string, string> Russian = new Dictionary<string, string>
    {
        { "game_title",    "ЭПИЧНАЯ БИТВА 3D" },
        { "subtitle",      "Бой всех против всех с ботами" },
        { "bots",          "Количество ботов" },
        { "frags",         "Фрагов до победы" },
        { "play",          "В БОЙ" },
        { "loading",       "Загрузка..." },
        { "controls",      "WASD — движение   Shift — бег   Space/колесо — прыжок   Ctrl — присесть\nЛКМ — огонь   R — перезарядка   Esc — освободить курсор" },
        { "your_frags",    "Ваши фраги" },
        { "to_win",        "до победы" },
        { "leader",        "Лидер" },
        { "nobody",        "нет" },
        { "victory",       "ПОБЕДА" },
        { "defeat",        "ПОРАЖЕНИЕ" },
        { "winner",        "Победитель" },
        { "play_again",    "Играть снова" },
        { "to_menu",       "В меню" },
        { "you_died",      "ВЫ УБИТЫ" },
        { "respawn_in",    "Возрождение через" },
        { "respawn_now",   "Возродиться сразу (реклама)" },
        { "results",       "ИТОГИ МАТЧА" },
        { "draw",          "НИЧЬЯ" },
        { "time_up",       "ВРЕМЯ ВЫШЛО" },
        { "difficulty",    "Сложность" },
        { "easy",          "Лёгкая" },
        { "normal",        "Средняя" },
        { "hard",          "Сложная" },
        { "player",        "Игрок" },
        { "bot",           "Бот" }
    };

    static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        { "game_title",    "EPIC BATTLE 3D" },
        { "subtitle",      "Free-for-all deathmatch against bots" },
        { "bots",          "Number of bots" },
        { "frags",         "Frags to win" },
        { "play",          "PLAY" },
        { "loading",       "Loading..." },
        { "controls",      "WASD — move   Shift — sprint   Space/wheel — jump   Ctrl — crouch\nLMB — fire   R — reload   Esc — release cursor" },
        { "your_frags",    "Your frags" },
        { "to_win",        "to win" },
        { "leader",        "Leader" },
        { "nobody",        "none" },
        { "victory",       "VICTORY" },
        { "defeat",        "DEFEAT" },
        { "winner",        "Winner" },
        { "play_again",    "Play again" },
        { "to_menu",       "Main menu" },
        { "you_died",      "YOU WERE KILLED" },
        { "respawn_in",    "Respawning in" },
        { "respawn_now",   "Respawn now (watch an ad)" },
        { "results",       "MATCH RESULTS" },
        { "draw",          "DRAW" },
        { "time_up",       "TIME UP" },
        { "difficulty",    "Difficulty" },
        { "easy",          "Easy" },
        { "normal",        "Normal" },
        { "hard",          "Hard" },
        { "player",        "Player" },
        { "bot",           "Bot" }
    };

    /// <summary>
    /// Applies a language code from the SDK, for example "ru", "en" or "tr".
    /// Anything the game does not translate falls back to English.
    /// </summary>
    public static void SetFromCode(string code)
    {
        Language detected = Language.English;

        if (!string.IsNullOrEmpty(code))
        {
            string normalised = code.Trim().ToLowerInvariant();

            // Russian for the languages of the countries where the game ships in
            // Russian; everything else reads better in English.
            if (normalised.StartsWith("ru") || normalised.StartsWith("be")
                || normalised.StartsWith("kk") || normalised.StartsWith("uk")
                || normalised.StartsWith("uz"))
            {
                detected = Language.Russian;
            }
        }

        Current = detected;
        MarkResolved();
    }

    /// <summary>Ends the startup wait even if no code ever arrived.</summary>
    public static void MarkResolved()
    {
        IsResolved = true;
        if (OnLanguageChanged != null) OnLanguageChanged();
    }

    public static string Get(string key)
    {
        Dictionary<string, string> table = Current == Language.Russian ? Russian : English;

        string value;
        if (table.TryGetValue(key, out value)) return value;
        if (English.TryGetValue(key, out value)) return value;

        return key;
    }
}
