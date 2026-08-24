using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the menu scene. It holds nothing but a camera and the one component
/// that assembles the interface at runtime, so the whole screen still lives in
/// code rather than in a scene file nobody can review.
/// </summary>
public static class MainMenuBuilder
{
    [MenuItem("Tools/Drone Strike/2b - Build Main Menu")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // A camera is required even for a screen-space overlay canvas: without
        // one Unity renders nothing behind the UI and the audio listener
        // warnings start.
        var cameraGO = new GameObject("MenuCamera");
        var camera = cameraGO.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.07f, 0.065f);
        cameraGO.AddComponent<AudioListener>();

        var menu = new GameObject("MainMenu");
        menu.AddComponent<MainMenuUI>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        Debug.Log("Drone Strike: menu saved to Assets/Scenes/MainMenu.unity");
    }
}
