using UnityEngine;

/// <summary>
/// The custom layers the game relies on, and the masks built from them.
///
/// The important one is <see cref="Character"/>: the CharacterController capsule
/// lives there and is deliberately excluded from shooting raycasts. Without that
/// the capsule — which wraps the whole body — would absorb every bullet before it
/// could reach a hitbox, and body-part damage would never happen.
///
/// The layers themselves are created by GameLayersSetup in the Editor folder.
/// </summary>
public static class GameLayers
{
    public const string CharacterName = "Character";
    public const string HitboxName = "Hitbox";
    public const string WeaponName = "ViewWeapon";

    public static int Character { get { return LayerMask.NameToLayer(CharacterName); } }
    public static int Hitbox { get { return LayerMask.NameToLayer(HitboxName); } }
    public static int Weapon { get { return LayerMask.NameToLayer(WeaponName); } }

    /// <summary>
    /// What bullets and line-of-sight checks are allowed to hit: level geometry
    /// and hitboxes, but not the character capsules or the first-person weapon
    /// model hanging in front of the camera.
    /// </summary>
    public static int ShootableMask
    {
        get
        {
            int mask = ~0;

            int character = Character;
            if (character >= 0) mask &= ~(1 << character);

            int weapon = Weapon;
            if (weapon >= 0) mask &= ~(1 << weapon);

            return mask;
        }
    }

    /// <summary>
    /// Level geometry only — no people at all. Used for the bots' obstacle checks,
    /// so they steer around walls rather than flinching at every passing body.
    /// </summary>
    public static int GeometryMask
    {
        get
        {
            int mask = ShootableMask;

            int hitbox = Hitbox;
            if (hitbox >= 0) mask &= ~(1 << hitbox);

            return mask;
        }
    }

    /// <summary>Applies a layer to an object and everything under it.</summary>
    public static void ApplyRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0) return;

        root.layer = layer;
        foreach (Transform child in root.transform)
            ApplyRecursively(child.gameObject, layer);
    }
}
