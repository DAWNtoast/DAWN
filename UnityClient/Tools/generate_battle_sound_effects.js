// Active entry point: the deterministic CC0 Foley remaster. The legacy source
// below is retained for history; it is never executed and cannot recreate the
// old oscillator clips or rewrite Unity importers. A byte-for-byte original
// also lives in AudioSamples/Foley/Legacy/.
{
  const remasterPath = require('path');
  const remasterFs = require('fs');
  const bundledPython = remasterPath.join(require('os').homedir(), '.cache', 'codex-runtimes', 'codex-primary-runtime', 'dependencies', 'python', 'python.exe');
  const python = process.env.AUDIO_PYTHON || (remasterFs.existsSync(bundledPython) ? bundledPython : 'python');
  const deps = remasterPath.resolve(__dirname, '../../Artifacts/AudioFix20261007/python-deps');
  const env = { ...process.env };
  if (remasterFs.existsSync(deps)) env.PYTHONPATH = [deps, env.PYTHONPATH].filter(Boolean).join(remasterPath.delimiter);
  const result = require('child_process').spawnSync(python,
    [remasterPath.join(__dirname, 'remaster_sound_effects.py'), '--group', 'battle', ...process.argv.slice(2)],
    { stdio: 'inherit', env });
  if (result.error) throw result.error;
  process.exit(result.status === null ? 1 : result.status);
}

// ARCHIVED SYNTHESIS IMPLEMENTATION (unreachable; preserved for review only).
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const sampleRate = 48000;
const outputDirectory = path.resolve(__dirname, '..', 'Assets', '_Project', 'Resources', 'Audio', 'BattleSfx');
const TAU = Math.PI * 2;

function seededRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0;
    return state / 0x100000000;
  };
}

function makeBuffer(seconds) {
  return new Float32Array(Math.ceil(seconds * sampleRate));
}

function envelope(t, length, attack, release, power = 1) {
  if (t < 0 || t >= length) return 0;
  const a = attack <= 0 ? 1 : Math.min(1, t / attack);
  const r = release <= 0 ? 1 : Math.min(1, (length - t) / release);
  return Math.pow(Math.min(a, r), power);
}

function wave(phase, type) {
  const cycle = phase - Math.floor(phase);
  if (type === 'triangle') return 1 - 4 * Math.abs(Math.round(cycle) - cycle);
  if (type === 'saw') return cycle * 2 - 1;
  if (type === 'square') return cycle < 0.5 ? 1 : -1;
  return Math.sin(phase * TAU);
}

function addTone(buffer, start, length, fromHz, toHz, amplitude, type = 'sine', attack = 0.005, release = 0.1) {
  const startIndex = Math.floor(start * sampleRate);
  const count = Math.floor(length * sampleRate);
  let phase = 0;
  for (let i = 0; i < count; i++) {
    const index = startIndex + i;
    if (index < 0 || index >= buffer.length) continue;
    const t = i / sampleRate;
    const progress = count <= 1 ? 1 : i / (count - 1);
    const frequency = fromHz + (toHz - fromHz) * progress;
    phase += frequency / sampleRate;
    buffer[index] += wave(phase, type) * amplitude * envelope(t, length, attack, release, 0.8);
  }
}

function addNoise(buffer, start, length, amplitude, attack = 0.002, release = 0.15, seed = 1, tilt = 0) {
  const random = seededRandom(seed);
  const startIndex = Math.floor(start * sampleRate);
  const count = Math.floor(length * sampleRate);
  let previous = 0;
  for (let i = 0; i < count; i++) {
    const index = startIndex + i;
    if (index < 0 || index >= buffer.length) continue;
    const raw = random() * 2 - 1;
    previous = previous * tilt + raw * (1 - tilt);
    buffer[index] += previous * amplitude * envelope(i / sampleRate, length, attack, release, 0.7);
  }
}

function addBell(buffer, start, frequency, amplitude, length) {
  addTone(buffer, start, length, frequency, frequency * 0.98, amplitude, 'sine', 0.002, length * 0.82);
  addTone(buffer, start, length * 0.72, frequency * 2.01, frequency * 2.0, amplitude * 0.22, 'sine', 0.002, length * 0.66);
}

function normalize(samples) {
  let peak = 0;
  for (const value of samples) peak = Math.max(peak, Math.abs(value));
  const gain = peak > 0.98 ? 0.98 / peak : 0.86;
  for (let i = 0; i < samples.length; i++) {
    const value = samples[i] * gain;
    samples[i] = Math.tanh(value * 1.18) / Math.tanh(1.18);
  }
}

