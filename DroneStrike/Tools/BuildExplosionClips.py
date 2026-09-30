"""Builds DroneStrike's nine explosion clips from CC0 field recordings.

Usage: download the five Freesound HQ previews below next to this script as
ex_<id>.mp3, run `python BuildExplosionClips.py`, copy the resulting
explosion_*.wav into Assets/Resources/Audio/Explosions/.

  182432 https://cdn.freesound.org/previews/182/182432_71257-hq.mp3   qubodup, "Explosive 1 v1 [DOD 130303]"
  182797 https://cdn.freesound.org/previews/182/182797_71257-hq.mp3   qubodup, "Windy Explosion"
  855898 https://cdn.freesound.org/previews/855/855898_71257-hq.mp3   qubodup, "Fire Explosion"
  840510 https://cdn.freesound.org/previews/840/840510_71257-hq.mp3   qubodup, "Loud Firewords Bang Cut Off"
  693421 https://cdn.freesound.org/previews/693/693421_15072041-hq.mp3 areniporgen, "CTS 7290" (flashbang)

All CC0. Needs numpy and soundfile (libsndfile >= 1.1 decodes MP3).
Processing: mono, trim to onset, resample-pitch, layer, add a short
low-passed outdoor tail, fade, peak-normalise.
"""
import hashlib
import numpy as np
import soundfile as sf

SR = 48000
rng = np.random.default_rng(20261001)


def load(n):
    x, sr = sf.read('ex_%s.mp3' % n)
    x = x if x.ndim == 1 else x.mean(1)
    assert sr == SR
    return x


def onset_trim(x, pre=0.01):
    env = np.abs(x)
    thresh = env.max() * 0.1
    i = int(np.argmax(env > thresh))
    return x[max(0, i - int(pre * SR)):]


def pitch(x, factor):
    """Resample: factor < 1 is deeper and longer."""
    idx = np.arange(0, len(x) - 1, factor)
    return np.interp(idx, np.arange(len(x)), x)


def lowpass(x, fc):
    a = np.exp(-2 * np.pi * fc / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def tail(x, seconds, decay, mix):
    """Outdoor reflections: convolve with decaying, low-passed noise."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-t / decay)
    ir[: int(0.02 * SR)] *= np.linspace(0, 1, int(0.02 * SR))
    ir = lowpass(ir, 1800.0)
    ir /= np.sqrt((ir ** 2).sum())
    wet = np.convolve(x, ir, mode="full")
    wet = np.concatenate([wet, np.zeros(1)])[: len(x) + n]
    dry = np.concatenate([x, np.zeros(n)])
    return dry + wet * mix


def fit(x, seconds, fade=0.35):
    n = int(seconds * SR)
    x = np.concatenate([x, np.zeros(max(0, n - len(x)))])[:n]
    f = int(fade * SR)
    x[-f:] *= np.linspace(1, 0, f) ** 2
    return x


def mix(*parts):
    n = max(len(p) for p, _, _ in parts)
    out = np.zeros(n)
    for p, gain, delay in parts:
        d = int(delay * SR)
        seg = p[: n - d]
        out[d: d + len(seg)] += seg * gain
    return out


def normalise(x, peak=0.95):
    return x / np.abs(x).max() * peak


dod = onset_trim(load('182432'))
windy = onset_trim(load('182797'))
fire = onset_trim(load('855898'))
bang = onset_trim(load('840510'))
flash = onset_trim(load('693421'))

clips = {
    # Compact: a sharp crack with a short body.
    'explosion_compact_01': fit(tail(mix((flash, 1.0, 0), (lowpass(fire, 900), 0.8, 0.005)), 1.2, 0.3, 0.5), 1.7),
    'explosion_compact_02': fit(tail(mix((bang, 1.0, 0), (lowpass(fire, 700), 0.5, 0.01)), 1.2, 0.3, 0.5), 1.7),
    'explosion_compact_03': fit(tail(mix((pitch(fire, 1.08), 1.0, 0), (pitch(flash, 0.9), 0.6, 0)), 1.2, 0.3, 0.5), 1.7),
    # Standard: the DOD detonation, a heavier thud under the crack.
    'explosion_standard_01': fit(tail(dod, 1.4, 0.4, 0.35), 3.0),
    'explosion_standard_02': fit(tail(mix((pitch(dod, 0.93), 1.0, 0), (fire, 0.6, 0)), 1.4, 0.4, 0.35), 3.0),
    'explosion_standard_03': fit(tail(mix((pitch(dod, 1.06), 1.0, 0), (flash, 0.45, 0)), 1.4, 0.4, 0.35), 3.0),
    # Heavy: the forced ammunition explosion, deeper and longer.
    'explosion_heavy_01': fit(tail(windy[: int(4.2 * SR)], 1.6, 0.5, 0.3), 4.6),
    'explosion_heavy_02': fit(tail(mix((pitch(dod, 0.8), 1.0, 0), (lowpass(fire, 1200), 0.7, 0)), 1.6, 0.5, 0.35), 4.2),
    'explosion_heavy_03': fit(tail(mix((pitch(windy[: int(4.6 * SR)], 0.9), 1.0, 0), (bang, 0.5, 0)), 1.6, 0.5, 0.3), 4.6),
}

for name, x in clips.items():
    y = normalise(x)
    path = name + '.wav'
    sf.write(path, y.astype(np.float32), SR, subtype='PCM_16')
    digest = hashlib.sha256(open(path, 'rb').read()).hexdigest()
    print(name, round(len(y) / SR, 2), 's', digest)
