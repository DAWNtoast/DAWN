"""Pack four reviewed, transparent item-only crops into their existing atlas cells.

No art generation or card-medallion extraction is performed. Inputs must contain
only the item, without its decorative frame, frame rivets or backing. The script trims alpha
margins, deterministically resizes and centers each item in a 256px cell.
"""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/battle-resource-icons-20261026'
BORDERLESS = OUT / 'borderless'
EDITED_ROOT = BORDERLESS / 'source-icons'
BACKUP = BORDERLESS / 'source-before'
FRAMES = ROOT / 'UnityClient/Assets/_Project/Resources/CardSchools/Frames/Integrated'
FRAME_LAYOUT = FRAMES / 'frame-layouts.json'
ATLAS = ROOT / 'UnityClient/Assets/_Project/Resources/BattleIcons/battle_icons_v1.png'
SOURCE_METHOD = 'reviewed-existing-item-sources'
SPECS = [
    ('powder', '火药', 'frame-neutral-both', 7),
    ('cannonball', '炮弹', 'frame-neutral-both', 8),
    ('planks', '木板', 'frame-neutral-ap-wood', 9),
    ('medicine', '药品', 'frame-neutral-ap-medicine', 10),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def cell_bounds(cell):
    x, y = cell % 8 * 256, cell // 8 * 256
    return (x, y, x + 256, y + 256)


def verify_frame_provenance():
    """Do not bless old item crops after their reference frames change."""
    baseline = BACKUP / 'shared-icons-manifest.json'
    require(baseline.is_file(), 'The preserved pre-edit source manifest is required.')
    previous = json.loads(baseline.read_text(encoding='utf-8-sig'))
    require(FRAME_LAYOUT.is_file(), f'Missing card-frame layout: {FRAME_LAYOUT}')
    require(sha(FRAME_LAYOUT).lower() == previous['frameLayoutSHA256'].lower(),
            'Card-frame layouts changed. Review fresh item-only sources before updating provenance.')
    sources = {row['id']: row for row in previous['assets']}
    for key, _, frame, _ in SPECS:
        source = FRAMES / (frame + '.png')
        require(source.is_file(), f'Missing reference card frame: {source}')
        require(key in sources and sha(source).lower() == sources[key]['sourceSHA256'].lower(),
                f'Reference card frame changed for {key}; review fresh item-only art before proceeding.')


def make_icon(source):
    require(source.is_file(), f'Missing reviewed item-only PNG: {source}. No card-frame fallback is allowed.')
    with Image.open(source) as opened:
        require(opened.format == 'PNG', f'Expected a reviewed transparent PNG: {source}')
        require('A' in opened.getbands() or 'transparency' in opened.info,
                f'Reviewed source has no alpha channel: {source}')
        edited = opened.convert('RGBA')
    alpha = edited.getchannel('A')
    minimum, maximum = alpha.getextrema()
    require(minimum == 0 and maximum > 0, f'Expected visible item pixels on a transparent background: {source}')
    bounds = alpha.getbbox()
    require(bounds is not None, f'Reviewed source is empty: {source}')
    content = edited.crop(bounds)
    scale = min(224 / content.width, 224 / content.height)
    size = (max(1, round(content.width * scale)), max(1, round(content.height * scale)))
    content = content.resize(size, Image.Resampling.LANCZOS)
    icon = Image.new('RGBA', (256, 256), (0, 0, 0, 0))
    icon.alpha_composite(content, ((256 - size[0]) // 2, (256 - size[1]) // 2))
    result_alpha = icon.getchannel('A')
    for edge in ((0, 0, 256, 1), (0, 255, 256, 256), (0, 0, 1, 256), (255, 0, 256, 256)):
        require(result_alpha.crop(edge).getextrema()[1] == 0, f'Item touches its cell boundary: {source}')
    require(sum(1 for a in result_alpha.tobytes() if a > 127) > 5000,
            f'Too few visible item pixels in {source}; inspect the reviewed source.')
    return icon, list(bounds), list(edited.size), list(size)


def build_preview(icons):
    sheet = Image.new('RGB', (1120, 410), (20, 29, 38))
    pen = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
    for index, ((_, label, _, _), icon) in enumerate(zip(SPECS, icons)):
        pen.text((index * 280 + 106, 18), label, font=font, fill=(238, 209, 155))
        sheet.paste(icon, (index * 280 + 12, 58), icon)
        thumbnail = icon.resize((48, 48), Image.Resampling.LANCZOS)
        sheet.paste(thumbnail, (index * 280 + 52, 332), thumbnail)
        pen.text((index * 280 + 114, 345), '48 px', font=small, fill=(210, 220, 223))
    return sheet


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true', help='Install the four reviewed item-only cells.')
    args = parser.parse_args()
    # Validate all inputs before touching canonical single icons or the manifest.
    for key, _, _, _ in SPECS:
        source = EDITED_ROOT / (key + '.png')
        require(source.is_file(), f'Missing reviewed item-only PNG: {source}. No card-frame fallback is allowed.')
    verify_frame_provenance()
    provenance_path = BORDERLESS / 'body-source-provenance.json'
    require(provenance_path.is_file(), 'Missing reviewed body source provenance.')
    provenance = {row['id']: row for row in json.loads(provenance_path.read_text(encoding='utf-8'))['assets']}
    for key, _, _, _ in SPECS:
        require(key in provenance, f'Missing source provenance for {key}')
        origin = provenance[key]
        require(sha(Path(origin['originalSource'])) == origin['originalSourceSHA256'],
                f'Original item art changed: {key}. Review a fresh source before packing.')
        require(sha(EDITED_ROOT / (key + '.png')) == origin['outputSHA256'],
                f'Reviewed item-only pixels changed: {key}')
    require(ATLAS.is_file(), f'Missing battle atlas: {ATLAS}')
    require((BACKUP / ATLAS.name).is_file(), 'Save this revision pre-change atlas before building.')
    with Image.open(ATLAS) as opened:
        before = opened.convert('RGBA')
    require(before.size == (2048, 1536), f'Unexpected battle atlas size: {before.size}')
    atlas = before.copy()
    icons, rows = [], []
    # A preview cannot replace canonical files consumed by the legacy installer.
    destination = OUT if args.install else BORDERLESS / 'preview'
    art_dir = destination / 'shared-icons'
    for key, label, frame, cell in SPECS:
        edited_source = EDITED_ROOT / (key + '.png')
        icon, bounds, original_size, fitted_size = make_icon(edited_source)
        icons.append(icon)
        x, y, _, _ = cell_bounds(cell)
        atlas.paste(icon, (x, y))
        frame_source = FRAMES / (frame + '.png')
        rows.append({
            'id': key, 'name': label,
            # Retain these frame fields for InstallBattleIcons.ps1 hash guards.
            'source': str(frame_source), 'sourceSHA256': sha(frame_source),
            'sourceMethod': provenance[key]['sourceMethod'],
            'originalSource': provenance[key]['originalSource'],
            'originalSourceSHA256': provenance[key]['originalSourceSHA256'],
            'originalSourceCrop': provenance[key]['sourceCrop'],
            'editedSource': str(edited_source), 'editedSourceSHA256': sha(edited_source),
            'editedSourceSize': original_size, 'editedSourceAlphaBounds': bounds,
            'fittedItemSize': fitted_size,
            'atlasCell': cell, 'atlasRect': [x, y, 256, 256],
            'icon': str(art_dir / (key + '.png')),
        })
    changed = {spec[3] for spec in SPECS}
    for cell in range(48):
        if cell not in changed:
            require(before.crop(cell_bounds(cell)).tobytes() == atlas.crop(cell_bounds(cell)).tobytes(),
                    f'Unrelated atlas cell changed: {cell}')
    sheet = build_preview(icons)

    art_dir.mkdir(parents=True, exist_ok=True)
    for row, icon in zip(rows, icons):
        target = Path(row['icon'])
        icon.save(target)
        row['iconSHA256'] = sha(target)
    preview = destination / '统一物品图标预览.png'
    sheet.save(preview)
    before_hash = sha(ATLAS)
    if args.install:
        atlas.save(ATLAS)
    report = {
        'installed': args.install, 'atlas': str(ATLAS), 'atlasSHA256': sha(ATLAS),
        'frameLayoutSHA256': sha(FRAME_LAYOUT), 'sourceMethod': SOURCE_METHOD,
        'unchangedAtlasCellsVerified': 44,
        'sourcePixels': 'Wood and medicine reuse existing standalone cargo art; powder and cannonball retain original card-frame item pixels with local contour crops. No redrawing. Packing only trims alpha margins, resizes and centers. No decorative frame, frame rivets or backing.',
        'assets': rows,
    }
    (destination / 'shared-icons-manifest.json').write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    if args.install:
        with Image.open(ATLAS) as opened:
            installed = opened.convert('RGBA')
        changed_cells = [cell for cell in range(48)
                         if before.crop(cell_bounds(cell)).tobytes() != installed.crop(cell_bounds(cell)).tobytes()]
        require(set(changed_cells).issubset(changed), f'Unexpected installed changes: {changed_cells}')
        exact = []
        for row, icon in zip(rows, icons):
            require(installed.crop(cell_bounds(row['atlasCell'])).tobytes() == icon.tobytes(),
                    f'Installed icon differs: {row["id"]}')
            exact.append(row['id'])
        audit = {
            'result': 'PASS',
            'scope': 'Deterministic packing and installed pixel verification; visual item-only review is performed before these inputs are supplied.',
            'atlasBeforeSHA256': before_hash, 'atlasAfterSHA256': sha(ATLAS),
            'dimensions': list(installed.size), 'allowedChangedCells': sorted(changed),
            'actualChangedCells': changed_cells, 'unchangedCellsVerified': 44,
            'fourIconsExactPixelMatch': exact, 'sourceMethod': SOURCE_METHOD,
            'sourceManifest': str(destination / 'shared-icons-manifest.json'),
            'backup': str(BACKUP),
            'sourceFramesAndLayoutsUnchangedFromPreservedApproval': True,
        }
        (BORDERLESS / 'pixel-audit.json').write_text(
            json.dumps(audit, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'installed': args.install, 'icons': len(rows), 'preview': str(preview)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
