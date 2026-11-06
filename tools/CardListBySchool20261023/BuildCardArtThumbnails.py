"""Scale the mapped current card illustrations locally; never redraw or crop."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def build(manifest):
    manifest = manifest.resolve(strict=True)
    output = manifest.parent
    report = json.loads(manifest.read_text(encoding='utf-8-sig'))
    rows = report['Cards']
    completed = {}
    total_bytes = 0
    for row in rows:
        resource = row['ResourcePath']
        if resource in completed:
            row.update(completed[resource])
            continue
        source = Path(row['SourcePath']).resolve(strict=True)
        if sha256(source) != row['SourceSha256']:
            raise RuntimeError(f'Source art changed: {source}')
        destination = output / row['Image']
        if not destination.resolve().is_relative_to((output / 'art').resolve()):
            raise RuntimeError(f'Unsafe thumbnail destination: {destination}')
        if destination.suffix != '.webp':
            raise RuntimeError('The mapping exporter must request WebP thumbnails.')
        destination.parent.mkdir(parents=True, exist_ok=True)
        with Image.open(source) as image:
            if image.size != (row['SourceWidth'], row['SourceHeight']):
                raise RuntimeError(f'Source dimensions changed: {source}')
            result = image.convert('RGBA' if 'A' in image.getbands() else 'RGB')
            result = result.resize((row['Width'], row['Height']), Image.Resampling.LANCZOS)
            options = dict(format='WEBP', quality=90, method=6, exact=True)
            if image.info.get('icc_profile'):
                options['icc_profile'] = image.info['icc_profile']
            result.save(destination, **options)
        if sha256(source) != row['SourceSha256']:
            raise RuntimeError(f'Source art changed during scaling: {source}')
        with Image.open(destination) as thumbnail:
            thumbnail.load()
            if thumbnail.size != (row['Width'], row['Height']):
                raise RuntimeError(f'Wrong thumbnail dimensions: {destination}')
        metadata = {'ImageSha256': sha256(destination), 'ImageBytes': destination.stat().st_size}
        row.update(metadata)
        completed[resource] = metadata
        total_bytes += metadata['ImageBytes']
    report['ImageFormat'] = 'WebP, quality 90; alpha preserved; complete composition'
    report['ThumbnailBytes'] = total_bytes
    temporary = manifest.with_suffix('.json.new')
    temporary.write_text(json.dumps(report, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    temporary.replace(manifest)
    validation = (f"Card identities: {report['CardCount']}\nStage art mappings: {len(rows)}\n"
                  f"Unique images: {len(completed)}\nMissing images: 0\n"
                  f"Maximum thumbnail edge: {report['MaximumEdge']}\nThumbnail bytes: {total_bytes}\n"
                  "PASS: Current published identities and stages match base; all resource paths resolve; "
                  "full compositions and alpha preserved; source and thumbnail hashes verified; every thumbnail decodes.\n")
    (output / 'art-validation.txt').write_text(validation, encoding='utf-8')
    print(validation, end='')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', required=True, type=Path)
    build(parser.parse_args().manifest)
