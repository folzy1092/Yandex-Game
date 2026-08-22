/// <summary>
/// Match configuration chosen by the player in the main menu.
/// Static so the values survive the load from the menu scene into the arena scene.
/// </summary>
public static class MatchSettings
{
    public const int MinBots = 1;
    public const int MaxBots = 10;
    public const int DefaultBots = 5;

    public const int MinFrags = 10;
    public const int MaxFrags = 100;
    public const int DefaultFrags = 50;

    public static int BotCount = DefaultBots;
    public static int FragLimit = DefaultFrags;
}
