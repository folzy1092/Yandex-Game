// Bridge between Unity's C# code and the Yandex Games JavaScript SDK.
//
// The SDK script is injected at runtime rather than through a custom WebGL
// template. That keeps the project working with any Unity version, because a
// custom template has to match the exact loader markup of the Unity release
// that generated it.
//
// Everything degrades quietly when the SDK is absent (running the build from a
// local web server, for example): calls simply report "no ad was shown".

mergeInto(LibraryManager.library, {

  // Name of the GameObject that receives the callbacks. Must match the object
  // created by YandexAds.cs.
  $yandexState: {
    ready: false,
    gameReadyRequested: false,
    gameReadySent: false,
    receiver: 'YandexAds'
  },

  YandexSendToUnity__deps: ['$yandexState'],
  YandexSendToUnity: function () {},

  YandexInitSDK__deps: ['$yandexState', '$yandexNotifyReady'],
  YandexInitSDK: function () {
    var send = function (method, value) {
      try {
        if (typeof SendMessage === 'function') {
          SendMessage(yandexState.receiver, method, value || '');
        } else if (window.unityInstance && window.unityInstance.SendMessage) {
          window.unityInstance.SendMessage(yandexState.receiver, method, value || '');
        }
      } catch (e) {
        console.warn('Yandex SDK: could not deliver ' + method + ' to Unity', e);
      }
    };

    window.__yandexSend = send;

    // Requirement 2.14: the language has to be detected during startup. It is
    // reported the moment the SDK answers, before the menu is drawn. When the
    // SDK is unreachable the browser language stands in, so the game is never
    // left without a language.
    var reportLanguage = function (code) {
      send('OnLanguageDetected', code || '');
    };

    var queryLanguage = function () {
      try {
        var params = new URLSearchParams(window.location.search);
        return (params.get('lang') || '').trim();
      } catch (e) {
        return '';
      }
    };

    var browserLanguage = function () {
      return (navigator.language || navigator.userLanguage || 'en').slice(0, 2);
    };

    var fallbackLanguage = function () {
      return queryLanguage() || browserLanguage();
    };

    var start = function () {
      if (typeof YaGames === 'undefined') {
        console.warn('Yandex SDK: YaGames is not defined, ads are disabled.');
        reportLanguage(fallbackLanguage());
        send('OnSdkFailed', 'YaGames missing');
        return;
      }

      YaGames.init().then(function (sdk) {
        window.ysdk = sdk;
        yandexState.ready = true;
        if (yandexState.gameReadyRequested) yandexNotifyReady();

        var language = browserLanguage();
        try {
          if (sdk.environment && sdk.environment.i18n && sdk.environment.i18n.lang) {
            language = sdk.environment.i18n.lang;
          }
        } catch (e) {
          console.warn('Yandex SDK: could not read environment.i18n.lang', e);
        }

        console.log('Yandex SDK: initialised, language = ' + language);
        reportLanguage(language);
        send('OnSdkReady', '');
      }).catch(function (error) {
        console.warn('Yandex SDK: init failed', error);
        reportLanguage(fallbackLanguage());
        send('OnSdkFailed', String(error));
      });
    };

    if (typeof YaGames !== 'undefined') {
      start();
      return;
    }

    var script = document.createElement('script');
    script.src = '/sdk.js';
    script.async = true;
    script.onload = start;
    script.onerror = function () {
      console.warn('Yandex SDK: /sdk.js could not be loaded. This is expected outside Yandex Games.');
      reportLanguage(fallbackLanguage());
      send('OnSdkFailed', 'sdk.js not reachable');
    };
    document.head.appendChild(script);
  },

  // Tells the platform the game has finished loading. Yandex uses this to stop
  // the loading indicator and to decide when ads may start.
  $yandexNotifyReady__deps: ['$yandexState'],
  $yandexNotifyReady: function () {
    if (!yandexState.ready || !yandexState.gameReadyRequested || yandexState.gameReadySent || !window.ysdk) return;
    try {
      if (window.ysdk.features && window.ysdk.features.LoadingAPI) {
        window.ysdk.features.LoadingAPI.ready();
        yandexState.gameReadySent = true;
      }
    } catch (e) {
      console.warn('Yandex SDK: LoadingAPI.ready failed', e);
    }
  },

  YandexGameReady__deps: ['$yandexState', '$yandexNotifyReady'],
  YandexGameReady: function () {
    yandexState.gameReadyRequested = true;
    yandexNotifyReady();
  },

  YandexShowFullscreen__deps: ['$yandexState'],
  YandexShowFullscreen: function () {
    var send = window.__yandexSend || function () {};

    if (!yandexState.ready || !window.ysdk) {
      send('OnFullscreenClosed', 'false');
      return;
    }

    var closed = false;
    var finish = function (shown) {
      if (closed) return;
      closed = true;
      send('OnFullscreenClosed', shown ? 'true' : 'false');
    };
    try {
      window.ysdk.adv.showFullscreenAdv({
        callbacks: {
          onOpen: function () { if (!closed) send('OnAdOpened', ''); },
          onClose: finish,
          onError: function (error) {
            console.warn('Yandex SDK: fullscreen ad error', error);
            finish(false);
          }
        }
      });
    } catch (error) { finish(false); }
  },

  YandexShowRewarded__deps: ['$yandexState'],
  YandexShowRewarded: function () {
    var send = window.__yandexSend || function () {};
    if (!yandexState.ready || !window.ysdk) {
      send('OnRewardedClosed', 'false');
      return;
    }
    var rewarded = false;
    var closed = false;
    var finish = function () {
      if (closed) return;
      closed = true;
      send('OnRewardedClosed', rewarded ? 'true' : 'false');
    };
    try {
      window.ysdk.adv.showRewardedVideo({
        callbacks: {
          onOpen: function () { if (!closed) send('OnAdOpened', ''); },
          onRewarded: function () {
            if (closed || rewarded) return;
            rewarded = true;
            send('OnRewardGranted', '');
          },
          onClose: finish,
          onError: function (error) {
            console.warn('Yandex SDK: rewarded ad error', error);
            finish();
          }
        }
      });
    } catch (error) { finish(); }
  }
});
