// Run with: node Tools/TestYandexBridge.js
// Exercises the actual WebGL .jslib against a mocked Yandex SDK.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Assets/Plugins/WebGL/YandexGames.jslib'), 'utf8');

function bridge() {
  const messages = [];
  let fullscreen;
  let rewarded;
  const window = {
    __yandexSend: (method, value) => messages.push([method, value]),
    ysdk: { adv: {
      showFullscreenAdv: ({ callbacks }) => { fullscreen = callbacks; },
      showRewardedVideo: ({ callbacks }) => { rewarded = callbacks; }
    } }
  };
  const state = { ready: true };
  const library = {};
  vm.runInNewContext(source, {
    LibraryManager: { library },
    mergeInto: Object.assign,
    window,
    yandexState: state,
    console: { warn: () => {} }
  });
  return { library, messages, get fullscreen() { return fullscreen; }, get rewarded() { return rewarded; }, state };
}

{
  const b = bridge();
  b.library.YandexShowRewarded();
  b.rewarded.onOpen();
  b.rewarded.onRewarded();
  b.rewarded.onRewarded();
  b.rewarded.onClose(true);
  b.rewarded.onClose(true);
  assert.deepEqual(b.messages, [
    ['OnAdOpened', ''], ['OnRewardGranted', ''], ['OnRewardedClosed', 'true']
  ]);
}

{
  const b = bridge();
  b.library.YandexShowRewarded();
  b.rewarded.onError(new Error('no fill'));
  b.rewarded.onClose(false);
  b.rewarded.onRewarded();
  assert.deepEqual(b.messages, [['OnRewardedClosed', 'false']]);
}

{
  const b = bridge();
  b.library.YandexShowFullscreen();
  b.fullscreen.onOpen();
  b.fullscreen.onClose(true);
  b.fullscreen.onClose(false);
  assert.deepEqual(b.messages, [['OnAdOpened', ''], ['OnFullscreenClosed', 'true']]);
}

{
  const b = bridge();
  b.state.ready = false;
  b.library.YandexShowRewarded();
  b.library.YandexShowFullscreen();
  assert.deepEqual(b.messages, [
    ['OnRewardedClosed', 'false'], ['OnFullscreenClosed', 'false']
  ]);
}

console.log('Yandex bridge callbacks: passed');
