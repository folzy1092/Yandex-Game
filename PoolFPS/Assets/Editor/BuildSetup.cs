using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One menu command that produces the entire playable game from an empty project:
/// materials, both scenes, and the scene list used by the player build.
/// </summary>
public static class BuildSetup
{
    [MenuItem("Tools/Pool FPS/BUILD EVERYTHING", priority = -100)]
    public static void BuildEverything()
    {
        GeneratedMaterials.Generate();
        PoolMapBuilder.BuildScene();
        MainMenuBuilder.BuildScene();
        ApplyBuildSettings();

        Debug.Log("Pool FPS: project ready. Open Assets/Scenes/MainMenu.unity and press Play.");
    }

    [MenuItem("Tools/Pool FPS/4 - Apply Build Settings")]
    public static void ApplyBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            // The menu must come first — it is the scene the game starts on.
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Pool.unity", true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();

        // Yandex Games runs the build in a browser, so WebGL is the only target
        // that matters. Compression must be off or set to a format the host can
        // serve; Yandex serves the plain uncompressed layout most reliably.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.runInBackground = true;

        Debug.Log("Pool FPS: build settings applied (MainMenu -> Pool, WebGL compression off).");
    }
}
