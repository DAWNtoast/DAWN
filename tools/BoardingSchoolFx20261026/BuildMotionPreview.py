"""Encode the engine's captured timeline as compact, loopable local previews."""
import json
from pathlib import Path
from PIL import Image

root = Path(r'D:\Game\outputs\boarding-school-fx-20261026')
capture = root / 'v2-validation' / 'capture1'
report = json.loads((capture / 'report.json').read_text(encoding='utf-8-sig'))
destination = root / 'v2'
destination.mkdir(exist_ok=True)
for school, title in [('Fire', '甲板火焰'), ('Thunder', '天降雷霆'), ('Tide', '甲板浪涌')]:
    sequence = [f for f in report['frames'] if f['school'] == school and f['role'] == 'Attack']
    images = []
    for frame in sequence:
        with Image.open(capture / frame['file']) as image:
            images.append(image.convert('RGB').resize((960, 540), Image.Resampling.LANCZOS))
    # A common palette keeps the unchanged deck and actors stable throughout playback.
    samples = Image.new('RGB', (960, 540 * 6))
    for i in range(6):
        samples.paste(images[round(i * (len(images) - 1) / 5)], (0, i * 540))
    palette = samples.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    frames = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
    interval = round(sequence[0]['duration'] * 1000 / (len(frames) - 1) / 10) * 10
    durations = [max(10, interval)] * len(frames)
    durations[-1] = 500
    output = destination / (title + '.gif')
    frames[0].save(output, save_all=True, append_images=frames[1:], duration=durations,
                   loop=0, optimize=True, disposal=1)
    print(f'{output}: {len(frames)} frames, {output.stat().st_size} bytes')
    for image in images + frames + [samples, palette]:
        image.close()
