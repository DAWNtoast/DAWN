# Reproducible non-music audio

Active generator: `../../remaster_sound_effects.py`.

The two older `generate_*_sound_effects.js` entry points now run that Python
generator and exit before their retained legacy implementations. The originals
are also preserved byte-for-byte under `Legacy/`. **Do not run copied legacy
code to regenerate production audio.**

Requirements: Python 3, numpy, scipy, soundfile, pyloudnorm. In this workspace
the dependencies are installed in `Artifacts/AudioFix20261007/python-deps`.
Set `PYTHONPATH` to that directory and run the bundled Python executable, or
install these packages in the developer's own environment. JS launchers accept
`AUDIO_PYTHON` to select another interpreter.

```
python UnityClient/Tools/remaster_sound_effects.py --compare
```

Use `--fetch` only if an allowlisted source is missing. Every fetched or cached
source must match its hardcoded SHA-256. A changed upstream file stops the
generator; it is never accepted automatically. `--group battle` renders 14
clips; `--group nonbattle` renders 22 cues and six 8-second loops. No command
writes Music, Unity `.meta` files, scenes, catalogs or event callbacks.

Source filenames and exact URLs are in `manifest.json`; each output's recipe,
source aliases, duration, readback loudness, oversampled true-peak estimate and
hash are in `Artifacts/AudioFix20261007/sfx-remaster-report.json`.

The remake uses real material attacks and water texture, filtering, short
envelopes and restrained layers. There are no oscillators or musical phrases
in its active rendering path. Ambient stereo is deliberately narrow. The
6 original 8-second loop lengths and all 36 original cue frame counts remain.
The approximate LUFS meter pads sub-400ms cues with trailing silence for its
400ms BS.1770 block. Metrics describe PCM files, not the final Unity mixer.

No local perceptual audition has been performed. The generated 12-second
comparison files and their timeline guide are intended for human listening.
