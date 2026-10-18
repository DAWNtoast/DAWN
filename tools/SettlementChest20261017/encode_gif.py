from pathlib import Path
from PIL import Image, ImageOps
import argparse, json

parser = argparse.ArgumentParser(description='Encode local deterministic Unity frames into a shareable animation.')
parser.add_argument('capture_dir', type=Path)
args = parser.parse_args()
root = args.capture_dir.resolve()
assert str(root).lower().startswith(str(Path('D:/Game/Artifacts/SettlementChest20261017').resolve()).lower())
report = json.loads((root / 'report.json').read_text(encoding='utf-8-sig'))
frames = []
for path in sorted((root / 'animation').glob('frame_*.png')):
    with Image.open(path) as image:
        frames.append(image.convert('RGB').resize((960, 540), Image.Resampling.LANCZOS))
assert len(frames) == report['animationFrames']
# Use one representative color palette so frame-to-frame colors remain stable.
palette_samples = [frames[0], frames[len(frames)//3], frames[2*len(frames)//3], frames[-1]]
palette_sheet = Image.new('RGB', (960, 540 * len(palette_samples)))
for i, frame in enumerate(palette_samples): palette_sheet.paste(frame, (0, i * 540))
palette = palette_sheet.quantize(colors=192, method=Image.Quantize.MEDIANCUT)
indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
output = root / 'SettlementChest_Jackpot.gif'
indexed[0].save(output, save_all=True, append_images=indexed[1:], loop=0,
    duration=[10 * (round((i + 1) * 100 / report['animationFps']) - round(i * 100 / report['animationFps'])) for i in range(len(indexed))], disposal=1, optimize=True)
with Image.open(output) as check:
    assert check.n_frames > 20
    assert check.size == (960, 540)
    print(json.dumps({'file': str(output), 'frames': check.n_frames, 'bytes': output.stat().st_size,
                      'size': check.size, 'fps': report['animationFps'], 'endsOnOverview': True}))
