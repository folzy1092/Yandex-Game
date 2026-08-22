using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the main menu scene. It holds nothing but a camera and the script that
/// draws the menu, because <see cref="MainMenuUI"/> creates its interface at runtime.
/// </summary>
public static class MainMenuBuilder
{
    [MenuItem("Tools/Epic Battle 3D/3 - Build Main Menu Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGO = new GameObject("MenuCamera");
        var camera = cameraGO.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
        cameraGO.AddComponent<AudioListener>();

        var menuGO = new GameObject("MainMenu");
        var menu = menuGO.AddComponent<MainMenuUI>();
        menu.arenaSceneName = "Pool";

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        Debug.Log("Epic Battle 3D: main menu saved to Assets/Scenes/MainMenu.unity");
    }
}
