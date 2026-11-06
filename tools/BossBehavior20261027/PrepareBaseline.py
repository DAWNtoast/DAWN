"""Reconstruct the pre-task Core from the release + audited hash-diff snapshot."""
import csv,hashlib,json,shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
AUDIT=ROOT/'outputs/release-diff-20261027'
RELEASE=ROOT/'Artifacts/GameplayUiRelease20261026/FrozenSource03/UnityClient'
OUT=ROOT/'outputs/boss-behavior-20261027/calibration/before-source'
TARGET=OUT/'CoreSnapshot'
PREFIX='Assets/_Project/Core/Runtime/'
hash_file=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
baseline={r['Path'].replace('\\','/'):r for r in csv.DictReader((ROOT/'Artifacts/GameplayUiRelease20261026/Snapshot03/source-manifest.csv').open(encoding='utf-8-sig'))}
changes={r['path']:r for r in json.loads((AUDIT/'file-diff.json').read_text(encoding='utf-8-sig'))}
keys=sorted(p for p in set(baseline)|set(changes) if p.startswith(PREFIX) and p.endswith('.cs'))
TARGET.mkdir(parents=True,exist_ok=True)
records=[]
for key in keys:
    change=changes.get(key)
    if change and change['change']=='deleted':continue
    source=Path(change['currentSnapshot']) if change else RELEASE/key
    expected=change['after']['sha256'] if change else baseline[key]['SHA256']
    assert hash_file(source)==expected,key
    target=TARGET/Path(key).name
    if target.exists():assert hash_file(target)==expected,'Refuse to overwrite changed snapshot: '+str(target)
    else:shutil.copyfile(source,target)
    assert hash_file(target)==expected
    records.append({'Source':str(source),'Snapshot':str(target),'SHA256':expected})
assert len(records)>140
with (OUT/'source-hashes.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=['Source','Snapshot','SHA256']);w.writeheader();w.writerows(records)
(OUT/'snapshot.json').write_text(json.dumps({'files':len(records),'origin':'r03 baseline overlaid with release-diff-20261027 frozen Core; no live source reads','modifiedCoreFiles':sum(k in changes for k in keys),'mainProjectModified':False},indent=2),encoding='utf-8')
print(json.dumps({'files':len(records),'snapshot':str(TARGET)}))
