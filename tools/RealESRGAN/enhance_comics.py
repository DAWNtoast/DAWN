"""Authorized local AI super-resolution; never writes to Unity resources.

Uses the official portable Real-ESRGAN x4plus model, then Lanczos downsamples
the neural 4x result to 2x. The resampling step is not the upscaling method.
"""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import subprocess
import time

from PIL import Image
import PIL

ROOT = Path('D:/Game')
TOOL_ROOT = ROOT / 'tools/RealESRGAN'
TOOL = TOOL_ROOT / 'realesrgan-ncnn-vulkan-20220424-windows'
OUT = ROOT / 'Artifacts/ComicHD20261011'
EXE = TOOL / 'realesrgan-ncnn-vulkan.exe'
MODEL = 'realesrgan-x4plus'
URL = 'https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def enhance(source):
    source = source.resolve()
    raw = OUT / 'SuperResolution' / (source.stem + '_x4_neural.png')
    dest = OUT / 'Enhanced' / source.name
    record_path = OUT / 'SuperResolution' / (source.stem + '.json')
    log_path = OUT / 'SuperResolution' / (source.stem + '.log')
    source_hash = sha(source)
    if record_path.exists():
        record = json.loads(record_path.read_text(encoding='utf-8'))
        if record['inputSha256'] == source_hash and dest.exists() and record['outputSha256'] == sha(dest):
            print('VERIFIED_EXISTING ' + str(dest), flush=True)
            return record
        raise RuntimeError('Refusing to reuse a mismatching enhancement: ' + str(record_path))
    if dest.exists() or raw.exists():
        raise RuntimeError('Refusing to overwrite an unrecorded output: ' + str(dest))
    with Image.open(source) as image:
        size = image.size
    args = [str(EXE), '-i', str(source), '-o', str(raw), '-n', MODEL,
            '-m', str(TOOL / 'models'), '-s', '4', '-t', '256', '-g', '0', '-j', '1:1:1', '-f', 'png']
    started = time.monotonic()
    print('START ' + source.name, flush=True)
    with log_path.open('w', encoding='utf-8') as log:
        subprocess.run(args, cwd=TOOL, stdout=log, stderr=subprocess.STDOUT,
                       creationflags=subprocess.CREATE_NO_WINDOW, check=True)
    with Image.open(raw) as image:
        if image.size != (size[0] * 4, size[1] * 4):
            raise RuntimeError('Neural output was not 4x: ' + str(image.size))
        raw_size = image.size
        image.resize((size[0] * 2, size[1] * 2), Image.Resampling.LANCZOS).save(dest, compress_level=6)
    with Image.open(dest) as image:
        final_size = image.size
    record = {
        'completedUtc': dt.datetime.now(dt.timezone.utc).isoformat(),
        'input': str(source), 'inputSha256': source_hash, 'inputDimensions': size,
        'output': str(dest), 'outputSha256': sha(dest), 'outputDimensions': final_size,
        'neuralOutput': str(raw), 'neuralOutputSha256': sha(raw), 'neuralOutputDimensions': raw_size,
        'method': 'Real-ESRGAN neural x4plus 4x; then Pillow Lanczos downsample to 2x',
        'model': MODEL, 'toolRelease': 'v0.2.5.0 / ncnn-vulkan 20220424 Windows portable',
        'officialDownload': URL, 'modelSource': 'Included in the official portable release archive',
        'toolLicense': 'MIT', 'modelProjectLicense': 'BSD-3-Clause',
        'licenseFiles': [str(TOOL_ROOT / 'LICENSE-ncnn-MIT.txt'), str(TOOL_ROOT / 'LICENSE-RealESRGAN-BSD-3-Clause.txt')],
        'archiveSha256': sha(TOOL_ROOT / 'realesrgan-ncnn-vulkan-20220424-windows.zip'),
        'executableSha256': sha(EXE),
        'modelBinSha256': sha(TOOL / 'models' / (MODEL + '.bin')),
        'modelParamSha256': sha(TOOL / 'models' / (MODEL + '.param')),
        'gpu': 'NVIDIA GeForce RTX 5080 (Vulkan GPU 0), 16303 MiB, driver 591.86',
        'command': args, 'pillowVersion': PIL.__version__, 'seconds': round(time.monotonic() - started, 3),
        'log': str(log_path),
    }
    record_path.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
    print('DONE ' + source.name + ' ' + str(final_size) + ' ' + str(record['seconds']) + 's', flush=True)
    return record


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('images', nargs='+')
    args = parser.parse_args()
    for folder in ('Enhanced', 'SuperResolution'):
        (OUT / folder).mkdir(parents=True, exist_ok=True)
    for item in args.images:
        enhance(Path(item))
    records = [json.loads(path.read_text(encoding='utf-8')) for path in sorted((OUT / 'SuperResolution').glob('*.json'))]
    (OUT / 'super-resolution-manifest.json').write_text(json.dumps({
        'method': 'Local neural Real-ESRGAN x4plus, reduced to 2x with Lanczos',
        'imageCount': len(records), 'images': records,
    }, ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
