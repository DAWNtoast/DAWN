"""Deterministic acoustic rendering, isolated from the unchanged score scheduler."""
from fractions import Fraction
from pathlib import Path
from importlib.metadata import version
import hashlib
import io
import json
import math
import re
import zipfile

import numpy as np
import pyloudnorm as pyln
from scipy import signal
import soundfile as sf

OUTPUT_RATE = 48000
TARGET_LUFS = -23.0
TRUE_PEAK_CEILING_DB = -6.05
PITCH_CLASS = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5,
               "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}


def sha256_json(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                    separators=(",", ":")).encode()).hexdigest()


def protected_score(score):
    events = []
    for event in score["events"]:
        row = {key: event[key] for key in ("type", "lane")}
        if event["type"] == "note":
            row["midi"] = event["midi"]
        row["start_seconds_hex"] = float(event["start_seconds"]).hex()
        row["duration_seconds_hex"] = float(event["duration_seconds"]).hex()
        events.append(row)
    return {key: score[key] for key in ("name", "bpm", "root", "scale", "bars",
                                        "original_sample_rate", "original_frames")} | {
        "nominal_seconds_hex": float(score["nominal_seconds"]).hex(), "events": events}


def midi_hz(midi):
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def measure_fundamental(data, sr, expected_midi):
    """Measure the fundamental itself, rather than a louder octave harmonic."""
    mono = data.mean(axis=1) if data.ndim == 2 else data
    expected = midi_hz(expected_midi)
    estimates = []
    for start in (.08, .2, .4, .7, 1.0):
        window = mono[int(start * sr):int((start + .45) * sr)]
        if len(window) < sr * .2:
            continue
        size = max(262144, 2 ** math.ceil(math.log2(len(window) * 4)))
        spectrum = np.abs(np.fft.rfft(window * np.hanning(len(window)), size))
        frequencies = np.fft.rfftfreq(size, 1.0 / sr)
        indices = np.flatnonzero((frequencies > expected * 2 ** (-.6 / 12)) &
                                 (frequencies < expected * 2 ** (.6 / 12)))
        peak = indices[np.argmax(spectrum[indices])]
        y = np.log(np.maximum(spectrum[peak - 1:peak + 2], 1.e-16))
        offset = .5 * (y[0] - y[2]) / (y[0] - 2 * y[1] + y[2])
        estimates.append((peak + float(np.clip(offset, -.5, .5))) * sr / size)
    measured = float(np.median(estimates))
    cents = 1200 * np.log2(measured / expected)
    if not np.isfinite(measured) or abs(cents) > 40:
        raise ValueError(f"Unexpected sample tuning for MIDI {expected_midi}: {cents:.1f} cents")
    return measured, [float(1200 * np.log2(value / expected)) for value in estimates]


