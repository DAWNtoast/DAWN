"""Crop verified Unity captures into a compact comparison GIF; no image generation."""
from pathlib import Path
import hashlib
import json
import subprocess

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "outputs/naval-vfx-remaster-20261105/validation/capture20261106"
DEST = ROOT / "outputs/naval-vfx-remaster-20261105/delivery-preview"
FFMPEG = ROOT / "tmp/boarding-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe"
CASES = [
    ("00_Cannon_Neutral", "炮击命中"),
    ("01_Cannon_Neutral", "炮击格挡"),
    ("02_Cannon_Neutral", "炮弹落水"),
    ("03_Cannon_Fire", "烈火"),
    ("04_Strike_Arcane", "奥术"),
    ("05_Strike_Curse", "咒术"),
    ("06_Cannon_Tide", "潮汐"),
    ("07_Strike_Thunder", "雷霆"),
    ("08_Cannon_Blood", "血术"),
]
WIDTH, HEIGHT, LABEL, FPS = 384, 240, 32, 20


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 20)
    videos = []
    inputs = []
    for stem, label in CASES:
        source = SOURCE / (stem + ".mp4")
        result = subprocess.run(
            [str(FFMPEG), "-v", "error", "-i", str(source), "-vf",
             f"fps={FPS},crop=640:400:640:180,scale={WIDTH}:{HEIGHT}:flags=lanczos",
             "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"],
            check=True, capture_output=True,
        )
        stride = WIDTH * HEIGHT * 3
        assert result.stdout and len(result.stdout) % stride == 0, source
        frames = [Image.frombytes("RGB", (WIDTH, HEIGHT), result.stdout[i:i + stride])
                  for i in range(0, len(result.stdout), stride)]
        videos.append(frames)
        inputs.append({"path": str(source), "sha256": digest(source), "frames": len(frames)})
    composites = []
    for frame_index in range(max(map(len, videos))):
        canvas = Image.new("RGB", (WIDTH * 3, (HEIGHT + LABEL) * 3), "#092b36")
        draw = ImageDraw.Draw(canvas)
        for case_index, ((_, label), frames) in enumerate(zip(CASES, videos)):
            x, y = (case_index % 3) * WIDTH, (case_index // 3) * (HEIGHT + LABEL)
            canvas.paste(frames[min(frame_index, len(frames) - 1)], (x, y + LABEL))
            draw.text((x + 12, y + 3), label, font=font, fill="#e9ddbc")
        composites.append(canvas)
    # Derive one palette from the whole timeline to avoid per-frame color flicker.
    palette_samples = Image.new("RGB", (384, 272 * len(composites)))
    for i, frame in enumerate(composites):
        palette_samples.paste(frame.resize((384, 272)), (0, i * 272))
    palette = palette_samples.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    quantized = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in composites]
    gif = DEST / "海战特效局部对比.gif"
    durations = [round(1000 / FPS)] * len(quantized)
    durations[0] += 150
    durations[-1] += 350
    quantized[0].save(gif, save_all=True, append_images=quantized[1:],
                      duration=durations, loop=0, optimize=False, disposal=1)
    composites[min(round(0.5 * FPS), len(composites) - 1)].save(DEST / "海战特效局部对比.jpg", quality=93)
    with Image.open(gif) as verified:
        count = verified.n_frames
        assert count > 1
        for i in range(count):
            verified.seek(i)
            verified.load()
    report = {
        "description": "Cropped and resized production Unity visual timeline captures; not native model-generated GIF.",
        "crop": {"x": 640, "y": 180, "width": 640, "height": 400},
        "fps": FPS, "frames": count, "inputs": inputs,
        "output": str(gif), "sha256": digest(gif), "bytes": gif.stat().st_size,
        "temporaryFilesCreated": 0,
    }
    (DEST / "preview-manifest.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"gif": str(gif), "frames": count, "bytes": gif.stat().st_size}, ensure_ascii=False))


if __name__ == "__main__":
    main()
