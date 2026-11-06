"""Reuse existing item pixels: isolate barrel/ball, crop standalone cargo art.

No image generation or repainting. The contour masks remove only the external
card medallions. Source RGB pixels within the retained objects are unchanged.
"""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/battle-resource-icons-20261026/borderless'
TARGET = OUT / 'source-icons'
FRAME = ROOT / 'UnityClient/Assets/_Project/Resources/CardSchools/Frames/Integrated/frame-neutral-both.png'
ITEMS = ROOT / 'UnityClient/Assets/_Project/Resources/ItemIcons/items_small_v1.png'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def contour_mask(key):
    mask = Image.new('L', (1200, 1200))
    draw = ImageDraw.Draw(mask)
    def polygon(points):
        draw.polygon([(round(x * 4), round(y * 4)) for x, y in points], fill=255)
    if key == 'cannonball':
        draw.ellipse((74 * 4, 63 * 4, 225 * 4, 218 * 4), fill=255)
    else:
        polygon([(103,70),(116,64),(140,60),(159,59),(176,64),(191,67),
                 (202,71),(207,79),(208,87),(214,94),(221,106),(224,121),
                 (225,144),(223,161),(217,179),(207,195),(191,207),
                 (173,213),(151,214),(131,211),(111,203),(95,192),
                 (83,176),(76,157),(75,138),(77,119),(82,105),(90,93),(95,81)])
        # The barrel's own red fuse and spark remain; card rim pixels do not.
        polygon([(175,79),(176,65),(181,48),(187,39),(195,32),(207,28),
                 (223,25),(224,33),(210,37),(200,43),(194,51),(190,64),(190,79)])
        polygon([(231,12),(235,23),(246,20),(240,29),(250,33),(240,36),
                 (242,45),(234,39),(228,49),(228,38),(217,41),(223,33),
                 (215,28),(225,27)])
    return mask.resize((300, 300), Image.Resampling.LANCZOS)


def main():
    TARGET.mkdir(parents=True, exist_ok=True)
    rows = []
    frame = Image.open(FRAME).convert('RGBA')
    for key, bounds in [('powder', (700,1300,1000,1600)),
                        ('cannonball', (-29,1297,271,1597))]:
        crop = frame.crop(bounds)
        original_rgb = crop.convert('RGB').tobytes()
        crop.putalpha(ImageChops.multiply(crop.getchannel('A'), contour_mask(key)))
        assert crop.convert('RGB').tobytes() == original_rgb
        clean = Image.new('RGBA', crop.size)
        clean.alpha_composite(crop)
        crop = clean
        output = TARGET / (key + '.png')
        crop.save(output)
        rows.append({'id':key,'sourceMethod':'original-card-item-contour-crop',
                     'originalSource':str(FRAME),'originalSourceSHA256':sha(FRAME),
                     'sourceCrop':list(bounds),'sourcePixelsUnchanged':True,
                     'output':str(output),'outputSHA256':sha(output)})
    items = Image.open(ITEMS).convert('RGBA')
    assert items.size == (1402,1122), f'Check cargo source measurements: {items.size}'
    for key, bounds in [('planks', (576,51,826,281)),
                        ('medicine', (292,317,543,552))]:
        crop = items.crop(bounds)
        assert crop.getchannel('A').getextrema()[0] == 0
        output = TARGET / (key + '.png')
        crop.save(output)
        rows.append({'id':key,'sourceMethod':'existing-standalone-item-atlas-crop',
                     'originalSource':str(ITEMS),'originalSourceSHA256':sha(ITEMS),
                     'sourceCrop':list(bounds),'sourcePixelsUnchanged':True,
                     'output':str(output),'outputSHA256':sha(output)})
    (OUT / 'body-source-provenance.json').write_text(
        json.dumps({'method':'Existing pixels only; no repainting or image generation used in final icons.',
                    'assets':rows},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'icons':4,'sources':str(TARGET)},ensure_ascii=False))


if __name__ == '__main__':
    main()
