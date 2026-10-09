"""Reproducible CC0 Foley remaster for the 42 existing non-music resources.

Never writes Music, .meta, scene or runtime files. Existing event names and
sample counts are fixed below. Use --fetch only to retrieve the six pinned
public sources; every archive/audio byte is SHA-256 checked before decoding.

Requires numpy, scipy, soundfile and pyloudnorm. The old JS entry points delegate
here. Their original source is retained in AudioSamples/Foley/Legacy/.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
import platform
import sys
import urllib.request
import zipfile
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import pyloudnorm as pyln
import scipy
from scipy import ndimage, signal
import soundfile as sf

SR = 48000
TOOLS = Path(__file__).resolve().parent
SAMPLES = TOOLS / "AudioSamples" / "Foley"
AUDIO = TOOLS.parent / "Assets" / "_Project" / "Resources" / "Audio"
DEFAULT_REPORT = TOOLS.parents[1] / "Artifacts" / "AudioFix20261007"
METER = pyln.Meter(SR)
KENNEY_PAGE = "https://kenney.nl/assets/impact-sounds"
BATTLE_PAGE = "https://opengameart.org/content/battle-at-sea"
WATER_PAGE = "https://opengameart.org/content/sea-and-river-wave-sounds"
CC0 = "https://creativecommons.org/publicdomain/zero/1.0/"

SOURCES = [
    dict(file="kenney_impact-sounds.zip", author="Kenney", page=KENNEY_PAGE,
         url="https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip",
         sha256="029d734af1582474edf3a694d1b0cebc97c1c152f2f39fa34d4c2bafc5de77f8"),
    dict(file="cannon_fire_1.ogg", author="Thimras", page=BATTLE_PAGE,
         url="https://opengameart.org/sites/default/files/cannon_fire_1.ogg",
         sha256="2b21ffa41e5c3be4eecdfbd3490544f5aa8b6d1581787265863e13de1a8e599d"),
    dict(file="cannon_hit_ship_short.ogg", author="Thimras", page=BATTLE_PAGE,
         url="https://opengameart.org/sites/default/files/cannon_hit_ship_short.ogg",
         sha256="24bdd9841ad964491b52e536cf191de15b59543076b60f1accbe0e44edd68ffe"),
    dict(file="cannon_miss_1.ogg", author="Thimras", page=BATTLE_PAGE,
         url="https://opengameart.org/sites/default/files/cannon_miss_1.ogg",
         sha256="d91a1effc2964673033fa9f8d22093029fa0b500aea0bafb86724738c0e89fe3"),
    dict(file="ship_ram_ship_shortened.ogg", author="Thimras", page=BATTLE_PAGE,
         url="https://opengameart.org/sites/default/files/ship_ram_ship_shortened.ogg",
         sha256="ae611c487c538752ed69b387b97464c9d3864313592b445cc8aac864d0db1fe8"),
    dict(file="VistulaShort_0.mp3", author="RandomMind", page=WATER_PAGE,
         url="https://opengameart.org/sites/default/files/VistulaShort_0.mp3",
         sha256="8c24607e41edd6f018ec3582a07e5a8dbc57347276339cb68440951a0fb4cfb1"),
]

# Material, articulation and low-register spectral contrast replace electronic
# whistles. The samples are not phrases from any music track.
ALIASES = {
    "wood": "impactWood_light_000.ogg", "wood2": "impactWood_light_003.ogg",
    "knock": "impactWood_medium_000.ogg", "knock2": "impactWood_medium_002.ogg",
    "heavywood": "impactWood_heavy_000.ogg", "plank": "impactPlank_medium_000.ogg",
    "metal": "impactMetal_light_000.ogg", "metal2": "impactMetal_light_003.ogg",
    "iron": "impactMetal_medium_000.ogg", "bell": "impactBell_heavy_000.ogg",
    "glass": "impactGlass_light_000.ogg", "glass2": "impactGlass_light_002.ogg",
    "plate": "impactPlate_light_000.ogg", "tin": "impactTin_medium_000.ogg",
    "soft": "impactSoft_medium_000.ogg", "cloth": "impactSoft_heavy_000.ogg",
    "punch": "impactPunch_medium_000.ogg", "step": "footstep_wood_000.ogg",
    "step2": "footstep_wood_003.ogg", "carpet": "footstep_carpet_000.ogg",
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def prepare_sources(fetch=False):
    SAMPLES.mkdir(parents=True, exist_ok=True)
    for source in SOURCES:
        path = SAMPLES / source["file"]
        if not path.exists():
            if not fetch:
                raise RuntimeError(f"Missing {path}; run this entry point with --fetch.")
            with urllib.request.urlopen(source["url"], timeout=90) as response:
                data = response.read()
            if sha(data) != source["sha256"]:
                raise RuntimeError(f"Source changed; refusing to use {source['url']}")
            path.write_bytes(data)
        if sha(path.read_bytes()) != source["sha256"]:
            raise RuntimeError(f"Source checksum mismatch: {path}")
    extracted = []
    with zipfile.ZipFile(SAMPLES / "kenney_impact-sounds.zip") as archive:
        # A fixed allowlist, never arbitrary archive extraction.
        for filename in sorted(set(ALIASES.values())):
            data = archive.read("Audio/" + filename)
            path = SAMPLES / "Kenney" / filename
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(data)
            extracted.append(dict(file="Kenney/" + filename, archive_member="Audio/" + filename,
                                  sha256=sha(data), bytes=len(data), license="CC0-1.0"))
        license_data = archive.read("License.txt")
        (SAMPLES / "LICENSE-Kenney.txt").write_bytes(license_data)
    manifest = dict(schema=1, license="CC0-1.0", license_url=CC0,
                    purpose="Only non-music Foley assets; no melody or musical phrase downloads.",
                    provenance_note="Kenney: creator's bundled license. Thimras and RandomMind: creator's original CC0 publication pages. River recording is not a claimed ocean field recording; naval effects are authored sound design.",
                    sources=[dict(source, license="CC0-1.0", license_source=source["page"],
                                  bytes=(SAMPLES / source["file"]).stat().st_size) for source in SOURCES],
                    kenney_license_sha256=sha(license_data), extracted_files=extracted,
                    recipe_aliases={**{alias: "Kenney/"+filename for alias,filename in ALIASES.items()},
                                    "cannon":"cannon_fire_1.ogg", "hit":"cannon_hit_ship_short.ogg",
                                    "splash":"cannon_miss_1.ogg", "ram":"ship_ram_ship_shortened.ogg",
                                    "water":"VistulaShort_0.mp3"})
    write_json(SAMPLES / "manifest.json", manifest)
    return manifest


def rate_convert(x, old_sr):
    if old_sr == SR:
        return x
    d = math.gcd(int(old_sr), SR)
    return signal.resample_poly(x, SR // d, int(old_sr) // d, axis=0)


def band(x, hp=75., lp=4700.):
    x = signal.sosfiltfilt(signal.butter(3, hp, "highpass", fs=SR, output="sos"), x, axis=0)
    return signal.sosfiltfilt(signal.butter(3, lp, "lowpass", fs=SR, output="sos"), x, axis=0)


def fades(x, attack=.004, release=.06):
    x = x.copy()
    a, r = min(len(x), int(SR * attack)), min(len(x), int(SR * release))
    if a:
        x[:a] *= np.linspace(0., 1., a).reshape((-1,) + (1,) * (x.ndim - 1))
    if r:
        x[-r:] *= np.linspace(1., 0., r).reshape((-1,) + (1,) * (x.ndim - 1))
    return x


def lufs(x):
    # BS.1770 400 ms blocks; only sub-400 ms clips receive trailing silence.
    if len(x) < int(SR * .4):
        x = np.pad(x, ((0, int(SR * .4) - len(x)),) + ((0, 0),) * (x.ndim - 1))
    return float(METER.integrated_loudness(x))


def db(value):
    return float(20 * np.log10(max(float(value), 1e-12)))


def true_peak(x):
    return float(np.max(np.abs(signal.resample_poly(x, 4, 1, axis=0))))


def master(x, target, ceiling):
    """Loudness match with a linked, soft-envelope lookahead peak limiter.

    No hard clipping or waveshaping: gain changes cover a 5 ms neighborhood
    and are smoothed after extending the minimum-gain region. Iteration makes
    the loudness and peak constraints simultaneous, rather than lowering all
    clips after a peak normalization pass.
    """
    x = np.asarray(x, dtype=np.float64)
    limit = 10 ** ((ceiling - .35) / 20)
    for _ in range(12):
        measured = lufs(x)
        if not np.isfinite(measured):
            raise ValueError("Silent or nonfinite render")
        x *= 10 ** ((target - measured) / 20)
        peak = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
        envelope = ndimage.maximum_filter1d(peak, size=241, mode="nearest")
        gain = np.minimum(1., limit / np.maximum(envelope, 1e-12))
        gain = ndimage.minimum_filter1d(gain, size=193, mode="nearest")
        gain = ndimage.gaussian_filter1d(gain, sigma=24., mode="nearest")
        x *= gain if x.ndim == 1 else gain[:, None]
        if abs(lufs(x) - target) < .06 and db(true_peak(x)) <= ceiling - .08:
            break
    # Time-varying gain can leave a tiny residual mean in an asymmetric impact.
    # Remove it after limiting and keep short-cue endpoints at zero. Ambient
    # channels are mean-centered without a fade, preserving their loop seam.
    x = x - np.mean(x, axis=0)
    if x.ndim == 1:
        x = fades(x, .002, .012)
    peak = true_peak(x)
    if db(peak) > ceiling - .05:
        x *= 10 ** ((ceiling - .05 - db(peak)) / 20)
    return x


class Palette:
    def __init__(self):
        self.cache = {}
        for alias, filename in ALIASES.items():
            self.cache[alias] = self.load(SAMPLES / "Kenney" / filename)
        for alias, filename in [("cannon", "cannon_fire_1.ogg"), ("hit", "cannon_hit_ship_short.ogg"),
                                ("splash", "cannon_miss_1.ogg"), ("ram", "ship_ram_ship_shortened.ogg")]:
            self.cache[alias] = self.load(SAMPLES / filename)
        self.water, sample_rate = sf.read(SAMPLES / "VistulaShort_0.mp3", always_2d=True, dtype="float64")
        self.water = rate_convert(self.water, sample_rate)
        self.water = band(self.water, 130, 5200)

    @staticmethod
    def load(path):
        x, sample_rate = sf.read(path, always_2d=True, dtype="float64")
        x = rate_convert(x.mean(axis=1), sample_rate)
        x -= x.mean()
        onset = np.flatnonzero(np.abs(x) > np.max(np.abs(x)) * .025)
        if len(onset):
            x = x[max(0, int(onset[0]) - int(SR * .002)):]
        return x / max(np.max(np.abs(x)), 1e-9)

    def tone(self, name, duration, pitch=1., hp=85., lp=4400., attack=.004, release=.06, reverse=False):
        # Resampling changes a Foley object's apparent mass, not a music score.
        x = self.cache[name]
        if reverse:
            x = x[::-1]
        if pitch != 1.:
            x = signal.resample_poly(x, 1000, max(1, round(1000 * pitch)))
        x = band(x, hp, lp)
        x /= max(np.max(np.abs(x)), 1e-9)
        n = round(SR * duration)
        # A long authored effect is cropped at the tail; its first impact stays
        # at the event onset. Empty padding keeps the existing clip duration.
        x = fades(x[:n], attack, min(release, len(x[:n]) / SR * .45))
        return np.pad(x, (0, max(0, n - len(x))))

    def water_slice(self, seconds, start, stereo=False, lp=3500):
        a, n = round(start * SR), round(seconds * SR)
        x = self.water[a:a+n].copy()
        if len(x) != n:
            raise ValueError("Water slice outside the source recording")
        x = band(x, 145, lp)
        x /= max(np.sqrt(np.mean(x * x)), 1e-9)
        if not stereo:
            return x.mean(axis=1)
        mid = x.mean(axis=1)
        side = (x[:, 0] - x[:, 1]) * .5 * .24
        return np.column_stack((mid + side, mid - side))


@dataclass(frozen=True)
class Layer:
    material: str
    start: float
    duration: float
    gain: float = 1.
    pitch: float = 1.
    hp: float = 85.
    lp: float = 4400.
    attack: float = .004
    release: float = .06
    reverse: bool = False


def L(material, start, duration, gain=1., pitch=1., **kwargs):
    return Layer(material, start, duration, gain, pitch, **kwargs)


# Frame counts retain even the one-frame Math.ceil artifacts of the legacy JS.
SPECS = {
    "BattleSfx": {
        "battle_start": (45600, "Low drum-like wood warning and a restrained ship bell", [L("heavywood",0,.36,1.,.68,lp=2800),L("bell",.12,.76,.22,.58,lp=2300)]),
        "battle_card_play": (22080,"Card/fabric movement landing on the table",[L("cloth",0,.22,.46,1.2,hp=220),L("wood",.028,.19,.8,1.08,lp=3700)]),
        "battle_enemy_intent": (26881,"Two muted low wood knocks; no saw warning tone",[L("knock",0,.26,.85,.75,lp=2600),L("knock2",.19,.31,.7,.67,lp=2200)]),
        "battle_maneuver": (34560,"Water movement and timber movement",[L("water",0,.68,.045,lp=3100,attack=.035,release=.18),L("plank",.035,.6,.28,.74,hp=110,lp=1800,attack=.03)]),
        "battle_cannon_fire": (51840,"Recorded/designed cannon attack with controlled low body and short tail",[L("cannon",0,1.08,1.,1.,hp=55,lp=4100,attack=.002,release=.2),L("heavywood",.014,.24,.1,.72,lp=1900)]),
        "battle_impact": (29760,"Hull impact with a splintering wood layer",[L("hit",0,.62,.75,hp=65,lp=3700,attack=.002,release=.14),L("plank",.014,.55,.38,.9,lp=3200)]),
        "battle_melee": (32641,"Cloth movement followed by a low metal weapon contact",[L("cloth",0,.28,.5,1.1,hp=220),L("metal",.11,.43,.45,.67,lp=3800)]),
        "battle_melee_impact": (33600,"Body impact plus brief dull blade contact",[L("punch",0,.5,.86,.9,hp=75,lp=2500),L("iron",.024,.43,.26,.75,lp=3200)]),
        "battle_ram_charge": (39360,"Broadband water swell into timber pressure",[L("water",0,.8,.08,lp=2700,attack=.15,release=.05),L("plank",.16,.62,.38,.7,lp=2000,attack=.14)]),
        "battle_ram_impact": (56640,"Heavy ship collision and visible-hull crunch",[L("ram",0,1.18,.85,hp=58,lp=3500,attack=.002,release=.2),L("plank",.025,.85,.5,.75,lp=3100)]),
        "battle_guard": (24000,"Short shield contact with warm metal resonance",[L("wood",0,.22,.7,.86,lp=3300),L("plate",.01,.46,.33,.76,lp=3000)]),
        "battle_repair": (28800,"Three controlled hammer-on-timber taps",[L("knock",.04,.17,.7,1.,lp=3500),L("knock2",.2,.17,.6,.93,lp=3300),L("wood2",.37,.2,.65,1.05,lp=3300)]),
        "battle_victory": (56640,"Wood confirmation with two soft bell accents",[L("knock",0,.26,.3,.85,lp=3000),L("bell",.06,1.02,.48,.76,lp=2600),L("glass",.36,.38,.25,.65,lp=3400)]),
        "battle_sunk": (68160,"Wood break falling into water; no descending electronic siren",[L("ram",0,.88,.42,.8,hp=65,lp=2700,release=.2),L("water",.2,1.2,.12,lp=3500,attack=.035,release=.4),L("splash",.3,1.12,.34,.9,hp=120,lp=3800,release=.3)]),
    },
    "InterfaceSfx": {
        "course_plot": (16321,"Small map-table tap and muted pen-like friction",[L("carpet",0,.15,.42,1.35,hp=450,lp=3600),L("wood",.045,.23,.7,1.13,lp=3700)]),
        "encounter_choice": (18240,"Single clear warm choice click",[L("wood2",0,.26,.8,1.1,lp=3600),L("soft",.025,.18,.35,.8,lp=2600)]),
        "trade_confirm": (24960,"Small coins/contact rather than high sine chirps",[L("metal",.01,.2,.55,1.25,hp=250,lp=4600),L("metal2",.14,.26,.42,1.35,hp=250,lp=4600),L("wood",.03,.21,.35,.85,lp=3200)]),
        "dispatch_start": (20160,"Paper/table confirmation with outward cloth movement",[L("wood",0,.23,.7,1.05),L("cloth",.075,.33,.42,1.2,hp=220,lp=3400)]),
        "dispatch_recall": (20160,"Two lower soft return knocks",[L("wood2",0,.2,.58,.85,lp=3100),L("wood",.14,.24,.48,.76,lp=3100)]),
        "tavern_recruit": (35520,"Table pat and restrained mug clink",[L("cloth",0,.24,.4,.9,lp=3100),L("wood",.02,.25,.58,.85,lp=3200),L("glass",.22,.37,.31,.72,lp=3400)]),
        "shipyard_equip": (26881,"Timber fixture locks with one iron fastening",[L("knock",0,.25,.7,.88),L("iron",.15,.3,.36,.73,lp=3400)]),
        "treasure_craft": (35520,"Two precise bench taps and a small artifact glint",[L("wood2",0,.18,.55,1.15),L("metal",.17,.25,.4,.91,lp=3500),L("glass2",.36,.31,.3,.78,lp=3400)]),
        "treasure_equip": (23040,"Artifact placed into a wooden slot",[L("wood",0,.27,.62,.88),L("plate",.085,.35,.24,.87,lp=3200)]),
        "chart_draw": (26881,"Short layered dry paper friction and pen stop",[L("carpet",0,.19,.35,.8,hp=350,lp=3000),L("carpet",.14,.2,.32,1.05,hp=350,lp=3200),L("wood2",.31,.2,.4,1.27,lp=3800)]),
        "shop_refresh": (24960,"Cloth shuffle and new goods table placement",[L("cloth",0,.28,.44,1.15,hp=220,lp=3200),L("wood",.18,.27,.54,.96)]),
    },
    "WorldSfx": {
        "sail_depart": (44160,"Rope/timber movement over a short wave wash",[L("plank",0,.65,.35,.77,lp=2400,attack=.015),L("water",.02,.88,.08,lp=3400,attack=.055,release=.22),L("bell",.2,.62,.18,.66,lp=2600)]),
        "sail_step": (22080,"Brief wave wash and quiet deck movement",[L("water",0,.44,.09,lp=3000,attack=.025,release=.15),L("step",.005,.26,.32,.91,lp=2900)]),
        "harbor_arrive": (50400,"Dock timber contact, light wave and a muted harbor bell",[L("heavywood",0,.38,.5,.75,lp=2300),L("water",.01,1.02,.055,lp=2500,release=.3),L("bell",.2,.78,.28,.71,lp=2400)]),
        "encounter_enter": (34560,"Dossier/table movement and a soft attention accent",[L("cloth",0,.3,.38,.92,lp=2900),L("wood2",.025,.29,.55,.88),L("plate",.12,.5,.2,.68,lp=2600)]),
        "settlement_reward": (48000,"Reward chest/table contact with small coin clusters",[L("wood",0,.26,.47,.83),L("metal",.09,.23,.4,1.22,lp=4200),L("metal2",.3,.23,.33,1.32,lp=4300),L("bell",.42,.56,.18,.72,lp=2300)]),
        "settlement_treasure": (59520,"Treasure box opening with longer artifact shimmer",[L("plank",0,.55,.38,.83,lp=2600),L("bell",.24,.97,.33,.72,lp=2700),L("glass",.52,.32,.2,.8,lp=3600),L("metal2",.67,.29,.15,1.12,lp=3600)]),
        "settlement_failure": (40320,"Low closed-box thud and fading wood movement",[L("heavywood",0,.49,.72,.61,lp=2200),L("plank",.12,.65,.3,.66,lp=1700,release=.22)]),
        "dispatch_complete": (36480,"Returning deck footsteps and a small delivery clink",[L("step",0,.27,.45,.97,lp=3100),L("wood",.19,.27,.5,1.1),L("glass2",.36,.3,.23,.71,lp=3500)]),
        "investment_build": (32641,"Three wood construction knocks, final fastening",[L("knock",0,.22,.6,.86),L("knock2",.19,.23,.58,.93),L("heavywood",.4,.26,.53,1.1,lp=3300),L("metal",.44,.2,.14,.81,lp=3200)]),
        "curse_purify": (49920,"Water cleansing movement and a restrained resonant release",[L("water",0,.86,.065,lp=2600,attack=.055,release=.28),L("bell",.25,.75,.3,.69,lp=2400),L("glass",.46,.4,.15,.66,lp=2800)]),
        "milestone_unlock": (48000,"Mechanical wooden latch followed by soft bell confirmation",[L("wood",0,.2,.55,1.1),L("metal",.13,.28,.25,.86,lp=3500),L("bell",.32,.66,.32,.77,lp=2800)]),
    },
}

AMBIENCES = {
    # All details are aperiodic, non-melodic Foley; no oscillator or sub-Hz LFO.
    "harbor_ambience": (35., 3400, .075, [L("plank",1.2,.7,.035,.68,lp=1500),L("step",5.15,.3,.025,.9,lp=2400)]),
    "sailing_ambience": (73., 4100, .09, [L("plank",2.3,.75,.025,.62,lp=1700),L("cloth",5.45,.54,.02,.7,hp=250,lp=2400)]),
    "shipyard_ambience": (110., 2100, .04, [L("knock",.92,.25,.14,.76,lp=2600),L("knock2",1.21,.24,.07,.88,lp=2700),L("plank",3.83,.68,.07,.75,lp=2200),L("knock2",6.34,.28,.11,.86,lp=2400)]),
    "tavern_ambience": (147., 1200, .028, [L("step",.83,.32,.06,.82,lp=1900),L("step2",1.38,.31,.042,.86,lp=1800),L("glass",2.76,.35,.035,.65,lp=2500),L("wood",4.47,.28,.045,.8,lp=1800),L("glass2",6.13,.38,.025,.7,lp=2300)]),
    "market_ambience": (181., 2100, .04, [L("step2",.74,.29,.053,.98,lp=2400),L("wood",2.81,.26,.048,.86,lp=2100),L("metal",3.16,.26,.018,1.15,lp=3400),L("step",5.23,.29,.055,.9,lp=2300),L("cloth",6.55,.41,.023,.91,lp=2000)]),
    "treasure_workbench_ambience": (222., 1150, .022, [L("wood2",.93,.26,.052,.9,lp=2100),L("metal",3.23,.28,.025,.85,lp=2700),L("carpet",4.62,.21,.022,.72,lp=2300),L("plate",6.1,.45,.02,.68,lp=1800)]),
}


def render_layer(palette, layer):
    if layer.material == "water":
        x = palette.water_slice(layer.duration, 18., lp=layer.lp)
        return fades(x, layer.attack, layer.release) * layer.gain
    return palette.tone(layer.material, layer.duration, layer.pitch, layer.hp, layer.lp,
                        layer.attack, layer.release, layer.reverse) * layer.gain


def add_at(buffer, x, start, pan=0.):
    i = round(start * SR)
    x = x[:max(0, len(buffer) - i)]
    if buffer.ndim == 2:
        x = np.column_stack((x * (1 - pan * .25), x * (1 + pan * .25)))
    buffer[i:i+len(x)] += x


def render_sfx(palette, frames, layers):
    x = np.zeros(frames)
    for layer in layers:
        add_at(x, render_layer(palette, layer), layer.start)
    x -= np.mean(x)
    return fades(x, .002, .022)


def render_ambience(palette, start, lp, water_gain, layers):
    n, overlap = 8 * SR, round(.55 * SR)
    x = palette.water_slice(8.55, start, stereo=True, lp=lp) * water_gain
    result = x[:n].copy()
    # Start on the source frame immediately after the end of the loop. Fade
    # that continuation into the beginning; this makes a real continuous seam
    # with no rhythmic volume pulsing or silence at either edge.
    theta = np.linspace(0, np.pi / 2, overlap)[:, None]
    result[:overlap] = x[n:n+overlap] * np.cos(theta) + x[:overlap] * np.sin(theta)
    for i, layer in enumerate(layers):
        add_at(result, render_layer(palette, layer), layer.start, (-1 if i % 2 else 1) * .5)
    result -= result.mean(axis=0)
    return result


def measure(x):
    mono = x.mean(axis=1) if x.ndim == 2 else x
    f, p = signal.welch(mono, SR, nperseg=min(32768, len(mono)))
    total = max(float(p.sum()), 1e-20)
    return dict(lufs=lufs(x), sample_peak_dbfs=db(np.max(np.abs(x))),
                true_peak_4x_dbtp=db(true_peak(x)), rms_dbfs=db(np.sqrt(np.mean(x*x))),
                dc_mean=float(np.max(np.abs(np.mean(x, axis=0)))),
                loop_boundary_sample_step=float(np.max(np.abs(x[-1] - x[0]))),
                power_fraction_below_60hz=float(p[f < 60].sum() / total),
                power_fraction_above_6khz=float(p[f >= 6000].sum() / total),
                correlation=float(np.corrcoef(x.T)[0,1]) if x.ndim == 2 else None)


def align_loop_boundary(x):
    """Choose a quiet natural cut without fading out or altering the waveform.

    The source-continuation crossfade already makes a periodic waveform. A
    water transient can still have a large adjacent-sample slope at its cut.
    Move that cut by at most 2 ms when needed, retaining every sample exactly.
    This is a circular phase shift, not endpoint surgery or a volume dip.
    """
    if np.max(np.abs(x[-1] - x[0])) <= .0025:
        return x
    radius = round(.002 * SR)
    candidates = np.arange(-radius, radius + 1)
    steps = np.max(np.abs(x[candidates % len(x)] - x[(candidates - 1) % len(x)]), axis=1)
    best = int(candidates[np.argmin(steps)])
    return np.roll(x, -best, axis=0)


def compare(report, archive_path):
    if not archive_path.is_file():
        return
    events = [(0.,"BattleSfx","battle_card_play"),(.62,"BattleSfx","battle_cannon_fire"),
              (2.,"BattleSfx","battle_guard"),(2.8,"BattleSfx","battle_repair"),
              (3.7,"InterfaceSfx","trade_confirm"),(4.45,"WorldSfx","settlement_reward")]
    length = round(5.5 * SR)
    before, after = np.zeros(length), np.zeros(length)
    with zipfile.ZipFile(archive_path) as archive:
        for onset, category, name in events:
            relative = f"Assets/_Project/Resources/Audio/{category}/{name}.wav"
            old, sr = sf.read(io.BytesIO(archive.read(relative)), dtype="float64")
            new, _ = sf.read(AUDIO/category/(name+".wav"), dtype="float64")
            add_at(before, rate_convert(old, sr), onset)
            add_at(after, new, onset)
        before, after = master(before,-24.,-8.), master(after,-24.,-8.)
        pair = np.concatenate((before,np.zeros(SR),after))
        sf.write(report/"sfx-timbre-before-after-12s.wav", pair, SR, subtype="PCM_16")
        relative="Assets/_Project/Resources/Audio/Ambience/harbor_ambience.wav"
        old, sr = sf.read(io.BytesIO(archive.read(relative)), dtype="float64", always_2d=True)
        old = rate_convert(old, sr)[:length]
        old = np.repeat(old,2,axis=1) if old.shape[1] == 1 else old
        new, _ = sf.read(AUDIO/"Ambience/harbor_ambience.wav", dtype="float64",always_2d=True)
        old, new = master(fades(old,.03,.08),-32.,-12.), master(fades(new[:length],.03,.08),-32.,-12.)
        sf.write(report/"ambience-timbre-before-after-12s.wav",
                 np.concatenate((old,np.zeros((SR,2)),new)),SR,subtype="PCM_16")
    (report/"sfx-comparison-guide.md").write_text(
        "# 音效对比\n\n未进行本机听觉试听。两份文件均为12秒，0–5.5秒修复前，5.5–6.5秒留白，6.5–12秒修复后。\n\n"
        "sfx-timbre-before-after-12s.wav：两半均约-24 LUFS，峰值不超过-8 dBFS，以便比较音色而非单纯音量。"
        "每半相对0秒出牌、0.62秒开炮、2秒格挡、2.8秒修理、3.7秒交易、4.45秒奖励。\n\n"
        "ambience-timbre-before-after-12s.wav：港口前后各约-32 LUFS，前版额外受-12 dBFS峰值保护；旧版低频纯音与自然水声可直接对照。"
        "预览文件经过独立响度匹配，非运行时最终混音。\n",encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fetch",action="store_true",help="Fetch only allowlisted, SHA-pinned CC0 sources when missing")
    parser.add_argument("--group",choices=["all","battle","nonbattle"],default="all")
    parser.add_argument("--only",choices=sorted(set(AMBIENCES).union(*(set(v) for v in SPECS.values()))),
                        help="Regenerate only this existing resource; never changes other WAVs")
    parser.add_argument("--report-dir",type=Path,default=DEFAULT_REPORT)
    parser.add_argument("--compare",action="store_true",help="Create 12s matched comparison previews using source-before.zip")
    args=parser.parse_args()
    provenance=prepare_sources(args.fetch)
    palette=Palette()
    args.report_dir.mkdir(parents=True,exist_ok=True)
    rendered=[]
    expected={category: set(specs) for category,specs in SPECS.items()}
    expected["Ambience"]=set(AMBIENCES)
    for category,names in expected.items():
        actual={p.stem for p in (AUDIO/category).glob("*.wav")}
        if actual != names:
            raise RuntimeError(f"Event inventory changed in {category}; do not silently add/remove events: {actual ^ names}")
    categories=list(SPECS)
    if args.group=="battle": categories=["BattleSfx"]
    if args.group=="nonbattle": categories=["WorldSfx","InterfaceSfx"]
    for category in categories:
        for name,(frames,description,layers) in SPECS[category].items():
            if args.only and name != args.only:
                continue
            target=-25.5 if category=="InterfaceSfx" else -24.5
            if name in ("battle_cannon_fire","battle_impact","battle_ram_impact","battle_melee_impact"):
                target=-23.
            ceiling=-6. if name=="battle_cannon_fire" else -8.
            raw=render_sfx(palette,frames,layers)
            x=master(raw,target,ceiling)
            path=AUDIO/category/(name+".wav")
            sf.write(path,x,SR,subtype="PCM_16")
            written,rate=sf.read(path,dtype="float64")
            stats=measure(written)
            ok=(len(written)==frames and rate==SR and written.ndim==1
                and -27<=stats["lufs"]<=-22 and stats["true_peak_4x_dbtp"]<=ceiling+.02
                and stats["dc_mean"]<.001 and np.all(np.isfinite(written)))
            rendered.append(dict(name=name,category=category,frames=frames,duration_seconds=frames/SR,
                                 sample_rate=rate,channels=1,sha256=sha(path.read_bytes()),
                                 design=description,layers=[layer.__dict__ for layer in layers],
                                 target_lufs=target,peak_ceiling=ceiling,passed=bool(ok),**stats))
    if args.group!="battle":
        for name,(start,lp,gain,layers) in AMBIENCES.items():
            if args.only and name != args.only:
                continue
            x=master(render_ambience(palette,start,lp,gain,layers),-32.,-12.)
            x=align_loop_boundary(x)
            path=AUDIO/"Ambience"/(name+".wav")
            sf.write(path,x,SR,subtype="PCM_16")
            written,rate=sf.read(path,dtype="float64",always_2d=True)
            stats=measure(written)
            ok=(len(written)==SR*8 and rate==SR and written.shape[1]==2
                and abs(stats["lufs"]+32)<.3 and stats["true_peak_4x_dbtp"]<=-12+.02
                and stats["power_fraction_below_60hz"]<.02 and stats["dc_mean"]<.001
                and stats["loop_boundary_sample_step"]<.003 and np.all(np.isfinite(written)))
            rendered.append(dict(name=name,category="Ambience",frames=SR*8,duration_seconds=8.,
                                 sample_rate=rate,channels=2,sha256=sha(path.read_bytes()),
                                 design="Natural river wash with sparse material Foley, restrained stereo, no oscillators",
                                 water_source_offset=start,layers=[layer.__dict__ for layer in layers],
                                 target_lufs=-32.,peak_ceiling=-12.,passed=bool(ok),**stats))
    report=dict(schema=1,group=args.group,perceptual_listening_performed=False,
                measurement="BS.1770 integrated loudness (400ms zero-padded only for short cues), 4x oversampled true-peak estimate; PCM16 readback. These are file metrics, not final Unity runtime loudness.",
                versions=dict(python=platform.python_version(),numpy=np.__version__,scipy=scipy.__version__,
                              soundfile=sf.__version__),
                source_manifest=str(SAMPLES/"manifest.json"),rendered_count=len(rendered),
                passed_count=sum(x["passed"] for x in rendered),files=rendered)
    report_name="sfx-remaster-report.json" if args.group=="all" else f"sfx-remaster-report-{args.group}.json"
    if args.only:
        report_name=f"sfx-remaster-report-{args.only}.json"
    write_json(args.report_dir/report_name,report)
    if args.compare and args.group=="all" and not args.only: compare(args.report_dir,args.report_dir/"source-before.zip")
    print(json.dumps({"rendered":len(rendered),"passed":report["passed_count"],"report":str(args.report_dir/report_name),
                      "failed":[dict(name=x["name"],lufs=x["lufs"],peak=x["true_peak_4x_dbtp"]) for x in rendered if not x["passed"]]},indent=2))
    return 0 if report["passed_count"]==len(rendered) else 2


if __name__=="__main__":
    sys.exit(main())