class SampleBank:
    def __init__(self, root):
        self.root = Path(root)
        self.samples = {}
        self.cache = {}
        self.pitch_report = []
        manifest = json.loads((self.root / "manifest.json").read_text(encoding="utf-8"))
        for item in manifest:
            path = self.root / item["file"]
            if hashlib.sha256(path.read_bytes()).hexdigest() != item["sha256"]:
                raise ValueError(f"Source sample hash mismatch: {path}")
            data, sr = sf.read(path, dtype="float64", always_2d=True)
            match = re.search(r"_([A-G]#?)([0-9])_", path.name)
            # The pinned banks label middle C as C3 (MIDI 60), not scientific C4.
            root_midi = (int(match[2]) + 2) * 12 + PITCH_CLASS[match[1]]
            measured, windows = measure_fundamental(data, sr, root_midi)
            self.pitch_report.append(dict(file=item["file"], family=item["family"], root_midi=root_midi,
                expected_hz=midi_hz(root_midi), measured_hz=measured,
                cents=float(1200 * np.log2(measured / midi_hz(root_midi))),
                window_cents=windows, source_sha256=item["sha256"]))
            # Trim only leading silence; natural instrument attacks remain intact.
            level = np.max(np.abs(data), axis=1)
            active = np.flatnonzero(level > max(float(level.max()) * .0008, .000001))
            trim = max(0, int(active[0]) - int(sr * .003)) if len(active) else 0
            data = data[trim:]
            data -= data.mean(axis=0, keepdims=True)
            reference = data[int(sr * .04):min(len(data), int(sr * .8))]
            rms = float(np.sqrt(np.mean(reference ** 2)))
            gain = min(.14 / max(rms, .0001), .78 / max(float(np.max(np.abs(data))), .0001))
            data *= gain
            self.samples.setdefault(item["family"], []).append((root_midi, measured, sr, data, item["file"]))
        if {family: len(items) for family, items in self.samples.items()} != {
                "harp": 12, "cello": 6, "clarinet": 4, "bass": 4}:
            raise ValueError("Expected the pinned 26-sample acoustic palette")

    def pitched(self, family, midi):
        key = family, midi
        if key in self.cache:
            return self.cache[key]
        root_midi, measured, sr, data, filename = min(self.samples[family], key=lambda row: abs(row[0] - midi))
        # Correct measured tuning and transpose to exact original MIDI, not filename octaves.
        rate_ratio = (OUTPUT_RATE / sr) * (measured / midi_hz(midi))
        fraction = Fraction(rate_ratio).limit_denominator(2048)
        pitched = signal.resample_poly(data, fraction.numerator, fraction.denominator, axis=0)
        lowpass = {"harp": 4000, "cello": 2300, "clarinet": 3300, "bass": 1700}[family]
        highpass = min({"harp": 105, "cello": 78, "clarinet": 140, "bass": 30}[family], midi_hz(midi) * .6)
        pitched = signal.sosfilt(signal.butter(2, highpass, "highpass", fs=OUTPUT_RATE, output="sos"), pitched, axis=0)
        pitched = signal.sosfilt(signal.butter(3, lowpass, "lowpass", fs=OUTPUT_RATE, output="sos"), pitched, axis=0)
        mid = pitched.mean(axis=1)
        side = (pitched[:, 0] - pitched[:, -1]) * .17
        self.cache[key] = np.column_stack((mid + side, mid - side)).astype(np.float32)
        return self.cache[key]

    def note(self, family, midi, frames, lane):
        source = self.pitched(family, midi)
        result = np.zeros((frames, 2), dtype=np.float64)
        count = min(frames, len(source))
        result[:count] = source[:count]
        t = np.arange(frames) / OUTPUT_RATE
        duration = frames / OUTPUT_RATE
        attack = {"harp": .008, "cello": .085, "clarinet": .040, "bass": .008}[family]
        release = min(duration * .3, {"harp": .19, "cello": .30, "clarinet": .19, "bass": .16}[family])
        onset = np.clip(t / max(attack, 1 / OUTPUT_RATE), 0, 1)
        ending = np.clip((duration - t - 1 / OUTPUT_RATE) / max(release, 1 / OUTPUT_RATE), 0, 1)
        envelope = np.sin(onset * np.pi / 2) ** 2 * np.sin(ending * np.pi / 2) ** 2
        if family == "harp":
            envelope *= np.exp(-t * 1.25)
        elif family == "bass":
            envelope *= np.exp(-t * 1.0)
        elif family == "clarinet" and lane == "lead":
            envelope *= np.exp(-t * 2.5)
        result *= envelope[:, None]
        return result


def soft_drum(kind, frames, rng):
    t = np.arange(frames) / OUTPUT_RATE
    duration = frames / OUTPUT_RATE
    noise = rng.normal(0, 1, frames)
    if kind == "kick":
        phase = 2 * np.pi * (68 * t + 22 * (.020 * (1 - np.exp(-t / .020))))
        wave = .70 * np.sin(phase) * np.exp(-t * 22)
        wave += .10 * np.sin(2 * np.pi * 173 * t) * np.exp(-t * 50)
        attack, pan = .004, -.025
    elif kind == "snare":
        brush = signal.sosfilt(signal.butter(2, [240, 2400], "bandpass", fs=OUTPUT_RATE, output="sos"), noise)
        wave = .34 * brush * np.exp(-t * 27) + .10 * np.sin(2 * np.pi * 178 * t) * np.exp(-t * 33)
        attack, pan = .006, .04
    else:
        brush = signal.sosfilt(signal.butter(2, [850, 3400], "bandpass", fs=OUTPUT_RATE, output="sos"), noise)
        wave = .20 * brush * np.exp(-t * 48)
        attack, pan = .003, .08
    edge = np.sin(np.clip(t / attack, 0, 1) * np.pi / 2) ** 2
    edge *= np.sin(np.clip((duration - t - 1 / OUTPUT_RATE) / min(.025, duration * .35), 0, 1) * np.pi / 2) ** 2
    wave *= edge
    return np.column_stack((wave * (1 - pan), wave * (1 + pan)))


