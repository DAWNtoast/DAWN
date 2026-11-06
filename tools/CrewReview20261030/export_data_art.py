"""Copy production portraits and crop current atlas/tavern pixels, never synthesize art."""
import argparse
import hashlib
import json
import math
import re
import shutil
from pathlib import Path
from PIL import Image


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    root, output = Path(args.root).resolve(), Path(args.output).resolve()
    data_path = output / 'crew-data.json'
    data = json.loads(data_path.read_text(encoding='utf-8-sig'))
    resource = root / 'UnityClient/Assets/_Project/Resources'
    art = output / 'art'
    art.mkdir(exist_ok=True)
    layout_path = root / 'UnityClient/Assets/_Project/Presentation/Runtime/TavernCrewLayoutCatalog.cs'
    layout_source = layout_path.read_text(encoding='utf-8-sig').split('private static readonly Vector2[][] NameCenters')[0]
    rects = [tuple(map(int, match)) for match in re.findall(r'R\((\d+),(\d+),(\d+),(\d+)\)', layout_source)]
    assert len(rects) == 36, 'Expected exact production bounds for 36 crew'
    ports = dict(zip(data['ports'], ['broken_anchor', 'white_shell', 'bronze_bell', 'salt_crown', 'twilight_tide', 'gray_gull']))
    source_hashes, asset_records = {}, []

    def record_source(source):
        rel = source.relative_to(root).as_posix()
        digest = sha(source)
        if rel in source_hashes:
            assert source_hashes[rel] == digest, f'Source changed during export: {rel}'
        source_hashes[rel] = digest
        return rel, digest

    def describe(source, dest, kind, label, crop=None):
        source_rel, digest = record_source(source)
        with Image.open(dest) as image:
            width, height = image.size
            has_transparency = image.mode in ('RGBA', 'LA') and image.getchannel('A').getextrema()[0] < 255
        result = dict(path=dest.relative_to(output).as_posix(), sourcePath=source_rel,
                      sourceSha256=digest, sha256=sha(dest), width=width, height=height,
                      kind=kind, label=label, transparent=has_transparency)
        if crop is not None:
            result['cropPixels'] = list(crop)
        asset_records.append(result)
        return result

    def copy(source, filename, kind, label):
        assert source.is_file(), f'Missing production art: {source}'
        dest = art / filename
        if not dest.exists() or sha(dest) != sha(source):
            shutil.copyfile(source, dest)
        assert sha(source) == sha(dest), f'Original copy changed bytes: {source}'
        return describe(source, dest, kind, label)

    def crop(source, filename, box, kind, label):
        dest = art / filename
        with Image.open(source) as original:
            assert 0 <= box[0] < box[2] <= original.width and 0 <= box[1] < box[3] <= original.height
            cropped = original.crop(box)
            cropped.save(dest, format='PNG')
            with Image.open(dest) as check:
                assert check.mode == cropped.mode and check.size == cropped.size and check.tobytes() == cropped.tobytes(), 'Crop pixels changed'
        return describe(source, dest, kind, label, crop=box)

    taverns = {}
    for port, slug in ports.items():
        source = resource / f'PortFacilities/Regional/Tavern/tavern_{slug}_v1.png'
        taverns[port] = copy(source, f'tavern_{slug}_original.png', 'production-tavern-original', f'{port}酒馆完整原图')

    for crew in data['crew']:
        cid, port, prof = crew['id'], crew['portName'], crew['professionIndex']
        slug = ports[port]
        portrait = resource / f'PortArt/CrewDossiers/{cid}_portrait_v2.png'
        current_portrait = copy(portrait, f'{cid}_portrait.png', 'production-dossier-portrait', '当前档案立绘原图（含场景背景的半身画像）')
        tavern_source = resource / f'PortFacilities/Regional/Tavern/tavern_{slug}_v1.png'
        x, y, width, height = rects[data['ports'].index(port) * 6 + prof]
        with Image.open(tavern_source) as image:
            box = (math.floor(x / 1672 * image.width), math.floor(y / 941 * image.height),
                   math.ceil((x + width) / 1672 * image.width), math.ceil((y + height) / 941 * image.height))
        tavern = crop(tavern_source, f'{cid}_tavern.png', box, 'production-tavern-crop', '酒馆内形象（按当前人物交互框裁切原图，保留环境）')
        tavern['normalizedBounds'] = dict(x=x / 1672, y=y / 941, width=width / 1672, height=height / 941)
        tavern['scenePath'] = taverns[port]['path']
        atlas_source = resource / f'PortArt/CrewRegional/crew_{slug}_portraits_v2.png'
        with Image.open(atlas_source) as atlas:
            column, row = prof % 3, prof // 3
            atlas_box = (round(column * atlas.width / 3), round(row * atlas.height / 2),
                         round((column + 1) * atlas.width / 3), round((row + 1) * atlas.height / 2))
        regional = crop(atlas_source, f'{cid}_regional.png', atlas_box, 'production-regional-portrait-crop', '通用角色画像（现役纸面图集原像素裁切）')
        crew['art'] = dict(portrait=current_portrait, tavern=tavern, regionalPortrait=regional)
        legacy = resource / f"PortArt/CrewFullBody/{crew['portraitAssetName']}.png"
        if legacy.is_file():
            crew['art']['legacyFullBody'] = copy(legacy, f'{cid}_legacy_fullbody.png', 'legacy-fullbody-original', '旧版全身立绘（兼容素材，非当前档案立绘）')

    data['tavernOriginals'] = taverns
    data['artNotes'] = [
        '当前档案立绘使用生产 CrewDossierPortrait 的 portrait_v2 原文件，逐字节复制，不缩小、不重新压缩。',
        '酒馆形象直接从每港生产酒馆原图裁切，区域来自 TavernCrewLayoutCatalog；保留原像素和场景背景。',
        '通用角色画像从当前 portraits_v2 图集逐人裁切；灰鸥港另附仍保留的旧版全身立绘。',
        '游戏四页截图由单独的当前生产 Unity 渲染流程提供，不由这些网页素材拼绘。'
    ]
    source_hashes[layout_path.relative_to(root).as_posix()] = sha(layout_path)
    for path, digest in source_hashes.items():
        assert sha(root / path) == digest, f'Source changed during export: {path}'
    data_path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    audit = dict(crewCount=len(data['crew']), portCount=len(data['ports']),
                 portraitCount=sum('portrait' in x['art'] for x in data['crew']),
                 tavernCropCount=sum('tavern' in x['art'] for x in data['crew']),
                 regionalPortraitCount=sum('regionalPortrait' in x['art'] for x in data['crew']),
                 legacyFullBodyCount=sum('legacyFullBody' in x['art'] for x in data['crew']),
                 specialCardCount=sum(len(x['specialCards']) for x in data['crew']),
                 allOriginalCopiesByteIdentical=True, allCropPixelsVerified=True,
                 sourceHashes=source_hashes, assets=asset_records,
                 assetBytes=sum((output / r['path']).stat().st_size for r in asset_records))
    (output / 'data-audit.json').write_text(json.dumps(audit, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in audit.items() if key not in ('assets', 'sourceHashes')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
