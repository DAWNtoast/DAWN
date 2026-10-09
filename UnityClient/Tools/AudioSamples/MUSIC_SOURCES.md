# Acoustic music palette and reproducible rendering

The 21 project scores are unchanged. `generate_background_music.py` now schedules
those original notes and calls `music_renderer.py`; it has no oscillator fallback
and never writes Unity `.meta` files. The original entry point therefore cannot
silently regenerate the previous saw/square arrangement.

## Sample sources

All 26 source recordings are distributed under CC0-1.0 by their upstream projects.
The complete upstream license texts and README credits are retained in
`Instruments/`. `Instruments/manifest.json` contains each exact source URL,
repository, pinned commit, license, byte count and SHA-256. The renderer verifies
every sample hash before use. No network connection is required to regenerate.

| Part | Samples | Upstream | Pinned commit |
|---|---:|---|---|
| Folk harp | 12 | [Versilian Community Sample Library](https://github.com/sgossner/VCSL) | `c1ea7bcc3c7309650ab0da9d15c9cd1fbc4a4c7e` |
| Cello section sustain | 6 | [VS Chamber Orchestra Community Edition](https://github.com/sgossner/VSCO-2-CE) | `440300901dfe9275fd84e0b7763af1f8443ae62e` |
| Clarinet sustain | 4 | Same VSCO 2 CE source | Same commit |
| Contrabass pizzicato | 4 | Same VSCO 2 CE source | Same commit |

VCSL is provided by Versilian Studios LLC. The VSCO 2 CE README credits recordings
to Sam Gossner and Simon Dalzell, and sample cutting to Elan Hickler/Soundemote.
The derived project music uses resampling, tuning correction, envelope shaping,
filtering and a restrained stereo mix. Existing project score ownership is
unaffected by the use of these source recordings.

## Pitch convention

These specific libraries label MIDI 60 middle C as `C3`; scientific pitch notation
calls that pitch `C4`. The renderer uses `(filename_octave + 2) * 12 + pitch_class`,
confirmed from actual spectra across all 26 recordings. Examples: harp `C3` is
about 261 Hz / MIDI 60, cello `C1` about 65 Hz / MIDI 36, clarinet `D3` about 294 Hz
/ MIDI 62, and contrabass `C#2` about 139 Hz / MIDI 49. Never interpret those names
with the scientific octave number or transpose a whole part to fit a sample.

Each sample's median early/steady fundamental is measured and tuning-corrected
when transposing to the score's exact MIDI. The natural vibrato and recorded
instrument texture remain. Reports include source tuning measurements.

## Regeneration

Python 3.12.14 was used for this render. Install the exact versions in
`Tools/requirements-audio.txt` into a virtual environment:

```powershell
python -m venv .venv-audio
.\.venv-audio\Scripts\python.exe -m pip install -r Tools/requirements-audio.txt
.\.venv-audio\Scripts\python.exe Tools/generate_background_music.py
```

The default output is `Assets/_Project/Resources/Audio/Music`, with verification
reports in `Tools/AudioSamples/rendered-music`. It writes only the 21 WAV resources
and reports. Use `--output` and `--report-dir` to render to an alternate directory.

```powershell
python Tools/generate_background_music.py --only port_broken_anchor battle_nonboss --output C:/Temp/music-preview --report-dir C:/Temp/music-report
```

For historical A/B, pass `--original-archive <source-before.zip>` and
`--previews-dir <directory>`. The original is copied unchanged; separate versions
are matched in integrated loudness, and `__AB_equal_12s.wav` plays original and new
versions of the same 2–14 second passage with 0.5 seconds of silence between them.

## Contracts

`music-score-reference.json` holds pre-change reference hashes. Every new render
**recomputes** the complete ordered score from the scheduler and hashes name,
BPM, root, scale, bars, original timebase/frame length and every note/drum lane,
MIDI, onset and gate. Onsets/gates use IEEE float hex to avoid rounding. An altered
score fails before audio rendering; expected hashes are not copied as evidence.
`music-events-rendered.json` contains the newly computed complete event list.

Original half-beat omissions and the `start + bar * 2.75` accent are retained,
including two out-of-file accent events in each of eight intense tracks. The
renderer does not play those events or extend the loop to accommodate them.

The render is 48 kHz / stereo / PCM24. Each length is the nearest 48 kHz frame to
the original actual WAV length, with error at most one output frame. Musical
gates stay unchanged; envelopes and natural sample decay are shaped within them.
Mix target is -23 LUFS integrated, with a ceiling below -6 dBTP measured by 4x
oversampling. Smooth 220 ms file-edge fades and short room reflections preserve
loop boundaries without appending a reverb tail or adding rhythmic echoes.

The new instruments are harp lead, restrained cello pad, pizzicato contrabass,
and soft clarinet accents with occasional same-note lead colour. Drums retain
their exact events and use smooth wood/drum sinusoids and filtered brushes.
There are no unbandlimited saw/square oscillators or waveshaping saturation.

Unity import should preserve calibrated levels and channels (`forceToMono=false`,
`normalize=false`). Import configuration and playback mixing are separate from
this renderer and must be verified in the editor/build by the main task.