def true_peak(audio):
    return float(np.max(np.abs(signal.resample_poly(audio, 4, 1, axis=0))))


def metrics(audio, sr=OUTPUT_RATE):
    loudness = float(pyln.Meter(sr).integrated_loudness(audio))
    peak = true_peak(audio)
    frequencies, power = signal.welch(audio.mean(axis=1), sr, nperseg=8192)
    total = float(power.sum())
    return dict(lufs=loudness, true_peak_dbtp=float(20 * np.log10(max(peak, 1.e-12))),
        sample_peak_dbfs=float(20 * np.log10(max(float(np.max(np.abs(audio))), 1.e-12))),
        rms_dbfs=float(20 * np.log10(max(float(np.sqrt(np.mean(audio ** 2))), 1.e-12))),
        power_2k_6k_fraction=float(power[(frequencies >= 2000) & (frequencies < 6000)].sum() / total),
        power_above_6k_fraction=float(power[frequencies >= 6000].sum() / total),
        loop_boundary_step=float(np.max(np.abs(audio[-1] - audio[0]))))


def render_track(score, bank):
    frames = round(score["original_frames"] / score["original_sample_rate"] * OUTPUT_RATE)
    output = np.zeros((frames, 2), dtype=np.float64)
    rng = np.random.default_rng(score["seed"])
    for index, event in enumerate(score["events"]):
        start = max(0, int(event["start_seconds"] * OUTPUT_RATE))
        end = min(frames, int((event["start_seconds"] + event["duration_seconds"]) * OUTPUT_RATE))
        if end <= start:
            continue
        gate_frames = int((event["start_seconds"] + event["duration_seconds"]) * OUTPUT_RATE) - start
        count = end - start
        amp, lane = event["original_amplitude"], event["lane"]
        if event["type"] == "drum":
            voice = soft_drum(lane, gate_frames, rng) * amp * .65
        elif lane == "pad":
            voice = bank.note("cello", event["midi"], gate_frames, lane) * amp * .60
        elif lane.startswith("bass"):
            voice = bank.note("bass", event["midi"], gate_frames, lane) * amp
        elif lane == "lead":
            voice = bank.note("harp", event["midi"], gate_frames, lane) * amp * 2.80
            # Timbre doubling of the same MIDI/event only, never an extra melody note.
            if index % 4 == 0:
                voice += bank.note("clarinet", event["midi"], gate_frames, lane) * amp * .14
        else:
            voice = bank.note("clarinet", event["midi"], gate_frames, lane) * amp * .60
        output[start:end] += voice[:count]
    # Short room reflections, without rhythmic echoes or extra score events.
    room = signal.sosfilt(signal.butter(2, 1900, "lowpass", fs=OUTPUT_RATE, output="sos"), output, axis=0)
    for delay, amount in ((.019, .08), (.031, .045), (.043, .025)):
        offset = round(delay * OUTPUT_RATE)
        output[offset:] += room[:-offset, ::-1] * amount
    output = signal.sosfilt(signal.butter(2, 32, "highpass", fs=OUTPUT_RATE, output="sos"), output, axis=0)
    fade = round(.22 * OUTPUT_RATE)
    curve = np.sin(np.linspace(0, np.pi / 2, fade)) ** 2
    output[:fade] *= curve[:, None]
    output[-fade:] *= curve[::-1, None]
    before = float(pyln.Meter(OUTPUT_RATE).integrated_loudness(output))
    gain = 10 ** ((TARGET_LUFS - before) / 20)
    gain = min(gain, 10 ** (TRUE_PEAK_CEILING_DB / 20) / max(true_peak(output), 1.e-12))
    output *= gain
    if not np.isfinite(output).all():
        raise ValueError("Non-finite samples")
    return output, metrics(output)


