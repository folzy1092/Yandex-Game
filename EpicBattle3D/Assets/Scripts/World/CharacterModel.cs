using UnityEngine;

/// <summary>
/// Builds the blocky humanoid figure used for both the player and the bots,
/// together with the hitboxes that make body-part damage work.
///
/// Everything is primitives assembled in code — no imported meshes, so nothing
/// to license and nothing added to the download.
///
/// Proportions are laid out for a 1.8 m fighter:
///
///     1.75 +---+      head      (x3 damage)
///     1.45 +-------+  torso     (x1)
///          | |   | |  arms      (x0.7)
///     0.85 +-------+
///          | |   | |  legs      (x0.75)
///     0.00 +-+   +-+
/// </summary>
public static class CharacterModel
{
    public class Parts
    {
        public Transform root;
        public Transform leftLegPivot;
        public Transform rightLegPivot;
        public Transform leftArmPivot;
        public Transform rightArmPivot;
        public Transform weaponMount;
        public Renderer[] renderers;
    }

    const float HeadSize = 0.28f;
    const float HeadBottom = 1.45f;
    const float TorsoTop = 1.45f;
    const float TorsoBottom = 0.85f;
    const float TorsoWidth = 0.5f;
    const float TorsoDepth = 0.28f;
    const float LegLength = 0.85f;
    const float LegThickness = 0.18f;
    const float ArmLength = 0.6f;
    const float ArmThickness = 0.14f;
    const float ShoulderHeight = 1.4f;

    /// <param name="hideRenderers">
    /// True for the local player: the body still needs hitboxes so bots can shoot
    /// it, but drawing arms and a head in front of a first-person camera looks wrong.
    /// </param>
    public static Parts Build(GameObject character, Health owner, Material bodyMaterial,
                              Material headMaterial, bool hideRenderers)
    {
        var parts = new Parts();

        var model = new GameObject("Model");
        model.transform.SetParent(character.transform, false);
        parts.root = model.transform;

        AddPart(model.transform, "Head", Hitbox.Part.Head, owner, headMaterial,
                new Vector3(0f, HeadBottom + HeadSize * 0.5f, 0f),
                new Vector3(HeadSize, HeadSize, HeadSize));

        AddPart(model.transform, "Torso", Hitbox.Part.Torso, owner, bodyMaterial,
                new Vector3(0f, (TorsoTop + TorsoBottom) * 0.5f, 0f),
                new Vector3(TorsoWidth, TorsoTop - TorsoBottom, TorsoDepth));

        // Limbs hang from a pivot at the joint so the animator can swing them
        // around the shoulder or hip rather than around their own centre.
        parts.leftLegPivot = AddLimb(model.transform, "LegLeft", Hitbox.Part.Leg, owner, bodyMaterial,
                                     new Vector3(-0.13f, LegLength, 0f),
                                     new Vector3(LegThickness, LegLength, LegThickness));

        parts.rightLegPivot = AddLimb(model.transform, "LegRight", Hitbox.Part.Leg, owner, bodyMaterial,
                                      new Vector3(0.13f, LegLength, 0f),
                                      new Vector3(LegThickness, LegLength, LegThickness));

        parts.leftArmPivot = AddLimb(model.transform, "ArmLeft", Hitbox.Part.Arm, owner, bodyMaterial,
                                     new Vector3(-(TorsoWidth * 0.5f + ArmThickness * 0.5f), ShoulderHeight, 0f),
                                     new Vector3(ArmThickness, ArmLength, ArmThickness));

        parts.rightArmPivot = AddLimb(model.transform, "ArmRight", Hitbox.Part.Arm, owner, bodyMaterial,
                                      new Vector3(TorsoWidth * 0.5f + ArmThickness * 0.5f, ShoulderHeight, 0f),
                                      new Vector3(ArmThickness, ArmLength, ArmThickness));

        // Where a weapon sits: at the end of the right arm, pushed forward a little.
        var mount = new GameObject("WeaponMount");
        mount.transform.SetParent(parts.rightArmPivot, false);
        mount.transform.localPosition = new Vector3(0f, -ArmLength * 0.9f, 0.12f);
        parts.weaponMount = mount.transform;

        parts.renderers = model.GetComponentsInChildren<Renderer>();

        if (hideRenderers)
        {
            for (int i = 0; i < parts.renderers.Length; i++)
                parts.renderers[i].enabled = false;
        }

        return parts;
    }

    static GameObject AddPart(Transform parent, string name, Hitbox.Part part, Health owner,
                              Material material, Vector3 localPosition, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = size;

        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;

        ConfigureHitbox(go, part, owner);
        return go;
    }

    /// <summary>
    /// Creates an empty pivot at the joint with the limb hanging below it, so
    /// rotating the pivot swings the limb the way a real one moves.
    /// </summary>
    static Transform AddLimb(Transform parent, string name, Hitbox.Part part, Health owner,
                             Material material, Vector3 jointPosition, Vector3 size)
    {
        var pivot = new GameObject(name + "Pivot");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = jointPosition;

        var limb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        limb.name = name;
        limb.transform.SetParent(pivot.transform, false);
        limb.transform.localPosition = new Vector3(0f, -size.y * 0.5f, 0f);
        limb.transform.localScale = size;

        if (material != null) limb.GetComponent<Renderer>().sharedMaterial = material;

        ConfigureHitbox(limb, part, owner);
        return pivot.transform;
    }

    static void ConfigureHitbox(GameObject go, Hitbox.Part part, Health owner)
    {
        // Triggers, so body parts never push the character around or block movement.
        var collider = go.GetComponent<BoxCollider>();
        collider.isTrigger = true;

        var hitbox = go.AddComponent<Hitbox>();
        hitbox.part = part;
        hitbox.owner = owner;

        int layer = GameLayers.Hitbox;
        if (layer >= 0) go.layer = layer;
    }
}
