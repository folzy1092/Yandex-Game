using UnityEngine;

/// <summary>
/// One damageable body part. Shots are resolved against these rather than
/// against the CharacterController capsule, which is what makes a headshot
/// worth more than a shot in the leg.
///
/// Hitboxes are triggers so they never interfere with movement, and they sit on
/// their own layer so <see cref="Hitscan"/> can aim at them while ignoring the
/// capsule that would otherwise swallow every bullet.
/// </summary>
public class Hitbox : MonoBehaviour
{
    public enum Part
    {
        Head,
        Torso,
        Arm,
        Leg
    }

    public Part part = Part.Torso;
    public Health owner;

    public float DamageMultiplier
    {
        get
        {
            switch (part)
            {
                case Part.Head: return 3f;
                case Part.Arm: return 0.7f;
                case Part.Leg: return 0.75f;
                default: return 1f;
            }
        }
    }

    public bool IsHeadshot { get { return part == Part.Head; } }
}