function writeWav(name, samples) {
  normalize(samples);
  const dataSize = samples.length * 2;
  const header = Buffer.alloc(44);
  header.write('RIFF', 0);
  header.writeUInt32LE(36 + dataSize, 4);
  header.write('WAVE', 8);
  header.write('fmt ', 12);
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(1, 22);
  header.writeUInt32LE(sampleRate, 24);
  header.writeUInt32LE(sampleRate * 2, 28);
  header.writeUInt16LE(2, 32);
  header.writeUInt16LE(16, 34);
  header.write('data', 36);
  header.writeUInt32LE(dataSize, 40);
  const data = Buffer.alloc(dataSize);
  for (let i = 0; i < samples.length; i++) {
    const value = Math.max(-1, Math.min(1, samples[i]));
    data.writeInt16LE(Math.round(value * 32767), i * 2);
  }
  fs.writeFileSync(path.join(outputDirectory, `${name}.wav`), Buffer.concat([header, data]));
}

function ensureMeta(filePath, folder = false) {
  const metaPath = `${filePath}.meta`;
  if (fs.existsSync(metaPath)) return;
  const guid = crypto.randomBytes(16).toString('hex');
  const contents = folder
    ? `fileFormatVersion: 2\nguid: ${guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`
    : `fileFormatVersion: 2\nguid: ${guid}\nAudioImporter:\n  serializedVersion: 7\n  audioClip:\n    serializedVersion: 2\n    loadType: 1\n    sampleRateSetting: 0\n    sampleRateOverride: 0\n    compressionFormat: 1\n    quality: 0.8\n    conversionMode: 0\n  forceToMono: 1\n  normalize: 1\n  preloadAudioData: 0\n  loadInBackground: 1\n  ambisonic: 0\n  3D: 0\n  loopable: 0\n`;
  fs.writeFileSync(metaPath, contents);
}

