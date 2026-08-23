using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the project layers the game needs and configures the physics matrix
/// for them.
///
/// Layers cannot be added from plain runtime code — they live in
/// ProjectSettings/TagManager.asset — so this edits that asset directly. It runs
/// as part of BUILD EVERYTHING, before anything that assigns a layer.
/// </summary>
public static class GameLayersSetup
{
    [MenuItem("Tools/Epic Battle 3D/0 - Create Layers")]
    public static void CreateLayers()
    {
        AddLayer(GameLayers.CharacterName);
        AddLayer(GameLayers.HitboxName);
        AddLayer(GameLayers.WeaponName);
        AddLayer(GameLayers.RagdollName);

        AssetDatabase.SaveAssets();
        ConfigureCollisions();

        Debug.Log("Epic Battle 3D: layers created (" + GameLayers.CharacterName + ", "
                  + GameLayers.HitboxName + ", " + GameLayers.WeaponName + ").");
    }

    static void AddLayer(string layerName)
    {
        if (LayerMask.NameToLayer(layerName) >= 0) return;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("Epic Battle 3D: could not open TagManager.asset, layers not created.");
            return;
        }

        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        // Slots 0-7 are reserved by Unity for its own built-in layers.
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;

            slot.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            return;
        }

        Debug.LogError("Epic Battle 3D: no free layer slots left for " + layerName + ".");
    }

    /// <summary>
    /// Hitboxes exist only to be hit by raycasts. Letting them take part in
    /// physics collisions as well would have every body part shoving its owner
    /// and the people around it.
    /// </summary>
    static void ConfigureCollisions()
    {
        int hitbox = GameLayers.Hitbox;
        int weapon = GameLayers.Weapon;
        int ragdoll = GameLayers.Ragdoll;
        int character = GameLayers.Character;
        if (hitbox < 0) return;

        for (int layer = 0; layer < 32; layer++)
        {
            Physics.IgnoreLayerCollision(hitbox, layer, true);
            if (weapon >= 0) Physics.IgnoreLayerCollision(weapon, layer, true);
        }

        // Corpses fall onto the level but pass through everything alive, so a
        // body can never shove a player or wedge a bot against a wall.
        if (ragdoll >= 0)
        {
            Physics.IgnoreLayerCollision(ragdoll, ragdoll, true);
            if (character >= 0) Physics.IgnoreLayerCollision(ragdoll, character, true);
            if (hitbox >= 0) Physics.IgnoreLayerCollision(ragdoll, hitbox, true);
            if (weapon >= 0) Physics.IgnoreLayerCollision(ragdoll, weapon, true);
        }
    }
}
