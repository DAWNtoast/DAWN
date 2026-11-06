"""Pack built-in ImageGen poses into the production atlas contract and transparent GIFs."""
import argparse
import hashlib
import json
import re
import shutil
import uuid
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/boarding-remaster-20261106/actors"
INSTALLED = ROOT / "UnityClient/Assets/_Project/Resources/BoardingBattle/Actions"
PRIOR = ROOT / "outputs/captain-action-gifs-20261105"
META = ROOT / "UnityClient/Assets/_Project/Resources/BattleVfx/NavalV2/naval_fx_remaster_v2.png.meta"
SIZE, GROUND = 256, 230
ACTIONS = ("Attack", "Cast", "Hit", "Guard", "Death")
DURATIONS = ((140, 90, 70, 65, 95, 260), (140, 120, 130, 110, 100, 250),
             (145, 55, 75, 110, 115, 260), (135, 80, 85, 150, 90, 240),
             (160, 110, 150, 150, 180, 600))


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def lines(occupancy, count):
    # Locate real transparent gutters near the nominal grid, including slightly uneven rows.
    length = len(occupancy)
    boundaries = [0]
    for i in range(1, count):
        center = length * i / count
        radius = int(length / count * .24)
        lo, hi = round(center) - radius, round(center) + radius
        local = occupancy[lo:hi+1]
        best = np.flatnonzero(local == local.min()) + lo
        boundaries.append(int(best[np.argmin(np.abs(best - center))]))
    return boundaries + [length]


def trim(im):
    # Use the existing matte only to locate the silhouette. Preserve RGBA inside its crop.
    alpha = im.getchannel("A")
    box = alpha.point(lambda v: 255 if v >= 65 else 0).getbbox()
    if not box:
        raise ValueError("Empty source frame")
    x, y, r, b = box
    return im.crop((max(0,x-2), max(0,y-2), min(im.width,r+2), min(im.height,b+2)))


def split_rows(image, count):
    alpha = np.asarray(image.getchannel("A"))
    if np.percentile(alpha, 15) > 12:
        raise ValueError("Source matte has a visible backdrop; request a clean cutout")
    solid = alpha > 90
    ys = lines(solid.sum(1), count)
    result = []
    for top, bottom in zip(ys, ys[1:]):
        xs = lines(solid[top:bottom].sum(0), 4)
        row = [trim(image.crop((x, top, right, bottom))) for x, right in zip(xs,xs[1:])]
        result.append(row)
    return result


def anchor_frame(im, scale, falling=False):
    width, height = max(1,round(im.width*scale)), max(1,round(im.height*scale))
    if width > SIZE-4 or height > GROUND-2:
        raise ValueError(("Oversize frame", width,height))
    resized = im.resize((width,height), Image.Resampling.LANCZOS)
    a = np.asarray(resized.getchannel("A"))
    feet = np.where(a[round(height*.88):] > 90)[1]
    anchor = width / 2 if falling or not len(feet) else (np.percentile(feet, 4)+np.percentile(feet, 96))/2
    left = int(np.clip(round(SIZE*.5-anchor), 2, SIZE-width-2))
    result = Image.new("RGBA", (SIZE,SIZE))
    result.alpha_composite(resized,(left,GROUND-height))
    return result


def make_rows(identity, source):
    with Image.open(source) as input_image:
        im = input_image.convert("RGBA")
    src = split_rows(im, 2 if identity == "captain" else 5)
    standing = [f.height for row in (src[:1] if identity=="captain" else src[:4]) for f in row]
    scale = min(205 / np.median(standing), (SIZE-6)/max(f.width for row in src for f in row),
                (GROUND-3)/max(f.height for row in src for f in row))
    aligned = [[anchor_frame(f, scale, (index == len(src)-1 and j>=2))
                for j,f in enumerate(row)] for index,row in enumerate(src)]
    if identity == "captain":
        # Reuse the existing identity and authored captain swing/cast/recoil.
        original = {}
        indices = [0,2,4,6,9,11]
        neutral = trim(Image.open(PRIOR / "captain-neutral.png").convert("RGBA"))
        templates = {a:[trim(Image.open(PRIOR/"frames"/a.lower()/f"{n:02d}.png").convert("RGBA")) for n in indices]
                     for a in ACTIONS[:3]}
        prior_scale = min(205/neutral.height, (SIZE-6)/max(f.width for row in templates.values() for f in row),
                          (GROUND-3)/max(f.height for row in templates.values() for f in row))
        for action, row in templates.items():
            original[action] = [anchor_frame(f, prior_scale) for f in row]
        idle = original["Attack"][0]
        originals = list(original.values())
        originals.extend(([idle, *aligned[0], idle], [idle, *aligned[1], aligned[1][-1]]))
        return originals
    idle = aligned[0][-1]
    return [[idle,*row,(row[-1] if i==4 else idle)] for i,row in enumerate(aligned)]


