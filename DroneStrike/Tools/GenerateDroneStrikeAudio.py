#!/usr/bin/env python3
"""Build original WAV assets used by DroneStrike's WebGL audio pass.

The generator runs offline. The game only imports the resulting files and does
not allocate raw sample buffers or synthesize rotor/explosion sounds at runtime.
All generated output is original project audio; the separately imported Kenney
clips are tracked in Assets/Resources/Audio/SOURCE_MANIFEST.md.
"""

from __future__ import annotations

import math
import random
import wave
from pathlib import Path

import numpy as np


RATE = 44100
TWO_PI = math.tau


def write_wav(path: Path, samples: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = (np.clip(samples, -0.98, 0.98) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm.tobytes())


def periodic_noise(t: np.ndarray, duration: float, seed: int, partials: int,
                   low: int, high: int) -> np.ndarray:
    """Loop-safe noise made from integer-cycle sine partials, vectorized."""
    rng = random.Random(seed)
    value = np.zeros_like(t)
    for _ in range(partials):
        cycles = rng.randint(low, high)
        phase = rng.random() * TWO_PI
        gain = rng.uniform(0.15, 1.0) / partials
        value += np.sin(TWO_PI * cycles * t / duration + phase) * gain
    return value


def motor_loop(duration: float, base_hz: float, seed: int) -> np.ndarray:
    rng = random.Random(seed)
    partial_phases = [rng.random() * TWO_PI for _ in range(7)]
    partial_gains = [0.34, 0.18, 0.13, 0.09, 0.06, 0.035, 0.02]
    # Integer-cycle modulation keeps the start and end of the loop compatible.
    mod_hz = 3.0
    blade_hz = 57.0
    total = round(duration * RATE)
    t = np.arange(total, dtype=np.float64) / RATE
    wobble = np.sin(TWO_PI * mod_hz * t) * 0.014
    pulse = 0.84 + 0.16 * np.sin(TWO_PI * blade_hz * t + 0.35)
    tone = np.zeros_like(t)
    for index, gain in enumerate(partial_gains, start=1):
        harmonic = index if index < 5 else index + 1
        frequency = base_hz * harmonic
        phase = TWO_PI * frequency * t + wobble * harmonic + partial_phases[index - 1]
        tone += np.sin(phase) * gain
    air = periodic_noise(t, duration, seed + 400, 24, 120, 1100) * 0.28
    edge = periodic_noise(t, duration, seed + 800, 14, 1200, 7200) * 0.10
    return (tone * pulse + air + edge) * 0.70


def wind_loop(duration: float, seed: int, colour: float) -> np.ndarray:
    total = round(duration * RATE)
    t = np.arange(total, dtype=np.float64) / RATE
    gust = 0.62 + 0.25 * np.sin(TWO_PI * 0.25 * t + 0.4)
    gust += 0.13 * np.sin(TWO_PI * 0.75 * t + 1.2)
    low = periodic_noise(t, duration, seed, 30, 2, 220) * 0.55
    high = periodic_noise(t, duration, seed + 100, 34, 300, 7800) * colour
    return (low + high) * gust * 0.34


def explosion(duration: float, seed: int, weight: float) -> np.ndarray:
    rng = np.random.default_rng(seed)
    total = round(duration * RATE)
    t = np.arange(total, dtype=np.float64) / RATE
    u = t / duration
    white = rng.uniform(-1.0, 1.0, total)
    noise_state = np.convolve(white, np.ones(15) / 15.0, mode="same")
    crack = white * np.exp(-t * 40.0) * 0.27
    debris = periodic_noise(t, duration, seed + 500, 28, 40, 9000)
    debris *= np.exp(-t * (4.5 / weight)) * 0.28
    sweep = 118.0 / weight * (1.0 - 0.62 * np.minimum(1.0, u))
    body = np.sin(TWO_PI * sweep * t) * np.exp(-t * (5.4 / weight)) * 0.62
    sub = np.sin(TWO_PI * (48.0 / weight) * t) * np.exp(-t * (3.1 / weight)) * 0.45
    smoke = noise_state * np.exp(-t * (2.4 / weight)) * 0.44
    ring = np.zeros_like(t)
    for multiplier, gain in ((1.0, 0.08), (1.41, 0.055), (2.07, 0.035)):
        ring += np.sin(TWO_PI * (410.0 * multiplier) * t + seed * 0.1) * gain
    ring *= np.exp(-t * 9.0)
    attack = np.minimum(1.0, t * 220.0)
    return (crack + debris + body + sub + smoke + ring) * attack * 0.78


def alert(duration: float, low_hz: float, high_hz: float) -> np.ndarray:
    total = round(duration * RATE)
    t = np.arange(total, dtype=np.float64) / RATE
    frequency = np.where(t < duration * 0.45, low_hz, high_hz)
    envelope = np.minimum(1.0, t * 70.0) * np.exp(-t * 7.0)
    return np.sin(TWO_PI * frequency * t) * envelope * 0.34


def main() -> None:
    root = Path(__file__).resolve().parents[1] / "Assets" / "Resources" / "Audio"
    write_wav(root / "Drone" / "motor_idle.wav", motor_loop(6.0, 124.0, 1101))
    write_wav(root / "Drone" / "motor_cruise.wav", motor_loop(6.0, 178.0, 1102))
    write_wav(root / "Drone" / "motor_load.wav", motor_loop(6.0, 236.0, 1103))
    write_wav(root / "Drone" / "wind_low.wav", wind_loop(6.0, 1201, 0.23))
    write_wav(root / "Drone" / "wind_high.wav", wind_loop(6.0, 1202, 0.42))

    for name, duration, weight, seed in (
        ("compact", 1.00, 0.82, 2101),
        ("standard", 1.45, 1.00, 2201),
        ("heavy", 2.05, 1.28, 2301),
    ):
        for variant in range(1, 4):
            write_wav(root / "Explosions" / f"explosion_{name}_{variant:02}.wav",
                      explosion(duration, seed + variant, weight))

    write_wav(root / "UI" / "signal_weak.wav", alert(0.28, 510.0, 690.0))
    write_wav(root / "UI" / "battery_low.wav", alert(0.36, 760.0, 540.0))
    print(f"Generated DroneStrike audio in {root}")


if __name__ == "__main__":
    main()