def make_previews(name, new_audio, archive_path, destination):
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive_path) as archive:
        raw = archive.read(f"Assets/_Project/Resources/Audio/Music/{name}.wav")
    (destination / f"{name}__A_original.wav").write_bytes(raw)
    original, old_sr = sf.read(io.BytesIO(raw), always_2d=True)
    original = signal.resample_poly(original, OUTPUT_RATE, old_sr, axis=0)
    if original.shape[1] == 1:
        original = np.repeat(original, 2, axis=1)
    original = original[:len(new_audio)]
    if len(original) < len(new_audio):
        original = np.pad(original, ((0, len(new_audio) - len(original)), (0, 0)))
    meter = pyln.Meter(OUTPUT_RATE)
    old_lufs = float(meter.integrated_loudness(original))
    new_lufs = float(meter.integrated_loudness(new_audio))
    target = min(old_lufs, new_lufs)
    a = original * 10 ** ((target - old_lufs) / 20)
    b = new_audio * 10 ** ((target - new_lufs) / 20)
    for label, audio in (("B_acoustic_mix", new_audio), ("A_equal_loudness", a), ("B_equal_loudness", b)):
        sf.write(destination / f"{name}__{label}.wav", audio, OUTPUT_RATE, subtype="PCM_24")
    begin, length = round(2 * OUTPUT_RATE), round(12 * OUTPUT_RATE)
    comparison = np.concatenate((a[begin:begin + length], np.zeros((OUTPUT_RATE // 2, 2)), b[begin:begin + length]))
    sf.write(destination / f"{name}__AB_equal_12s.wav", comparison, OUTPUT_RATE, subtype="PCM_24")
    return dict(name=name, original_lufs=old_lufs, new_lufs=new_lufs,
                comparison_lufs_target=target, excerpt_start_seconds=2, excerpt_seconds=12)


def export_tracks(scores, bank, destination, report_dir, original_archive=None, previews_dir=None):
    destination, report_dir = Path(destination), Path(report_dir)
    destination.mkdir(parents=True, exist_ok=True)
    report_dir.mkdir(parents=True, exist_ok=True)
    reference = Path(__file__).parent / "AudioSamples" / "music-score-reference.json"
    references = {item["name"]: item for item in json.loads(reference.read_text())["tracks"]}
    report, event_report, previews = [], [], []
    for score in scores:
        protected = protected_score(score)
        computed = sha256_json(protected)
        # Recompute NEW scheduler events; reference hashes are comparison data only.
        if computed != references[score["name"]]["score_sha256"]:
            raise ValueError(f"Original score changed: {score['name']}")
        audio, measured = render_track(score, bank)
        path = destination / (score["name"] + ".wav")
        sf.write(path, audio, OUTPUT_RATE, subtype="PCM_24")
        measured.update(name=score["name"], file=str(path), frames=len(audio), sample_rate=OUTPUT_RATE,
            channels=2, subtype="PCM_24", score_sha256=computed,
            sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            original_seconds=score["original_frames"] / score["original_sample_rate"],
            seconds=len(audio) / OUTPUT_RATE,
            duration_error_seconds=len(audio) / OUTPUT_RATE - score["original_frames"] / score["original_sample_rate"],
            notes=sum(event["type"] == "note" for event in score["events"]),
            drums=sum(event["type"] == "drum" for event in score["events"]))
        if abs(measured["duration_error_seconds"]) > 1 / OUTPUT_RATE or measured["true_peak_dbtp"] > -6:
            raise ValueError(f"Invalid rendered bounds: {measured}")
        report.append(measured)
        event_report.append(dict(name=score["name"], score_sha256=computed, protected_score=protected, events=score["events"]))
        if original_archive and previews_dir:
            previews.append(make_previews(score["name"], audio, original_archive, previews_dir))
        print(f"{score['name']}: {measured['seconds']:.5f}s {measured['lufs']:.2f} LUFS "
              f"{measured['true_peak_dbtp']:.2f} dBTP score {computed[:12]}", flush=True)
        (report_dir / "music-render-report.json").write_text(json.dumps({"tracks": report,
            "dependencies": {name: version(name) for name in ("numpy", "scipy", "soundfile", "pyloudnorm")},
            "pitch_samples": bank.pitch_report, "previews": previews}, indent=2), encoding="utf-8")
        (report_dir / "music-events-rendered.json").write_text(json.dumps({"tracks": event_report}, indent=2), encoding="utf-8")
