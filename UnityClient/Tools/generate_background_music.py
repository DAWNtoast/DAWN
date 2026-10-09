"""Render the unchanged 21 music scores through the acoustic sample renderer.

Install Tools/requirements-audio.txt, then run this file from any directory.
The original timing arithmetic, event order and off-end accents are retained.
No old oscillator renderer or .meta writer remains. Importing performs no I/O.
"""
import argparse
from pathlib import Path
from music_renderer import SampleBank, export_tracks

OUT = Path(__file__).resolve().parents[1] / "Assets" / "_Project" / "Resources" / "Audio" / "Music"


SR = 22050

DURATION_BARS = 16

TRACKS = [
    ("port_broken_anchor", 82, 50, [0,2,3,5,7,9,10], 0, 0.56),
    ("port_white_shell", 96, 60, [0,2,4,5,7,9,11], 1, 0.52),
    ("port_bronze_bell", 88, 55, [0,2,4,7,9], 2, 0.54),
    ("port_salt_crown", 92, 52, [0,1,3,5,7,8,10], 3, 0.60),
    ("port_twilight_tide", 78, 57, [0,2,3,5,7,8,10], 4, 0.58),
    ("port_gray_gull", 104, 62, [0,2,4,5,7,9,10], 5, 0.50),
    ("route_shattered_sail_reef", 112, 50, [0,2,3,5,7,9,10], 6, 0.67),
    ("route_golden_tide_trade", 124, 55, [0,2,4,5,7,9,11], 7, 0.66),
    ("route_moon_tide_ruins", 88, 45, [0,2,3,5,7,8,10], 8, 0.63),
    ("treasure_weathered", 82, 45, [0,2,3,5,7,8,10], 9, 0.60),
    ("treasure_gilded", 104, 57, [0,2,4,5,7,9,11], 10, 0.58),
    ("treasure_moonlit", 72, 62, [0,2,4,6,7,9,11], 11, 0.55),
    ("boss_route_shattered_sail_reef", 126, 50, [0,1,3,5,7,8,10], 12, 0.84),
    ("boss_route_golden_tide_trade", 138, 52, [0,2,3,5,7,8,11], 13, 0.88),
    ("boss_route_moon_tide_ruins", 118, 45, [0,2,3,5,7,8,10], 14, 0.86),
    ("battle_nonboss", 130, 50, [0,2,3,5,7,8,10], 15, 0.76),
    ("battle_legendary_iron_oath", 112, 43, [0,2,3,5,7,8,10], 16, 0.92),
    ("battle_legendary_thunder_crown", 144, 57, [0,2,4,5,7,9,10], 17, 0.94),
    ("battle_legendary_red_widow", 132, 52, [0,2,3,5,7,8,11], 18, 0.93),
    ("battle_legendary_ember_ark", 124, 45, [0,1,3,5,7,8,10], 19, 0.91),
    ("battle_legendary_tide_requiem", 108, 50, [0,2,4,6,7,9,11], 20, 0.89),
]

def midi_for(root, scale, degree):
    octave, idx = divmod(degree, len(scale))
    return root + octave * 12 + scale[idx]

def add_note(buf, sr, start, duration, midi, amp, kind="sine", vibrato=0.0, lane="lead"):
    # kind/vibrato are historical score metadata, not oscillator implementations.
    buf.append(dict(type="note", lane=lane, midi=midi, start_seconds=start,
                    duration_seconds=duration, original_amplitude=amp,
                    original_instrument=kind, original_vibrato_hz=vibrato))


def add_drum(buf, sr, start, duration, amp, kind, rng):
    buf.append(dict(type="drum", lane=kind, start_seconds=start,
                    duration_seconds=duration, original_amplitude=amp))


def build_score(name, bpm, root, scale, seed, intensity):
    bar = 60.0 / bpm * 4.0
    total = bar * DURATION_BARS
    n = int(total * SR)
    buf = []
    rng = None
    progression = [0, 5, 3, 4] if seed % 3 else [0, 3, 5, 4]
    pad_kind = 'triangle' if intensity < 0.7 else 'saw'
    for bar_index in range(DURATION_BARS):
        start = bar_index * bar
        degree = progression[bar_index % len(progression)]
        chord = [midi_for(root, scale, degree + x) for x in (0, 2, 4)]
        for note in chord:
            add_note(buf, SR, start, bar * 1.05, note, 0.075 if intensity < 0.7 else 0.09, pad_kind, 0.15, lane='pad')
        bass = midi_for(root - 12, scale, degree)
        add_note(buf, SR, start, bar * 0.42, bass, 0.21 * intensity, 'sine', 0.0, lane='bass_primary')
        add_note(buf, SR, start + bar * 0.52, bar * 0.4, midi_for(root - 12, scale, degree + 4), 0.15 * intensity, 'sine', 0.0, lane='bass_secondary')
        motif = [(0, 0.0), (2, 0.5), (4, 1.0), (2, 1.5), (3, 2.0), (1, 2.5), (4, 3.0), (2, 3.5)]
        shift = (bar_index * 2 + seed) % 5
        for note_degree, beat in motif:
            if (bar_index + seed) % 5 == 0 and beat == 2.5:
                continue
            note = midi_for(root + 12, scale, note_degree + shift)
            add_note(buf, SR, start + beat * bar / 4.0, bar * 0.32, note, (0.075 if intensity < 0.7 else 0.1) * (1.0 - bar_index % 4 * 0.035), 'saw' if intensity > 0.72 else 'triangle', 4.2, lane='lead')
    beat = 60.0 / bpm
    for step in range(int(total / beat)):
        t = step * beat
        phase = step % 4
        if phase in (0, 2):
            add_drum(buf, SR, t, min(0.18, beat * 0.75), 0.16 * intensity, 'kick', rng)
        if phase in (1, 3):
            add_drum(buf, SR, t, min(0.14, beat * 0.5), 0.095 * intensity, 'snare', rng)
        if step % 2 == 0:
            add_drum(buf, SR, t, min(0.045, beat * 0.18), 0.035 * intensity, 'hat', rng)
    if intensity > 0.78:
        for bar_index in range(DURATION_BARS):
            start = bar_index * bar
            add_note(buf, SR, start + bar * 0.75, bar * 0.18, midi_for(root + 12, scale, 7), 0.09, 'square', 8.0, lane='accent_primary')
            add_note(buf, SR, start + bar * 2.75, bar * 0.18, midi_for(root + 12, scale, 5), 0.08, 'square', 9.0, lane='accent_secondary')
    return dict(name=name, bpm=bpm, root=root, scale=scale, seed=seed, intensity=intensity, bars=DURATION_BARS, original_sample_rate=SR, original_frames=n, nominal_seconds=total, events=buf)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="*", help="Optional exact track names")
    parser.add_argument("--output", type=Path, default=OUT)
    parser.add_argument("--report-dir", type=Path, default=Path(__file__).parent / "AudioSamples" / "rendered-music")
    parser.add_argument("--original-archive", type=Path, help="Baseline ZIP for original/equal-LUFS A/B")
    parser.add_argument("--previews-dir", type=Path)
    args = parser.parse_args()
    known = {track[0] for track in TRACKS}
    if args.only and set(args.only) - known:
        parser.error("Unknown track: " + ", ".join(sorted(set(args.only) - known)))
    scores = [build_score(*track) for track in TRACKS if not args.only or track[0] in args.only]
    bank = SampleBank(Path(__file__).parent / "AudioSamples" / "Instruments")
    export_tracks(scores, bank, args.output, args.report_dir, args.original_archive, args.previews_dir)


if __name__ == "__main__":
    main()
