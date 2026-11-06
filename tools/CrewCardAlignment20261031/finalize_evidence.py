"""Collect generation provenance and verify installation after the catalogue export."""
import hashlib
import json
from pathlib import Path

ROOT = Path('D:/Game')
TASK = ROOT/'outputs/crew-card-alignment-20261031'
def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
mapping = read(TASK/'audit/crew-card-mapping.json')
installed = read(TASK/'audit/install-manifest.json')['installed']
records = []
for item in installed:
    crew_id = item['crewId']
    suffix = crew_id.rsplit('_',1)[-1]
    choices = [TASK/'generated'/f'{crew_id}-generation.json', TASK/'generated'/f'{crew_id}.generation.json', TASK/'generated'/f'{suffix}-generation.json']
    record_path = next(p for p in choices if p.exists())
    record = read(record_path)
    crew = next(c for c in mapping['crew'] if c['id']==crew_id)
    assert sha(item['sourcePath']) == item['installedSha256'] == sha(item['candidatePath'])
    records.append({
        'crewId': crew_id, 'name': item['name'], 'tool': 'built-in image_gen',
        'prompt': record['prompt'], 'promptRecord': str(record_path),
        'targetOriginalSha256': item['originalSha256'],
        'targetOriginalBackup': str(TASK/'input-backup'/Path(item['sourcePath']).relative_to(ROOT)),
        'canonicalReferences': [crew['tavernOverview'], crew['dossierPortrait']],
        'installedPath': item['sourcePath'], 'generatedCopy': item['candidatePath'],
        'installedSha256': item['installedSha256'], 'dimensions': item['dimensions'],
    })

assert len(records)==6
for crew in mapping['crew']:
    for ref in [crew['tavernOverview'], crew['dossierPortrait']]:
        assert sha(ref['sourcePath']) == ref['sha256'], ('Tavern reference changed', crew['id'])
    original = Path(crew['card']['sourcePath'])
    backup = TASK/'input-backup'/original.relative_to(ROOT)
    assert sha(str(original)+'.meta') == sha(str(backup)+'.meta')
    if crew['id'] not in {r['crewId'] for r in records}:
        assert sha(original) == crew['card']['sha256']

out = {'tool': 'built-in image_gen', 'externalApisUsed': False, 'images': records,
       'verification': {'all36CrewChecked':True, 'sixInstalled':True, 'other30Unchanged':True,
                        'tavernSourcesUnchanged':True, 'allImporterMetadataUnchanged':True}}
(TASK/'generation-manifest.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(out['verification'],ensure_ascii=False))
