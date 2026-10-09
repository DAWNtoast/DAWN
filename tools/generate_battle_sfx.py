"""Generate the game's battle-only sound effects.

The clips are deliberately short, mono, and procedural so the project does not
depend on an external sample pack.  The output is deterministic and can be
regenerated with CPython's standard library only.
"""

from __future__ import annotations

import hashlib
import math
import struct
import wave
from pathlib import Path


SAMPLE_RATE = 22_050
OUTPUT = Path(__file__).resolve().parents[1] / "UnityClient" / "Assets" / "_Project" / "Resources" / "Audio" / "BattleSfx"


def envelope(t: float, duration: float, attack: float = 0.004, release: float = 0.12) -> float:
    return min(1.0, t / max(attack, 1e-5)) * min(1.0, (duration - t) / max(release, 1e-5))


def tone(t: float, frequency: float, shape: str = "sine") -> float:
    phase = 2.0 * math.pi * frequency * t
    if shape == "brass":
        return math.tanh(1.35 * (math.sin(phase) + 0.25 * math.sin(phase * 2.0)))
    if shape == "bell":
        return math.sin(phase) + 0.35 * math.sin(phase * 2.01) + 0.16 * math.sin(phase * 3.93)
    if shape == "saw":
        return 2.0 * ((frequency * t) % 1.0) - 1.0
    return math.sin(phase)


def noise_sample(state: int) -> tuple[int, float]:
    state ^= (state << 13) & 0xFFFFFFFF
    state ^= state >> 17
    state ^= (state << 5) & 0xFFFFFFFF
    return state & 0xFFFFFFFF, ((state & 0xFFFFFFFF) / 0xFFFFFFFF) * 2.0 - 1.0


def synth(name: str, duration: float) -> list[int]:
    total = int(duration * SAMPLE_RATE)
    samples = [0.0] * total
    state = int(hashlib.sha1(name.encode("utf-8")).hexdigest()[:8], 16) or 1
    noise_state = state
    filtered_noise = 0.0

    def add_tone(start: float, length: float, freq: float, amp: float, shape: str = "sine") -> None:
        first = max(0, int(start * SAMPLE_RATE))
        last = min(total, int((start + length) * SAMPLE_RATE))
        for index in range(first, last):
            local = (index - first) / SAMPLE_RATE
            samples[index] += tone(local, freq, shape) * envelope(local, length, 0.003, min(0.2, length * 0.35)) * amp

    def add_sweep(start: float, length: float, low: float, high: float, amp: float) -> None:
        first = max(0, int(start * SAMPLE_RATE))
        last = min(total, int((start + length) * SAMPLE_RATE))
        phase = 0.0
        for index in range(first, last):
            local = (index - first) / SAMPLE_RATE
            ratio = min(1.0, local / max(length, 1e-5))
            freq = low + (high - low) * ratio
            phase += 2.0 * math.pi * freq / SAMPLE_RATE
            samples[index] += math.tanh(1.2 * math.sin(phase)) * math.sin(math.pi * ratio) * amp

    def add_noise(start: float, length: float, amp: float) -> None:
        nonlocal noise_state, filtered_noise
        first = max(0, int(start * SAMPLE_RATE))
        last = min(total, int((start + length) * SAMPLE_RATE))
        for index in range(first, last):
            noise_state, raw = noise_sample(noise_state)
            filtered_noise = filtered_noise * 0.66 + raw * 0.34
            local = (index - first) / SAMPLE_RATE
            ratio = min(1.0, local / max(length, 1e-5))
            samples[index] += filtered_noise * math.sin(math.pi * ratio) * amp

    if name == "battle_start":
        add_tone(0.00, 0.75, 146.83, 0.34, "brass")
        add_tone(0.15, 0.85, 220.00, 0.27, "brass")
        add_noise(0.00, 0.20, 0.18)
        add_noise(0.34, 0.20, 0.16)
    elif name == "battle_card_play":
        add_noise(0.00, 0.24, 0.16)
        add_sweep(0.02, 0.34, 360.0, 880.0, 0.18)
    elif name == "battle_enemy_intent":
        add_tone(0.00, 0.48, 659.25, 0.22, "bell")
        add_tone(0.08, 0.42, 987.77, 0.14, "bell")
    elif name == "battle_cannon_fire":
        add_sweep(0.00, 0.46, 128.0, 42.0, 0.43)
        add_noise(0.00, 0.48, 0.42)
    elif name == "battle_impact":
        add_noise(0.00, 0.40, 0.38)
        add_tone(0.00, 0.38, 74.0, 0.34, "brass")
        add_tone(0.035, 0.44, 1_180.0, 0.11, "bell")
    elif name == "battle_shield":
        add_sweep(0.00, 0.52, 330.0, 990.0, 0.17)
        add_tone(0.08, 0.46, 659.25, 0.12)
    elif name == "battle_repair":
        add_tone(0.00, 0.30, 523.25, 0.16, "bell")
        add_tone(0.11, 0.34, 659.25, 0.16, "bell")
        add_tone(0.22, 0.38, 783.99, 0.16, "bell")
    elif name == "battle_victory":
        for offset, frequency in enumerate((261.63, 329.63, 392.00, 523.25)):
            add_tone(offset * 0.24, 0.72 if offset == 3 else 0.36, frequency, 0.22, "brass")
    elif name == "battle_defeat":
        add_sweep(0.00, 1.10, 132.0, 36.0, 0.31)
        add_noise(0.00, 1.10, 0.18)
    else:
        raise ValueError(name)

    peak = max(1e-6, max(abs(sample) for sample in samples))
    gain = min(1.0, 0.88 / peak)
    return [max(-32767, min(32767, int(round(sample * gain * 32767.0)))) for sample in samples]


def write_wav(path: Path, samples: list[int]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(b"".join(struct.pack("<h", sample) for sample in samples))


def write_meta(path: Path, guid: str) -> None:
    path.with_suffix(path.suffix + ".meta").write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "AudioImporter:\n"
        "  serializedVersion: 8\n"
        "  audioClip:\n"
        "    serializedVersion: 2\n"
        "    loadType: 0\n"
        "    sampleRateSetting: 0\n"
        "    sampleRateOverride: 0\n"
        "    compressionFormat: 2\n"
        "    quality: 0.82\n"
        "    conversionMode: 0\n"
        "  forceToMono: 1\n"
        "  normalize: 1\n"
        "  preloadAudioData: 1\n"
        "  loadInBackground: 0\n"
        "  ambisonic: 0\n"
        "  3D: 0\n"
        "  loopable: 0\n",
        encoding="utf-8",
    )


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    folder_hash = hashlib.sha1(str(OUTPUT).encode("utf-8")).hexdigest()[:32]
    (OUTPUT / "BattleSfx.meta").write_text(
        "fileFormatVersion: 2\n"
        f"guid: {folder_hash}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n",
        encoding="utf-8",
    )
    durations = {
        "battle_start": 1.10,
        "battle_card_play": 0.42,
        "battle_enemy_intent": 0.62,
        "battle_cannon_fire": 0.55,
        "battle_impact": 0.50,
        "battle_shield": 0.58,
        "battle_repair": 0.70,
        "battle_victory": 1.45,
        "battle_defeat": 1.25,
    }
    for name, duration in durations.items():
        wav = OUTPUT / f"{name}.wav"
        write_wav(wav, synth(name, duration))
        write_meta(wav, hashlib.sha1(name.encode("utf-8")).hexdigest()[:32])


if __name__ == "__main__":
    main()
