"""Pack authored RGBA sheets into eight-frame strips. Only crop, size and format conversion."""
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/boarding-remaster-20261106/effects"
RES = ROOT / "UnityClient/Assets/_Project/Resources/BattleVfx/BoardingRemaster"
ROWS = [
    ("blade", "slash", "dash"), ("parry", "guard", "guard-break"),
    ("support", "heal", "defeat-dust"), ("flame_arcane", "fire", "arcane"),
    ("curse_tide", "curse", "tide"), ("storm_blood", "thunder", "blood")
]
CELL = 256


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    original = json.loads((OUT / "generated-sources.json").read_text(encoding="utf-8"))
    inputs, preview = OUT / "sources", OUT / "previews"
    for folder in (inputs, preview, RES):
        folder.mkdir(parents=True, exist_ok=True)
    packed = Image.new("RGBA", (8 * CELL, 12 * CELL))
    review = Image.new("RGB", (8 * 112, 12 * 112), "#17303a")
    all_frames = []
    report = {"rows": [], "source": "built-in image_gen", "cell": CELL, "framesPerRow": 8}
    for sheet_index, (name, first, second) in enumerate(ROWS):
        path = inputs / (name + ".png")
        if not path.is_file():
            shutil.copyfile(original[name], path)
        image = Image.open(path)
        if image.mode != "RGBA":
            raise ValueError(f"{name}: expected transparent source, got {image.mode}")
        for half, effect in enumerate((first, second)):
            row = sheet_index * 2 + half
            frames, hashes, peaks = [], [], []
            for i in range(8):
                x, y = i % 4, 2 * half + i // 4
                # Independently round grid lines to support odd source dimensions.
                box = (round(x * image.width / 4), round(y * image.height / 4),
                       round((x + 1) * image.width / 4), round((y + 1) * image.height / 4))
                frame = image.crop(box)
                amin, amax = frame.getchannel("A").getextrema()
                if amin > 2 or amax < 12:
                    raise ValueError(f"{name}/{effect}/{i}: suspicious alpha {amin}, {amax}")
                peaks.append(amax)
                frame.thumbnail((CELL - 16, CELL - 16), Image.Resampling.LANCZOS)
                tile = Image.new("RGBA", (CELL, CELL))
                tile.paste(frame, ((CELL - frame.width) // 2, (CELL - frame.height) // 2))
                hashes.append(hashlib.sha256(tile.tobytes()).hexdigest())
                packed.paste(tile, (i * CELL, row * CELL))
                bg = Image.new("RGBA", (CELL, CELL), "#17303a")
                bg.alpha_composite(tile)
                frames.append(bg.convert("RGB"))
                review.paste(bg.convert("RGB").resize((112,112), Image.Resampling.LANCZOS), (i*112,row*112))
            if len(set(hashes)) != 8:
                raise ValueError(f"{effect}: duplicate frames")
            all_frames.append(frames)
            report["rows"].append({"name":effect,"row":row,"sheet":path.name,"sourceSha256":sha(path),
                                   "sourceSize":image.size,"frameAlphaPeak":peaks,"frameHashes":hashes})
    output = RES / "boarding_fx_v3.png"
    packed.save(output, optimize=True)
    report["atlas"] = {"size":packed.size,"path":str(output),"sha256":sha(output),"bytes":output.stat().st_size}
    review.save(preview / "all-frames.jpg", quality=90)
    # This review plays the unmodified frames on an opaque deck-tone backdrop for legibility.
    showcase = []
    for frame_index in range(8):
        frame = Image.new("RGB", (4 * 184, 3 * 208), "#172f36")
        draw = ImageDraw.Draw(frame)
        for row, frames in enumerate(all_frames):
            x, y = row % 4 * 184, row // 4 * 208
            frame.paste(frames[frame_index].resize((184,184), Image.Resampling.LANCZOS), (x, y))
            draw.text((x+10, y+188), report["rows"][row]["name"].upper(), fill="#ead9b6")
        showcase.append(frame)
    gif = preview / "boarding-effects.gif"
    showcase[0].save(gif, save_all=True, append_images=showcase[1:], duration=[80,75,75,90,90,90,100,220],
                     disposal=1, loop=0)
    with Image.open(gif) as check:
        assert check.n_frames == 8
        for i in range(8):
            check.seek(i)
            check.load()
    (OUT / "art-validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report["atlas"]))


if __name__ == "__main__":
    main()
