"""Pack built-in ImageGen artwork; deterministic crop/resize/format conversion only.

No image generation APIs. No temporary Unity project or cache is created.
The source images, prompts and hashes are preserved under the task output folder.
"""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
from PIL import Image, ImageDraw
import numpy as np

PROJECT = Path(__file__).resolve().parents[2]
OUT = PROJECT / 'outputs/naval-vfx-remaster-20261105'
RESOURCE = PROJECT / 'UnityClient/Assets/_Project/Resources/BattleVfx/NavalV2'
ORDER = ['muzzle', 'impact', 'splash', 'fire', 'arcane', 'curse', 'tide', 'thunder', 'blood']
LABELS = ['Muzzle', 'Hull impact', 'Water splash', 'Fire', 'Arcane', 'Curse', 'Tide', 'Thunder', 'Blood']
CELL, PAD = 256, 12


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def cells(sheet, columns, rows):
    return [sheet.crop((round(c * sheet.width / columns), round(r * sheet.height / rows),
                        round((c + 1) * sheet.width / columns), round((r + 1) * sheet.height / rows)))
            for r in range(rows) for c in range(columns)]


def tile(source):
    # One fixed framing for the entire source grid, never crop to alpha bounds.
    size = CELL - PAD * 2
    scale = min(size / source.width, size / source.height)
    resized = source.resize((round(source.width * scale), round(source.height * scale)), Image.Resampling.LANCZOS)
    output = Image.new('RGBA', (CELL, CELL))
    output.alpha_composite(resized, ((CELL - resized.width) // 2, (CELL - resized.height) // 2))
    return output


def composite(im, color='#173644'):
    bg = Image.new('RGBA', im.size, color)
    bg.alpha_composite(im)
    return bg.convert('RGB')


def gif(frames, path):
    # Source-art inspection only. Runtime screenshots are exported independently by Unity.
    rgb = [composite(x) for x in frames]
    rgb.append(Image.new('RGB', (CELL, CELL), '#173644'))
    palette_input = Image.new('RGB', (CELL * len(rgb), CELL))
    for i, frame in enumerate(rgb):
        palette_input.paste(frame, (i * CELL, 0))
    palette = palette_input.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    encoded = [x.quantize(palette=palette, dither=Image.Dither.NONE) for x in rgb]
    encoded[0].save(path, save_all=True, append_images=encoded[1:], duration=[90] * 8 + [500],
                    loop=0, disposal=2, optimize=False)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    data = json.loads((OUT / 'art-sources.json').read_text(encoding='utf-8'))
    for folder in ['sources', 'art-preview', 'packed']:
        (OUT / folder).mkdir(parents=True, exist_ok=True)
    atlas = Image.new('RGBA', (CELL * 8, CELL * 11))
    report = {'layout': {'columns': 8, 'rows': 11, 'cell_pixels': CELL, 'padding_pixels': PAD,
                          'sequence_rows': ORDER, 'static_rows': [9, 10]}, 'sources': {}, 'tiles': []}
    gallery = Image.new('RGB', (CELL * 4, CELL * 3), '#173644')
    draw = ImageDraw.Draw(gallery)
    for name, input_path in data['sources'].items():
        src = Path(input_path)
        assert src.is_file(), f'Missing required original: {src}'
        saved = OUT / 'sources' / f'{name}.png'
        if not saved.exists() or sha(saved) != sha(src):
            shutil.copy2(src, saved)
        sheet = Image.open(saved).convert('RGBA')
        alpha_range = sheet.getchannel('A').getextrema()
        assert alpha_range[0] == 0 and alpha_range[1] > 64, f'No usable transparent artwork: {name}'
        report['sources'][name] = {'original': str(src), 'archived': str(saved), 'sha256': sha(saved), 'size': list(sheet.size)}
        parts = cells(sheet, 4, 4 if name == 'static' else 2)
        final = [tile(part) for part in parts]
        for i, frame in enumerate(final):
            row, col = ((9 + i // 8, i % 8) if name == 'static' else (ORDER.index(name), i))
            alpha = np.asarray(frame.getchannel('A'))
            assert alpha.max() > 0
            assert np.all(alpha[:PAD] == 0) and np.all(alpha[-PAD:] == 0)
            assert np.all(alpha[:, :PAD] == 0) and np.all(alpha[:, -PAD:] == 0)
            report['tiles'].append({'kind': name, 'frame': i, 'row': row, 'column': col,
                'alpha_pixels': int((alpha > 0).sum()), 'semi_transparent_pixels': int(((alpha > 0) & (alpha < 255)).sum())})
            atlas.alpha_composite(frame, (col * CELL, row * CELL))
        preview = Image.new('RGBA', (CELL * 4, CELL * (4 if name == 'static' else 2)))
        for i, frame in enumerate(final):
            preview.alpha_composite(frame, (i % 4 * CELL, i // 4 * CELL))
        composite(preview).save(OUT / 'art-preview' / f'{name}-frames.jpg', quality=94)
        if name != 'static':
            gif(final, OUT / 'art-preview' / f'{name}.gif')
            n = ORDER.index(name)
            gallery.paste(composite(final[3]), (n % 4 * CELL, n // 4 * CELL))
            draw.text((n % 4 * CELL + 12, n // 4 * CELL + 10), LABELS[n], fill='#eee2bf', font_size=16)
    packed = OUT / 'packed/naval_fx_remaster_v2.png'
    atlas.save(packed)
    composite(atlas).resize((1024, 1408), Image.Resampling.LANCZOS).save(OUT / 'art-preview/atlas-review.jpg', quality=94)
    gallery.save(OUT / 'art-preview/effects-overview.jpg', quality=94)
    report['packed'] = {'path': str(packed), 'size': list(atlas.size), 'sha256': sha(packed), 'bytes': packed.stat().st_size}
    if args.install:
        RESOURCE.mkdir(parents=True, exist_ok=True)
        target = RESOURCE / packed.name
        shutil.copy2(packed, target)
        template = PROJECT / 'UnityClient/Assets/_Project/Resources/BattleVfx/Naval/naval_fx_atlas_v1.png.meta'
        meta = template.read_text(encoding='utf-8').replace('c3854804589d432b9fa1e51046757824', '7b497dd2f0e34df2a4ebc66da1f24405')
        meta = meta.replace('maxTextureSize: 2048', 'maxTextureSize: 4096')
        meta = meta.replace('Built-in image_gen; transparent 4x4 naval component VFX; original alpha preserved.',
                            'Built-in image_gen naval remaster; 8x11 atlas; 9 eight-frame sequences plus 16 static elements; RGBA preserved.')
        Path(str(target) + '.meta').write_text(meta, encoding='utf-8')
        folder_meta = Path(str(RESOURCE) + '.meta')
        if not folder_meta.exists():
            folder_meta.write_text('fileFormatVersion: 2\nguid: d94cb238a89748509f2a583db36588bc\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')
        report['installed'] = {'path': str(target), 'sha256': sha(target)}
    (OUT / 'art-validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'packed': report['packed'], 'tile_count': len(report['tiles']), 'installed': report.get('installed')}, indent=2))


if __name__ == '__main__':
    main()