const sounds = {
  battle_start() {
    const b = makeBuffer(0.95);
    addTone(b, 0, 0.68, 92, 116, 0.4, 'saw', 0.03, 0.28);
    addTone(b, 0.09, 0.58, 138, 174, 0.24, 'triangle', 0.02, 0.3);
    addBell(b, 0.14, 410, 0.24, 0.72);
    addNoise(b, 0.02, 0.28, 0.08, 0.005, 0.12, 11, 0.65);
    return b;
  },
  battle_card_play() {
    const b = makeBuffer(0.46);
    addNoise(b, 0, 0.07, 0.42, 0.001, 0.045, 21, 0.15);
    addTone(b, 0.01, 0.1, 178, 108, 0.42, 'triangle', 0.001, 0.08);
    addTone(b, 0.1, 0.25, 640, 930, 0.2, 'sine', 0.01, 0.18);
    addBell(b, 0.12, 1220, 0.14, 0.24);
    return b;
  },
  battle_enemy_intent() {
    const b = makeBuffer(0.56);
    addTone(b, 0, 0.34, 196, 156, 0.38, 'saw', 0.025, 0.13);
    addTone(b, 0.18, 0.36, 247, 196, 0.34, 'saw', 0.02, 0.18);
    addBell(b, 0.02, 392, 0.16, 0.4);
    return b;
  },
  battle_maneuver() {
    const b = makeBuffer(0.72);
    addNoise(b, 0, 0.62, 0.25, 0.06, 0.2, 41, 0.72);
    addTone(b, 0, 0.68, 86, 132, 0.22, 'sine', 0.08, 0.22);
    addTone(b, 0.08, 0.46, 240, 520, 0.12, 'triangle', 0.08, 0.2);
    return b;
  },
  battle_cannon_fire() {
    const b = makeBuffer(1.08);
    addTone(b, 0, 0.55, 68, 42, 0.85, 'sine', 0.001, 0.43);
    addTone(b, 0, 0.24, 125, 58, 0.54, 'triangle', 0.001, 0.18);
    addNoise(b, 0, 0.34, 0.68, 0.001, 0.25, 51, 0.32);
    addNoise(b, 0.21, 0.73, 0.2, 0.015, 0.45, 52, 0.86);
    addTone(b, 0.02, 0.18, 730, 160, 0.18, 'saw', 0.001, 0.12);
    return b;
  },
  battle_impact() {
    const b = makeBuffer(0.62);
    addTone(b, 0, 0.34, 102, 48, 0.72, 'sine', 0.001, 0.25);
    addNoise(b, 0, 0.22, 0.64, 0.001, 0.16, 61, 0.45);
    addTone(b, 0.03, 0.28, 310, 86, 0.2, 'triangle', 0.001, 0.19);
    return b;
  },
  battle_melee() {
    const b = makeBuffer(0.68);
    addNoise(b, 0, 0.22, 0.3, 0.01, 0.17, 71, 0.55);
    addTone(b, 0, 0.4, 410, 250, 0.26, 'saw', 0.012, 0.24);
    addTone(b, 0.12, 0.43, 820, 570, 0.34, 'triangle', 0.004, 0.27);
    addBell(b, 0.18, 1040, 0.16, 0.32);
    return b;
  },
  battle_melee_impact() {
    const b = makeBuffer(0.7);
    addTone(b, 0, 0.34, 136, 54, 0.62, 'sine', 0.001, 0.26);
    addNoise(b, 0, 0.15, 0.55, 0.001, 0.12, 81, 0.38);
    addTone(b, 0.025, 0.34, 760, 325, 0.42, 'saw', 0.001, 0.26);
    addBell(b, 0.11, 1360, 0.14, 0.34);
    return b;
  },
  battle_ram_charge() {
    const b = makeBuffer(0.82);
    addNoise(b, 0, 0.7, 0.26, 0.18, 0.2, 91, 0.78);
    addTone(b, 0, 0.72, 54, 116, 0.48, 'sine', 0.13, 0.2);
    addTone(b, 0.26, 0.48, 150, 220, 0.17, 'saw', 0.05, 0.19);
    return b;
  },
  battle_ram_impact() {
    const b = makeBuffer(1.18);
    addTone(b, 0, 0.72, 54, 31, 0.92, 'sine', 0.001, 0.54);
    addTone(b, 0, 0.32, 112, 45, 0.58, 'triangle', 0.001, 0.24);
    addNoise(b, 0, 0.28, 0.74, 0.001, 0.24, 101, 0.4);
    addNoise(b, 0.22, 0.74, 0.29, 0.01, 0.5, 102, 0.8);
    addTone(b, 0.28, 0.68, 220, 76, 0.25, 'saw', 0.02, 0.38);
    return b;
  },
  battle_guard() {
    const b = makeBuffer(0.5);
    addBell(b, 0, 520, 0.42, 0.42);
    addTone(b, 0.02, 0.32, 980, 610, 0.18, 'triangle', 0.002, 0.23);
    addNoise(b, 0, 0.11, 0.17, 0.001, 0.08, 111, 0.2);
    return b;
  },
  battle_repair() {
    const b = makeBuffer(0.6);
    [0.04, 0.2, 0.37].forEach((start, index) => {
      addTone(b, start, 0.11, 260 + index * 62, 120 + index * 42, 0.34, 'triangle', 0.001, 0.08);
      addNoise(b, start, 0.045, 0.2, 0.001, 0.025, 121 + index, 0.15);
    });
    addBell(b, 0.25, 740, 0.18, 0.28);
    return b;
  },
  battle_victory() {
    const b = makeBuffer(1.18);
    addTone(b, 0, 0.64, 196, 196, 0.23, 'saw', 0.03, 0.18);
    addTone(b, 0.18, 0.7, 247, 247, 0.25, 'saw', 0.03, 0.19);
    addTone(b, 0.36, 0.78, 294, 294, 0.29, 'saw', 0.03, 0.24);
    addBell(b, 0.36, 588, 0.2, 0.72);
    addNoise(b, 0.38, 0.54, 0.1, 0.02, 0.36, 131, 0.72);
    return b;
  },
  battle_sunk() {
    const b = makeBuffer(1.42);
    addTone(b, 0, 1.1, 130, 38, 0.55, 'saw', 0.04, 0.4);
    addTone(b, 0, 0.76, 76, 31, 0.42, 'sine', 0.02, 0.35);
    addNoise(b, 0.18, 1.05, 0.31, 0.02, 0.68, 141, 0.84);
    addBell(b, 0.04, 290, 0.18, 0.9);
    return b;
  },
};

fs.mkdirSync(outputDirectory, { recursive: true });
ensureMeta(outputDirectory, true);
for (const [name, factory] of Object.entries(sounds)) {
  writeWav(name, factory());
  ensureMeta(path.join(outputDirectory, `${name}.wav`));
}
console.log(`Generated ${Object.keys(sounds).length} battle SFX clips in ${outputDirectory}`);