def transparent_gif(frames, target, durations):
    sample = Image.new("RGB",(SIZE*len(frames),SIZE),"#293540")
    for n,frame in enumerate(frames):
        sample.paste(frame,(n*SIZE,0),frame)
    palette = sample.quantize(colors=255, method=Image.Quantize.MEDIANCUT)
    palette.putpalette(palette.getpalette()[:765]+[0,0,0])
    encoded=[]
    for frame in frames:
        colors = np.array(frame.convert("RGB").quantize(palette=palette,dither=Image.Dither.NONE))
        colors[colors==255] = 0
        colors[np.array(frame.getchannel("A"))<96] = 255
        out = Image.frombytes("P",(SIZE,SIZE),colors.tobytes())
        out.putpalette(palette.getpalette())
        encoded.append(out)
    encoded[0].save(target, save_all=True, append_images=encoded[1:], duration=durations,
                    loop=0, disposal=2, transparency=255, optimize=False)
    with Image.open(target) as gif:
        if gif.n_frames < 4:
            raise ValueError(("Insufficient GIF frames", target))
        for i in range(gif.n_frames):
            gif.seek(i)
            gif.load()


def visible_sole(frame):
    solid = np.asarray(frame.getchannel("A")) >= 64
    def supports_weight(line):
        if line.sum() < 8:
            return False
        edges = np.flatnonzero(np.diff(np.pad(line.astype(int), (1,1))))
        return bool(len(edges) and np.max(edges[1::2] - edges[::2]) >= 6)
    supported = [supports_weight(line) for line in solid]
    return next(y for y in range(SIZE-1, 1, -1) if all(supported[y-2:y+1]))


def place_on_deck(rows):
    # Preserve relative pose motion while pinning the same contact patch for every identity.
    dy = GROUND - visible_sole(rows[0][0])
    for row in rows:
        for i, frame in enumerate(row):
            bounds = frame.getchannel("A").getbbox()
            if bounds[1]+dy < 0 or bounds[3]+dy > SIZE:
                raise ValueError("Foot calibration would clip the silhouette")
            placed = Image.new("RGBA",frame.size)
            placed.paste(frame,(0,dy))
            row[i]=placed
    assert visible_sole(rows[0][0]) == GROUND
    return dy


def meta_file(path, template):
    target = Path(str(path)+".meta")
    guid = uuid.uuid5(uuid.NAMESPACE_URL, path.relative_to(ROOT).as_posix()).hex
    if target.exists() and ("guid: "+guid) not in target.read_text(encoding="utf-8-sig"):
        return
    text = re.sub(r"(?m)^guid: .+$","guid: "+guid,template)
    if path.suffix == ".png":
        text = re.sub(r"(?m)^  userData:.*$", "  userData: Boarding actor; 6 columns x 5 actions; RGBA.",text)
    if not target.exists() or target.read_text(encoding="utf-8-sig") != text:
        target.write_text(text, encoding="utf-8")


def build(record):
    key = record["key"]
    source_dir = OUT / "sources"
    source_dir.mkdir(exist_ok=True)
    source = source_dir / (key+".png")
    generated = Path(record["source"])
    if generated.is_file() and (not source.exists() or digest(generated) != digest(source)):
        shutil.copyfile(generated, source)
    rows = make_rows(key, source)
    sole_offset = place_on_deck(rows)
    atlas = Image.new("RGBA",(SIZE*6,SIZE*5))
    gifs = OUT / "gifs" / key
    gifs.mkdir(parents=True,exist_ok=True)
    for i, row in enumerate(rows):
        for j,frame in enumerate(row):
            atlas.alpha_composite(frame,(j*SIZE,i*SIZE))
        transparent_gif(row,gifs/(ACTIONS[i]+".gif"),DURATIONS[i])
    image_path = INSTALLED / (key+".png")
    atlas.save(image_path)
    meta_file(image_path,META.read_text(encoding="utf-8"))
    return {"key":key,"sourceSha256":digest(source),"atlasSha256":digest(image_path),
            "idleSoleY":GROUND,"uniformYOffset":sole_offset,
            "atlasBytes":image_path.stat().st_size,"gifs":[str((gifs/(a+".gif")).relative_to(OUT)) for a in ACTIONS]}


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--available", action="store_true")
    parser.add_argument("--only",nargs="*")
    args=parser.parse_args()
    INSTALLED.mkdir(parents=True,exist_ok=True)
    meta_file(INSTALLED,Path(str(INSTALLED.parent/"Characters")+".meta").read_text(encoding="utf-8"))
    files=sorted((OUT/"generation").glob("*.json"))
    if not args.available and not args.only and len(files)!=19:
        raise ValueError(f"Need 19 identities; have {len(files)}")
    results=[]
    for file in files:
        record=read(file)
        if args.only and record["key"] not in args.only:
            continue
        if record["key"]=="captain" and (OUT/"captain-matte-revision.json").exists():
            record=read(OUT/"captain-matte-revision.json")
        revision = OUT/"revisions"/(record["key"]+".json")
        if revision.exists():
            record=read(revision)
        if args.available:
            try:
                results.append(build(record))
            except ValueError as error:
                print("Awaiting visual review:", record["key"],error,flush=True)
            continue
        results.append(build(record))
        print(record["key"],flush=True)
    manifest=OUT/"art-validation.json"
    if args.only and manifest.exists():
        by_key={record["key"]:record for record in read(manifest)}
        by_key.update({record["key"]:record for record in results})
        results=[by_key[key] for key in sorted(by_key)]
    manifest.write_text(json.dumps(results,indent=2),encoding="utf-8")


if __name__ == "__main__":
    main()
