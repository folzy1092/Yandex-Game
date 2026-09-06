// Runs without Unity: node DroneStrike/Tests/yandex-bridge.test.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Assets/Plugins/WebGL/YandexGames.jslib'), 'utf8');

function bridge(sdk) {
    const messages = [];
    const context = vm.createContext({
        LibraryManager: { library: {} }, mergeInto: Object.assign,
        window: { location: { search: '' } }, navigator: { language: 'ru' }, URLSearchParams,
        console: { log() {}, warn() {} },
        SendMessage: (_, method, value) => messages.push([method, value]),
        YaGames: { init: () => Promise.resolve(sdk) }
    });
    vm.runInContext(source, context);
    const lib = context.LibraryManager.library;
    context.yandexState = lib.$yandexState;
    context.yandexNotifyReady = lib.$yandexNotifyReady;
    return { lib, context, messages, async init() { lib.YandexInitSDK(); await new Promise(setImmediate); } };
}

test('ready requested before asynchronous SDK init is delivered once', async () => {
    let ready = 0;
    const b = bridge({ features: { LoadingAPI: { ready: () => ready++ } } });
    b.lib.YandexGameReady();
    assert.equal(ready, 0);
    await b.init();
    b.lib.YandexGameReady();
    assert.equal(ready, 1);
});

test('ready requested after SDK init is delivered once', async () => {
    let ready = 0;
    const b = bridge({ features: { LoadingAPI: { ready: () => ready++ } } });
    await b.init();
    assert.equal(ready, 0);
    b.lib.YandexGameReady(); b.lib.YandexGameReady();
    assert.equal(ready, 1);
});

test('no SDK resolves both placements without blocking play', () => {
    const b = bridge({});
    b.context.window.__yandexSend = (m, v) => b.messages.push([m, v]);
    b.lib.YandexShowFullscreen(); b.lib.YandexShowRewarded();
    assert.deepEqual(b.messages, [['OnFullscreenClosed', 'false'], ['OnRewardedClosed', 'false']]);
});

test('synchronous SDK exceptions resolve each request', async () => {
    const fail = () => { throw Error('unavailable'); };
    const b = bridge({ adv: { showFullscreenAdv: fail, showRewardedVideo: fail } });
    await b.init(); b.messages.length = 0;
    b.lib.YandexShowFullscreen(); b.lib.YandexShowRewarded();
    assert.deepEqual(b.messages, [['OnFullscreenClosed', 'false'], ['OnRewardedClosed', 'false']]);
});

test('fullscreen error followed by close cannot resolve twice', async () => {
    let cb;
    const b = bridge({ adv: { showFullscreenAdv: ({ callbacks }) => { cb = callbacks; } } });
    await b.init(); b.messages.length = 0;
    b.lib.YandexShowFullscreen(); cb.onError('no fill'); cb.onClose(true); cb.onOpen();
    assert.deepEqual(b.messages, [['OnFullscreenClosed', 'false']]);
});

test('reward is granted once and resolves only after closing', async () => {
    let cb;
    const b = bridge({ adv: { showRewardedVideo: ({ callbacks }) => { cb = callbacks; } } });
    await b.init(); b.messages.length = 0;
    b.lib.YandexShowRewarded(); cb.onOpen(); cb.onRewarded(); cb.onRewarded();
    assert.deepEqual(b.messages, [['OnAdOpened', ''], ['OnRewardGranted', '']]);
    cb.onClose(); cb.onError('late');
    assert.deepEqual(b.messages.at(-1), ['OnRewardedClosed', 'true']);
    assert.equal(b.messages.length, 3);
});

test('closing early cannot turn into a reward from a late callback', async () => {
    let cb;
    const b = bridge({ adv: { showRewardedVideo: ({ callbacks }) => { cb = callbacks; } } });
    await b.init(); b.messages.length = 0;
    b.lib.YandexShowRewarded(); cb.onClose(); cb.onRewarded(); cb.onClose();
    assert.deepEqual(b.messages, [['OnRewardedClosed', 'false']]);
});

test('a late close from a previous request cannot close the next ad', async () => {
    const callbacks = [];
    const b = bridge({ adv: { showRewardedVideo: (x) => callbacks.push(x.callbacks) } });
    await b.init(); b.messages.length = 0;
    b.lib.YandexShowRewarded(); callbacks[0].onClose();
    b.lib.YandexShowRewarded(); callbacks[0].onClose();
    callbacks[1].onRewarded(); callbacks[1].onClose();
    assert.deepEqual(b.messages, [['OnRewardedClosed', 'false'], ['OnRewardGranted', ''], ['OnRewardedClosed', 'true']]);
});
