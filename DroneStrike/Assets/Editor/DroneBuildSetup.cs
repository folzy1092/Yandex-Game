using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One menu command that produces the whole playable game from an empty project.
/// </summary>
public static class DroneBuildSetup
{
    [MenuItem("Tools/Drone Strike/BUILD EVERYTHING", priority = -100)]
    public static void BuildEverything()
    {
        InputSettingsSetup.UseLegacyInput();

        DroneMaterials.Generate();
        IndustrialZoneBuilder.BuildScene();
        ApplyBuildSettings();

        if (!InputSettingsSetup.IsLegacyInputEnabled())
        {
            Debug.LogError("Drone Strike: the scene is built, but input handling still has to be "
                           + "switched. Set Edit > Project Settings > Player > Active Input Handling "
                           + "to \"Both\" and restart Unity.");
            return;
        }

        Debug.Log("Drone Strike: project ready. Open Assets/Scenes/IndustrialZone.unity and press Play. "
                  + "If input was just switched, restart Unity first.");
    }

    [MenuItem("Tools/Drone Strike/3 - Apply Build Settings")]
    public static void ApplyBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene("Assets/Scenes/IndustrialZone.unity", true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();

        // Yandex Games serves the plain uncompressed WebGL layout most reliably.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.runInBackground = true;

        Debug.Log("Drone Strike: build settings applied (WebGL, compression off).");
    }
}
