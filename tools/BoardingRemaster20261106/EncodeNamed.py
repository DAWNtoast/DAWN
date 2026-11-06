"""Encode the 13 production enemy rigs sampled by Capture.cs and verify every GIF frame."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from PIL import Image


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def safe_dir(path, root):
    if not path.is_relative_to(root) or path.resolve() != path:
        raise ValueError(f"Unexpected preview path: {path}")
    for part in (path, *path.parents):
        if part.is_symlink() or getattr(part, "is_junction", lambda: False)():
            raise ValueError(f"Linked preview path: {part}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    parser.add_argument("--remove-verified-sheets", action="store_true")
    parser.add_argument("--deaths-only", action="store_true")
    args = parser.parse_args()
    if not re.fullmatch(r"[a-zA-Z0-9_-]+", args.run):
        raise ValueError("Invalid capture run name")
    root = Path(__file__).resolve().parents[2] / "outputs/boarding-remaster-20261106/validation"
    source, output = root / args.run, root / "named-enemies"
    safe_dir(source, root)
    safe_dir(output, root)
    report = json.loads((source / "capture-report.json").read_text(encoding="utf-8-sig"))
    if len(report["clips"]) != (13 if args.deaths_only else 65) or len({c["identity"] for c in report["clips"]}) != 13:
        raise ValueError("Incomplete production enemy capture")
    if args.deaths_only and any(c["action"] != "Death" for c in report["clips"]):
        raise ValueError("Unexpected action in updated death run")
    frames, cols, width, height = [report[k] for k in ("frames", "columns", "width", "height")]
    clips = []
    for clip in report["clips"]:
        identity, action = clip["identity"], clip["action"]
        if not re.fullmatch(r"enemy_[a-z_]+", identity) or action not in {"Attack","Cast","Hit","Guard","Death"}:
            raise ValueError("Unexpected identity/action")
        sheet_path = source / clip["sheet"]
        if sheet_path != source / f"{identity}_{action}.png":
            raise ValueError("Sheet filename does not match the declared action")
        safe_dir(sheet_path, source)
        folder = output / identity
        safe_dir(folder, root)
        folder.mkdir(parents=True, exist_ok=True)
        with Image.open(sheet_path) as image:
            sheet = image.convert("RGB")
        if sheet.size != (width * cols, height * (frames // cols)):
            raise ValueError(f"Bad sheet size: {sheet_path}")
        pieces = []
        for i in range(frames):
            x, y = i % cols * width, i // cols * height
            pieces.append(sheet.crop((x, y, x+width, y+height)))
        if len({hashlib.sha256(p.tobytes()).digest() for p in pieces}) < 3:
            raise ValueError(f"No rendered action sequence: {sheet_path}")
        # One common palette per action preserves the backdrop between frames.
        palette = sheet.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
        quantized = [p.quantize(palette=palette, dither=Image.Dither.NONE) for p in pieces]
        path = folder / (action + ".gif")
        safe_dir(path, root)
        duration = max(30, round(1000 * clip["duration"] / (frames - 1) / 10) * 10)
        durations = [duration] * frames
        durations[0] += 140
        durations[-1] += 550 if action == "Death" else 220
        quantized[0].save(path, save_all=True, append_images=quantized[1:],
            duration=durations, loop=0, optimize=False, disposal=1)
        with Image.open(path) as verified:
            decoded = verified.n_frames
            if decoded < 3 or verified.size != (width, height):
                raise ValueError(f"GIF lost its action: {path}")
            for i in range(decoded):
                verified.seek(i)
                verified.load()
            verified.seek(0)
            if len(verified.convert("RGB").resize((48, 32)).getcolors(1536)) < 15:
                raise ValueError(f"Blank render: {path}")
        clips.append({**clip, "renderRun":args.run, "gif": path.relative_to(output).as_posix(), "sha256":digest(path),
                      "sheetSha256":digest(sheet_path), "sheetBytes":sheet_path.stat().st_size, "decodedFrames":decoded})
    merged = list(clips)
    if args.deaths_only:
        previous = json.loads((output / "manifest.json").read_text(encoding="utf8"))["clips"]
        for old in previous:
            if old["action"] == "Death":
                continue
            safe_dir(output / old["gif"], output)
            if digest(output / old["gif"]) != old["sha256"]:
                raise ValueError("An existing non-death GIF needs revalidation")
            merged.append(old)
    if len({c["gif"] for c in merged}) != 65:
        raise ValueError("Duplicate output path")
    manifest = {"scope":report["scope"], "frames":frames, "width":width,"height":height,
                "clips":sorted(merged, key=lambda c:(c["identity"], c["action"]))}
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf8")
    freed = 0
    if args.remove_verified_sheets:
        for clip in clips:
            sheet_path = source / clip["sheet"]
            safe_dir(sheet_path, source)
            if digest(output / clip["gif"]) != clip["sha256"] or digest(sheet_path) != clip["sheetSha256"]:
                raise ValueError("Input or output changed after decoding")
            sheet_path.unlink()
            freed += clip["sheetBytes"]
    (source / "named-gif-cleanup.json").write_text(json.dumps({"decodedGifs":len(clips),
        "releasedBytes":freed, "mainProjectTouched":False}, indent=2), encoding="utf8")
    print(f"PASS {len(clips)} decoded GIFs; released {freed} bytes")


if __name__ == "__main__":
    main()
