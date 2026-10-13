from pathlib import Path
import hashlib
import json
import re
import shutil
import uuid
from PIL import Image

ROOT = Path('D:/Game')
OUT = ROOT/'Artifacts/CrewLayers20261013'
PROJECT = ROOT/'UnityClient'
SCRATCH = ROOT/'tmp/card-clarity-20261013'
template = (PROJECT/'Assets/_Project/Resources/PortArt/CrewRegional/crew_broken_anchor_portraits_4k_trial_v1.jpg.meta').read_text()
assets = [
    ('portraits-4k-v1.jpg', 'CrewRegional/crew_broken_anchor_portraits_4k_layers_v1.jpg', 4096),
    ('frame-4k-v1.jpg', 'UI/tavern_crew_dossier_frame_4k_layers_v1.jpg', 8192),
]
records = []
for source_name, relative, maximum in assets:
    source = OUT/source_name
    target = PROJECT/'Assets/_Project/Resources/PortArt'/relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    meta = Path(str(target)+'.meta')
    if not meta.exists():
        text = re.sub(r'guid: [a-f0-9]+', 'guid: '+uuid.uuid4().hex, template, count=1)
        text = text.replace('maxTextureSize: 4096', f'maxTextureSize: {maximum}')
        text = re.sub(r'  userData: .*', '  userData: Independent crew layers trial. Original Nano Banana JPEG; shared blue-key artwork; native desktop BC7; no mips.', text)
        meta.write_text(text, encoding='utf-8')
    with Image.open(source) as im:
        size = list(im.size)
    records.append(dict(source=str(source),resource=str(target),pixels=size,bytes=source.stat().st_size,
                        sha256=hashlib.sha256(source.read_bytes()).hexdigest(),maxTextureSize=maximum))
manifest_path = OUT/'manifest.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
manifest.update(generationRequestsCompleted=2,assets=records,
    technique='Independent six-person blue-screen atlas and one empty reusable frame, keyed on GPU with existing component text.',
    atlasCropColumns=[[0,1356],[1372,2723],[2740,4096]],atlasCropRows=[[0,2040],[2056,4096]],
    frameWindowTopLeftXYWH=[450,445,1464,1856],
    limitations=['Portrait shoulders and some outside hair are cropped by the cell; intended for framed use, not complete free-standing cutouts.',
                 'Source JPEGs have blue backgrounds. Transparency is applied by the game shader, not stored in the source.'])
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

paths = [
    'Assets/_Project/Presentation/Runtime/CoreGameplayTavernDossierArtComparison.cs',
    'Assets/_Project/Presentation/Runtime/PortArtCatalog.cs',
    'Assets/_Project/Presentation/Runtime/CrewArtChromaKey.cs',
    'Assets/_Project/Presentation/Runtime/CrewArtChromaKey.cs.meta',
    'Assets/_Project/Resources/Shaders/CrewPortraitChromaKey.shader',
    'Assets/_Project/Resources/Shaders/CrewPortraitChromaKey.shader.meta',
]
for _, relative, _ in assets:
    paths.extend(['Assets/_Project/Resources/PortArt/'+relative,'Assets/_Project/Resources/PortArt/'+relative+'.meta'])
for relative in paths:
    dest = SCRATCH/relative
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(PROJECT/relative,dest)
shutil.copy2(ROOT/'tools/CrewLayers20261013/CrewLayersCapture.cs',SCRATCH/'Assets/_Project/Editor/CrewLayersCapture.cs')
print(json.dumps(dict(synced=len(paths)+1,assets=records),ensure_ascii=False,indent=2))
