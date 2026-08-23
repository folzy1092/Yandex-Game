using UnityEditor;
using UnityEngine;

/// <summary>
/// Switches the project to legacy input handling.
///
/// A fresh Unity 6 project defaults to the Input System package, and every
/// UnityEngine.Input call then throws at runtime — which is exactly what the
/// game is built on. This flips the setting from code so a new project does not
/// have to be fixed by hand before it will run.
///
/// The value lives in ProjectSettings.asset with no public API, so it is edited
/// through SerializedObject. Unity only picks the change up after a restart.
/// </summary>
public static class InputSettingsSetup
{
    const string PropertyName = "activeInputHandler";

    /// <summary>0 = legacy Input Manager, 1 = Input System package, 2 = both.</summary>
    const int Both = 2;

    [MenuItem("Tools/Drone Strike/0 - Use Legacy Input")]
    public static void UseLegacyInput()
    {
        SerializedProperty handler = FindHandlerProperty();

        if (handler == null)
        {
            Debug.LogError("Drone Strike: could not read " + PropertyName + " from "
                           + "ProjectSettings.asset. Set Edit > Project Settings > Player > "
                           + "Active Input Handling to \"Both\" by hand.");
            return;
        }

        // 0 is legacy only, 2 is both; either one means UnityEngine.Input works.
        if (handler.intValue == 0 || handler.intValue == Both)
        {
            Debug.Log("Drone Strike: input handling already includes the legacy Input Manager.");
            return;
        }

        handler.intValue = Both;
        handler.serializedObject.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Debug.LogWarning("Drone Strike: input handling set to \"Both\". "
                         + "RESTART UNITY for it to take effect — until then every input call still throws.");
    }

    /// <summary>True when UnityEngine.Input will actually work at runtime.</summary>
    public static bool IsLegacyInputEnabled()
    {
        SerializedProperty handler = FindHandlerProperty();
        return handler != null && (handler.intValue == 0 || handler.intValue == Both);
    }

    static SerializedProperty FindHandlerProperty()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) return null;

        var settings = new SerializedObject(assets[0]);
        return settings.FindProperty(PropertyName);
    }
}
