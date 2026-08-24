using System;

/// <summary>
/// Startup language detection, per Yandex Games requirement 2.14.
///
/// The language must be read from the SDK before gameplay begins, or the game
/// is rejected at moderation. YandexAds reports the detected code here as soon
/// as the SDK answers. DroneStrike's menu text is Russian-only for now, so
/// nothing downstream reads <see cref="Current"/> yet — this just satisfies the
/// requirement that detection happens, ready for the day the UI is translated.
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

    /// <summary>
    /// Applies a language code from the SDK, for example "ru", "en" or "tr".
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
}
