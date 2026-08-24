using UnityEngine;

public enum WarheadType
{
    /// <summary>Light charge. Weak blast, but the drone flies noticeably better.</summary>
    Compact,

    /// <summary>Standard charge. Enough for anything on the map if you hit it properly.</summary>
    Standard,

    /// <summary>Heavy charge. Overkills anything, widest blast, noticeably heavier to fly.</summary>
    Heavy
}

/// <summary>
/// What a warhead choice costs and buys.
///
/// The trade is the point: the compact charge cannot reliably kill armour, but
/// the lighter drone accelerates harder and turns tighter, so it is the better
/// pick against trucks, depots and antennas. The standard charge kills anything
/// but flies like it is carrying something.
/// </summary>
public struct WarheadProfile
{
    public float damage;
    public float blastRadius;

    /// <summary>Multiplies the drone's thrust — under 1 means a heavier aircraft.</summary>
    public float thrustFactor;

    /// <summary>Multiplies the drone's top speed.</summary>
    public float speedFactor;

    public string DisplayName;

    public static WarheadProfile For(WarheadType type)
    {
        if (type == WarheadType.Compact)
        {
            return new WarheadProfile
            {
                damage = 85f,
                blastRadius = 4.5f,
                thrustFactor = 1.25f,
                speedFactor = 1.2f,
                DisplayName = "МАЛЫЙ"
            };
        }

        if (type == WarheadType.Standard)
        {
            return new WarheadProfile
            {
                damage = 165f,
                blastRadius = 7.5f,
                thrustFactor = 1f,
                speedFactor = 1f,
                DisplayName = "СТАНДАРТ"
            };
        }

        return new WarheadProfile
        {
            damage = 230f,
            blastRadius = 9.5f,
            thrustFactor = 0.85f,
            speedFactor = 0.9f,
            DisplayName = "ТЯЖЁЛЫЙ"
        };
    }
}
