using UnityEngine;

public enum BotDifficulty
{
    Easy,
    Normal,
    Hard
}

/// <summary>
/// The numbers that decide how dangerous a bot is, in one place so difficulty is
/// a single lever rather than a dozen scattered constants.
///
/// The one that matters most for how human a bot feels is <see cref="reactionTime"/>:
/// a bot that fires the instant you enter its view reads as a machine no matter
/// how badly it aims, while one that takes a moment to notice you reads as a
/// player even when it shoots well.
/// </summary>
public struct BotProfile
{
    public float reactionTime;      // delay between spotting a target and firing
    public float spreadDegrees;     // aim cone; larger is worse
    public float fireCooldown;      // seconds between shots
    public int damage;
    public float viewDistance;
    public float fieldOfView;
    public float moveSpeed;
    public float turnSpeed;

    /// <summary>Health fraction below which a bot considers backing off.</summary>
    public float retreatHealthFraction;

    /// <summary>How much a bot leads its aim when the target is moving sideways.</summary>
    public float aimPrediction;

    public static BotProfile For(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                return new BotProfile
                {
                    reactionTime = 0.65f,
                    spreadDegrees = 8f,
                    fireCooldown = 1.3f,
                    damage = 12,
                    viewDistance = 28f,
                    fieldOfView = 95f,
                    moveSpeed = 3f,
                    turnSpeed = 190f,
                    retreatHealthFraction = 0.5f,
                    aimPrediction = 0f
                };

            case BotDifficulty.Hard:
                return new BotProfile
                {
                    reactionTime = 0.16f,
                    spreadDegrees = 2.2f,
                    fireCooldown = 0.55f,
                    damage = 22,
                    viewDistance = 60f,
                    fieldOfView = 125f,
                    moveSpeed = 4.6f,
                    turnSpeed = 340f,
                    retreatHealthFraction = 0.22f,
                    aimPrediction = 0.35f
                };

            default:
                return new BotProfile
                {
                    reactionTime = 0.35f,
                    spreadDegrees = 4.5f,
                    fireCooldown = 0.9f,
                    damage = 18,
                    viewDistance = 45f,
                    fieldOfView = 110f,
                    moveSpeed = 3.8f,
                    turnSpeed = 260f,
                    retreatHealthFraction = 0.32f,
                    aimPrediction = 0.18f
                };
        }
    }

    /// <summary>
    /// Nudges a profile by a per-bot personality value so a squad of bots does not
    /// behave like one mind copied several times.
    /// </summary>
    /// <param name="aggression">0 = cautious and careful, 1 = reckless and pushy.</param>
    public BotProfile WithPersonality(float aggression)
    {
        BotProfile tuned = this;

        // Aggressive bots close in and shoot sooner but aim worse under pressure;
        // cautious ones hang back, take their time and land more shots.
        tuned.reactionTime *= Mathf.Lerp(1.25f, 0.75f, aggression);
        tuned.spreadDegrees *= Mathf.Lerp(0.8f, 1.3f, aggression);
        tuned.moveSpeed *= Mathf.Lerp(0.92f, 1.12f, aggression);
        tuned.retreatHealthFraction *= Mathf.Lerp(1.5f, 0.4f, aggression);

        return tuned;
    }
}
