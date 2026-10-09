"""Offline contracts: unchanged score, sample provenance, pitch, and saved WAVs."""
import copy
import hashlib
import json
from pathlib import Path
import unittest

import numpy as np
import soundfile as sf

from generate_background_music import TRACKS, OUT, build_score
from music_renderer import OUTPUT_RATE, SampleBank, protected_score, sha256_json

ROOT = Path(__file__).parent


class MusicScoreContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        reference = json.loads((ROOT / "AudioSamples/music-score-reference.json").read_text())
        cls.expected = {row["name"]: row for row in reference["tracks"]}

    def test_every_new_score_matches_prechange_reference(self):
        self.assertEqual(len(TRACKS), 21)
        for config in TRACKS:
            with self.subTest(track=config[0]):
                score = build_score(*config)
                self.assertEqual(sha256_json(protected_score(score)), self.expected[config[0]]["score_sha256"])
                self.assertEqual(sum(event["type"] == "drum" for event in score["events"]), 96)

    def test_contract_detects_changed_pitch_onset_gate_or_tempo(self):
        original = build_score(*TRACKS[0])
        expected = self.expected[original["name"]]["score_sha256"]
        for field, delta in (("midi", 12), ("start_seconds", .01), ("duration_seconds", .01)):
            with self.subTest(field=field):
                changed = copy.deepcopy(original)
                changed["events"][0][field] += delta
                self.assertNotEqual(sha256_json(protected_score(changed)), expected)
        changed = copy.deepcopy(original)
        changed["bpm"] += 1
        self.assertNotEqual(sha256_json(protected_score(changed)), expected)

    def test_out_of_file_accents_are_still_scheduled_but_not_lengthened(self):
        total = 0
        for config in TRACKS:
            score = build_score(*config)
            duration = score["original_frames"] / score["original_sample_rate"]
            outside = [event for event in score["events"] if event["start_seconds"] >= duration]
            self.assertEqual(len(outside), 2 if score["intensity"] > .78 else 0)
            self.assertTrue(all(event["lane"] == "accent_secondary" for event in outside))
            total += len(outside)
        self.assertEqual(total, 16)

    def test_saved_files_preserve_actual_duration_and_stereo(self):
        for config in TRACKS:
            with self.subTest(track=config[0]):
                info = sf.info(OUT / (config[0] + ".wav"))
                self.assertEqual((info.samplerate, info.channels, info.subtype), (48000, 2, "PCM_24"))
                self.assertLessEqual(abs(info.duration - self.expected[config[0]]["original_file_seconds"]), 1 / OUTPUT_RATE)

    def test_pinned_source_recordings_match_manifest(self):
        root = ROOT / "AudioSamples/Instruments"
        manifest = json.loads((root / "manifest.json").read_text())
        self.assertEqual(len(manifest), 26)
        for row in manifest:
            with self.subTest(sample=row["file"]):
                self.assertEqual(hashlib.sha256((root / row["file"]).read_bytes()).hexdigest(), row["sha256"])

    def test_actual_timbres_keep_expected_octave_when_transposed(self):
        bank = SampleBank(ROOT / "AudioSamples/Instruments")
        # These reference notes have a dominant fundamental. This FFT searches the
        # full audible low/mid spectrum rather than assuming the renderer's root.
        for family, midi in (("harp", 60), ("harp", 62), ("cello", 57),
                             ("clarinet", 70), ("bass", 49), ("bass", 51)):
            with self.subTest(family=family, midi=midi):
                audio = bank.pitched(family, midi).mean(axis=1)
                window = audio[int(.15 * OUTPUT_RATE):int(.8 * OUTPUT_RATE)]
                spectrum = np.abs(np.fft.rfft(window * np.hanning(len(window)), 262144))
                frequencies = np.fft.rfftfreq(262144, 1 / OUTPUT_RATE)
                audible = np.flatnonzero((frequencies > 30) & (frequencies < 3000))
                frequency = frequencies[audible[np.argmax(spectrum[audible])]]
                measured_midi = 69 + 12 * np.log2(frequency / 440)
                self.assertLess(abs(measured_midi - midi), .4)


if __name__ == "__main__":
    unittest.main()
