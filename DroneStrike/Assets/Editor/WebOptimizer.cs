using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Release settings for the Yandex Games WebGL build, applied by code so they
/// cannot drift: this repo ships only Assets/, so anything set by hand in
/// Project Settings is lost the next time the project is recreated.
/// BUILD EVERYTHING calls <see cref="Apply"/>; <see cref="Report"/> logs the
/// values actually stored, read back after saving.
///
/// Choices specific to this game:
/// - Gzip + decompression fallback, not Brotli and not "Disabled". The loader
///   decompresses the gzip files itself when the host does not send
///   Content-Encoding, so the build runs on any static host (Yandex, itch, a
///   local python server) and still downloads a fraction of the raw size.
///   Brotli would be smaller, but without server headers its JS fallback is
///   slow on startup, and Brotli needs HTTPS.
/// - Exceptions: None. Game code has no try/catch; an uncaught exception in
///   a release WebGL build aborts either way.
/// - Quality: everything already targets a single level; shadows stay on —
///   they are what keeps vehicles from floating visually.
/// </summary>
public static class WebOptimizer
{
    [MenuItem("Tools/Drone Strike/Apply Web Release Settings")]
    public static void Apply()
    {
        NamedBuildTarget web = NamedBuildTarget.WebGL;

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.nameFilesAsHashes = false;

        PlayerSettings.stripEngineCode = true;
        PlayerSettings.stripUnusedMeshComponents = true;
        PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
        PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetApiCompatibilityLevel(web, ApiCompatibilityLevel.NET_Standard);

        // Browser paces frames with requestAnimationFrame.
        QualitySettings.vSyncCount = 0;

        // The Unity splash is optional since Unity 6 and its logo texture
        // alone was 2.7 MB of the data file; Yandex shows its own loader.
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        SetCodeOptimization();
        StripShaderVariants();
        ConfigureAudio();
        UseLegacyInputIfPackageGone();

        AssetDatabase.SaveAssets();
        Report();
    }

    /// <summary>Lives in the WebGL module, persists in Library/ (per machine), so it is re-applied every build.</summary>
    static void SetCodeOptimization()
    {
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
    }

    /// <summary>
    /// Graphics settings: let the build drop lightmap and fog variants the
    /// scenes do not use (no baked lightmaps; every map uses linear fog),
    /// and unused instancing / BatchRendererGroup variants.
    /// </summary>
    static void StripShaderVariants()
    {
        var graphics = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        Set(graphics, "m_LightmapStripping", 0);
        Set(graphics, "m_FogStripping", 0);
        Set(graphics, "m_InstancingStripping", 1);
        Set(graphics, "m_BrgStripping", 1);
        graphics.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(SerializedObject target, string property, int value)
    {
        SerializedProperty found = target.FindProperty(property);
        if (found != null) found.intValue = value;
        else Debug.LogWarning("WebOptimizer: graphics setting " + property + " not found in this Unity version.");
    }

    /// <summary>
    /// Every clip mono and Vorbis-compressed. Effects decompress on load (short,
    /// played often); loops and music stay compressed in memory / stream.
    /// The WAVs in the repo are uncompressed 48 kHz, which went into the
    /// build as raw PCM.
    /// </summary>
    static void ConfigureAudio()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            bool music = path.Contains("/Music/");
            bool loop = path.Contains("/Drone/");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.4f : 0.6f;
            settings.loadType = music ? AudioClipLoadType.Streaming
                              : loop ? AudioClipLoadType.CompressedInMemory
                              : AudioClipLoadType.DecompressOnLoad;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = music ? 44100u : 32000u;

            bool changed = !importer.forceToMono
                || importer.defaultSampleSettings.compressionFormat != settings.compressionFormat
                || importer.defaultSampleSettings.loadType != settings.loadType
                || importer.defaultSampleSettings.sampleRateOverride != settings.sampleRateOverride
                || !Mathf.Approximately(importer.defaultSampleSettings.quality, settings.quality);
            if (!changed) continue;

            importer.forceToMono = true;
            importer.loadInBackground = music;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// The game reads only the legacy Input Manager. Once the Input System
    /// package is removed, "Both" would make the player complain about a
    /// missing package, so the handler drops to legacy-only.
    /// </summary>
    static void UseLegacyInputIfPackageGone()
    {
        if (System.Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem") != null) return;
        Object settings = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/ProjectSettings.asset");
        if (settings == null) return;
        var serialized = new SerializedObject(settings);
        SerializedProperty handler = serialized.FindProperty("activeInputHandler");
        if (handler == null || handler.intValue == 0) return;
        handler.intValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Drone Strike/Report Web Settings")]
    public static void Report()
    {
        NamedBuildTarget web = NamedBuildTarget.WebGL;
        var lines = new List<string>
        {
            "activeBuildTarget=" + EditorUserBuildSettings.activeBuildTarget,
            "compressionFormat=" + PlayerSettings.WebGL.compressionFormat,
            "decompressionFallback=" + PlayerSettings.WebGL.decompressionFallback,
            "stripEngineCode=" + PlayerSettings.stripEngineCode,
            "stripUnusedMeshComponents=" + PlayerSettings.stripUnusedMeshComponents,
            "managedStrippingLevel(WebGL)=" + PlayerSettings.GetManagedStrippingLevel(web),
            "il2cppCodeGeneration(WebGL)=" + PlayerSettings.GetIl2CppCodeGeneration(web),
            "apiCompatibilityLevel(WebGL)=" + PlayerSettings.GetApiCompatibilityLevel(web),
            "exceptionSupport=" + PlayerSettings.WebGL.exceptionSupport,
            "debugSymbolMode=" + PlayerSettings.WebGL.debugSymbolMode,
            "dataCaching=" + PlayerSettings.WebGL.dataCaching,
            "initialMemorySize=" + PlayerSettings.WebGL.initialMemorySize,
            "maximumMemorySize=" + PlayerSettings.WebGL.maximumMemorySize,
            "memoryGrowthMode=" + PlayerSettings.WebGL.memoryGrowthMode,
            "codeOptimization=" + UnityEditor.WebGL.UserBuildSettings.codeOptimization,
            "targetFrameRate=" + Application.targetFrameRate,
            "vSyncCount=" + QualitySettings.vSyncCount,
            "splashScreen=" + PlayerSettings.SplashScreen.show
        };
        var graphics = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        foreach (string p in new[] { "m_LightmapStripping", "m_FogStripping", "m_InstancingStripping", "m_BrgStripping" })
        {
            SerializedProperty prop = graphics.FindProperty(p);
            lines.Add(p + "=" + (prop != null ? prop.intValue.ToString() : "n/a"));
        }
        var text = new StringBuilder("DroneStrike web settings:\n");
        foreach (string line in lines) text.AppendLine("  " + line);
        Debug.Log(text.ToString());
    }
}
