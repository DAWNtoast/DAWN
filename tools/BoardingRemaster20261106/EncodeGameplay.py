"""Encode live Unity button-input clips with their recorded frame times."""
import argparse
import json
import re
import shutil
import subprocess
from pathlib import Path

from PIL import Image
from EncodeNamed import safe_dir, digest


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    parser.add_argument("--remove-verified-frames", action="store_true")
    parser.add_argument("--boss-four-actions", action="store_true")
    args = parser.parse_args()
    if not re.fullmatch(r"[a-zA-Z0-9_-]+", args.run):
        raise ValueError("Unexpected run name")
    workspace = Path(__file__).resolve().parents[2]
    task = "boarding-boss-actions-20261106" if args.boss_four_actions else "boarding-remaster-20261106"
    root = workspace / "outputs" / task / "validation"
    source = root / args.run / "liveFrames"
    out = root / "gameplay"
    safe_dir(source, root)
    safe_dir(out, root)
    out.mkdir(parents=True, exist_ok=True)
    ffmpeg = workspace / "tmp/boarding-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe"
    report = json.loads((source / "live-report.json").read_text(encoding="utf-8-sig"))
    expected = 4 if args.boss_four_actions else 15
    if len(report["clips"]) != expected or len({c["id"] for c in report["clips"]}) != expected:
        raise ValueError("Incomplete live action capture")
    clips, processed = [], {}
    for clip in report["clips"]:
        ident = clip["id"]
        if not re.fullmatch("[a-z_]+", ident):
            raise ValueError("Unexpected clip ID")
        frames = [source / name for name in clip["frames"]]
        times = clip["seconds"]
        if len(frames) != len(times) or len(frames) < 4:
            raise ValueError(ident + " has too few frames")
        playlist = source / (ident + ".ffconcat")
        safe_dir(playlist, source)
        rows = ["ffconcat version 1.0"]
        for i, frame in enumerate(frames):
            if frame.name != f"{ident}_{i:03d}.jpg" or frame.parent != source:
                raise ValueError("Unexpected frame name")
            safe_dir(frame, source)
            with Image.open(frame) as image:
                if image.size != (report["width"], report["height"]):
                    raise ValueError("Invalid frame dimensions")
            processed[frame] = (frame.stat().st_size, digest(frame))
            duration = max(.025, times[i+1] - times[i]) if i + 1 < len(times) else .2
            rows.extend(["file '" + frame.name + "'", "duration " + f"{duration:.5f}"])
        rows.append("file '" + frames[-1].name + "'")
        playlist.write_text("\n".join(rows), encoding="ascii")
        video = out / (ident + ".mp4")
        safe_dir(video, out)
        subprocess.run([str(ffmpeg), "-nostdin", "-hide_banner", "-v", "error", "-y", "-f", "concat", "-safe", "1",
            "-i", str(playlist), "-vf", "fps=24", "-c:v", "libx264", "-preset", "fast", "-crf", "18", "-pix_fmt", "yuv420p",
            "-movflags", "+faststart", str(video)], check=True)
        subprocess.run([str(ffmpeg), "-nostdin", "-v", "error", "-xerror", "-i", str(video), "-f", "null", "-"], check=True)
        marked = [i for i, state in enumerate(clip["frameActions"]) if state]
        if not marked:
            raise ValueError(f"Missing visible actor action in {ident}")
        index = marked[len(marked)//2]
        if args.boss_four_actions:
            state = {"iron_hit":"enemy_iron_oath/Hit/", "treasure_guard":"enemy_treasure/Guard/",
                     "widow_attack":"enemy_red_widow/Attack/", "moon_cast":"enemy_moon_weave/Cast/"}[ident]
            moments=[]
            peak=66 if ident=="moon_cast" else 55 if ident=="widow_attack" else 51
            for i, states in enumerate(clip["frameActions"]):
                pose=next((part for part in states.split() if part.startswith(state)),None)
                if pose is None:
                    if moments: break
                    continue
                moments.append((abs(int(pose.split("/")[-1])-peak),i))
            if not moments: raise ValueError("Live output has no sampled boss action: "+state)
            index=min(moments)[1]
        if "death" in ident:
            deaths = [i for i, state in enumerate(clip["frameActions"]) if "/Death/" in state]
            if not deaths:
                raise ValueError(f"No recorded death in {ident}")
            index = deaths[len(deaths)//2]
        poster = out / (ident + ".jpg")
        safe_dir(poster, out)
        shutil.copyfile(frames[index], poster)
        clips.append({"id":ident, "title":clip["title"], "video":video.name, "poster":poster.name,
            "input":clip["input"], "sourceCard":clip["sourceCard"], "sourceFrames":len(frames),
            "observedActions":clip["observedActions"], "sha256":digest(video)})
        playlist.unlink()
    (out / "manifest.json").write_text(json.dumps({"scope":report["scope"], "clips":clips}, ensure_ascii=False, indent=2), encoding="utf8")
    freed = 0
    if args.remove_verified_frames:
        for frame, (size, expected) in processed.items():
            safe_dir(frame, source)
            if digest(frame) != expected:
                raise ValueError("Frame changed since encoding")
            frame.unlink()
            freed += size
    (source / "encode-report.json").write_text(json.dumps({"releasedBytes":freed, "decodedVideos":len(clips),
        "frames":[{"name":f.name, "sha256":info[1]} for f, info in processed.items()]}, indent=2), encoding="utf8")
    print(f"PASS {len(clips)} decoded MP4; {len(processed)} frames; released {freed} bytes")


if __name__ == "__main__":
    main()
