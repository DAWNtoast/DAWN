"""Install the already-generated anatomy fix; no network calls."""
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image

ROOT = Path('D:/Game')
OUT = ROOT / 'Artifacts/CardDoctorAnatomy20261013'
RESOURCE = Path('Assets/_Project/Resources/CardArt/Western/card_doctor_sutures.png')
MAIN = ROOT / 'UnityClient' / RESOURCE
SCRATCH = ROOT / 'tmp/card-clarity-20261013' / RESOURCE
ORIGINAL = OUT / 'original-card_doctor_sutures.png'
GENERATED = OUT / 'doctor-sutures-anatomy-v2.jpg'
PNG = OUT / 'doctor-sutures-anatomy-v2.png'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

original_sha = sha(ORIGINAL)
meta_backup = OUT / 'original-card_doctor_sutures.png.meta'
assert MAIN.with_suffix('.png.meta').read_bytes() == meta_backup.read_bytes(), 'Importer changed; preserve concurrent work'
with Image.open(GENERATED) as im:
    assert im.size == (2048, 2048), im.size
    im.convert('RGB').save(PNG)
new_sha = sha(PNG)
for target in (MAIN, SCRATCH):
    assert sha(target) in (original_sha, new_sha), f'Unexpected concurrent change: {target}'
for target in (MAIN, SCRATCH):
    shutil.copyfile(PNG, target)
    assert sha(target) == new_sha
assert MAIN.with_suffix('.png.meta').read_bytes() == meta_backup.read_bytes()
record = dict(apiCalls=1, provider='Nano Banana / gemini-3-pro-image',
              original=str(ORIGINAL), generated=str(GENERATED), installed=str(MAIN),
              sourcePixels=[2048, 2048], gameImportMaxSize=1024,
              originalMetaUnchanged=True, guid='7388de96b53640bcb7b398b516ac78cf',
              installedSha256=new_sha, prompt=str(OUT / 'prompt.txt'))
(OUT / 'manifest.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(record, ensure_ascii=False))
