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
        DroneMaterials.Generate();
        IndustrialZoneBuilder.BuildScene();
        ApplyBuildSettings();

        Debug.Log("Drone Strike: project ready. Open Assets/Scenes/IndustrialZone.unity and press Play.");
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
