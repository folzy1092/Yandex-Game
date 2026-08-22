using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// C# side of the Yandex Games advertising bridge.
///
/// Creates itself on game start and survives scene loads, so any scene can ask
/// for an ad without wiring anything up. Outside a WebGL build — in the editor,
/// or on a local web server where /sdk.js does not exist — every call reports
/// "no ad shown" immediately, so the game stays fully playable.
///
/// Yandex requires the game to be paused and silent while an ad is on screen;
/// that is handled here rather than by each caller.
/// </summary>
public class YandexAds : MonoBehaviour
{
    public const string ReceiverName = "YandexAds";

    public static YandexAds Instance { get; private set; }

    /// <summary>True once the SDK has initialised. False in the editor and outside Yandex Games.</summary>
    public static bool IsAvailable { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void YandexInitSDK();
    [DllImport("__Internal")] private static extern void YandexGameReady();
    [DllImport("__Internal")] private static extern void YandexShowFullscreen();
    [DllImport("__Internal")] private static extern void YandexShowRewarded();
#endif

    Action<bool> fullscreenCallback;
    Action<bool> rewardedCallback;
    bool rewardGranted;
    float savedTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject(ReceiverName);
        Instance = go.AddComponent<YandexAds>();
        DontDestroyOnLoad(go);

#if UNITY_WEBGL && !UNITY_EDITOR
        YandexInitSDK();
#endif
    }

    /// <summary>
    /// Call once the first playable scene is up. Yandex uses it to hide its
    /// loading screen and to start counting the session.
    /// </summary>
    public static void NotifyGameReady()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        YandexGameReady();
#endif
    }

    /// <summary>
    /// Shows an interstitial. The platform decides whether one actually appears,
    /// so <paramref name="onClosed"/> may be called with false straight away.
    /// Never gate progress on the result.
    /// </summary>
    public static void ShowFullscreen(Action<bool> onClosed = null)
    {
        if (Instance == null)
        {
            if (onClosed != null) onClosed(false);
            return;
        }

        Instance.RequestFullscreen(onClosed);
    }

    /// <summary>
    /// Shows a rewarded video. <paramref name="onFinished"/> receives true only
    /// if the player actually watched it through.
    /// </summary>
    public static void ShowRewarded(Action<bool> onFinished = null)
    {
        if (Instance == null)
        {
            if (onFinished != null) onFinished(false);
            return;
        }

        Instance.RequestRewarded(onFinished);
    }

    void RequestFullscreen(Action<bool> onClosed)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        fullscreenCallback = onClosed;
        YandexShowFullscreen();
#else
        if (onClosed != null) onClosed(false);
#endif
    }

    void RequestRewarded(Action<bool> onFinished)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        rewardedCallback = onFinished;
        rewardGranted = false;
        YandexShowRewarded();
#else
        if (onFinished != null) onFinished(false);
#endif
    }

    // ---------- called from JavaScript ----------

    public void OnSdkReady(string _)
    {
        IsAvailable = true;
    }

    public void OnSdkFailed(string reason)
    {
        IsAvailable = false;
        Debug.Log("Yandex ads unavailable: " + reason);
    }

    /// <summary>
    /// Requirement 2.14: the language comes from the SDK during startup, never
    /// mid-game. Applying it here is what turns the moderator's language check green.
    /// </summary>
    public void OnLanguageDetected(string languageCode)
    {
        Localization.SetFromCode(languageCode);
    }

    public void OnAdOpened(string _)
    {
        PauseGame(true);
    }

    public void OnFullscreenClosed(string wasShown)
    {
        PauseGame(false);

        Action<bool> callback = fullscreenCallback;
        fullscreenCallback = null;
        if (callback != null) callback(wasShown == "true");
    }

    public void OnRewardGranted(string _)
    {
        rewardGranted = true;
    }

    public void OnRewardedClosed(string wasShown)
    {
        PauseGame(false);

        Action<bool> callback = rewardedCallback;
        rewardedCallback = null;
        if (callback != null) callback(rewardGranted || wasShown == "true");
    }

    void PauseGame(bool paused)
    {
        if (paused)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            AudioListener.volume = 0f;
            FirstPersonController.LockCursor(false);
            return;
        }

        Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
        AudioListener.pause = false;
        AudioListener.volume = 1f;
    }
}
