"""Synchronize an idle shared validation checkout, preserving differing inputs."""
import csv,hashlib,json,os,shutil,stat
from pathlib import Path
root=Path(__file__).resolve().parents[2]
source=root/'UnityClient';project=root/'tmp/shipyard-detail-clean-20261011'
evidence=root/'Artifacts/CampaignAudit20261027/Snapshot'
parts=['Assets','Packages','ProjectSettings']
retired='Assets/_Project/Scenes/UIPreviewsForMe'
def unlinked(path):
    node=path.resolve(strict=True)
    assert node==path.absolute(),f'Path resolves elsewhere: {path}'
    for item in [path,*path.parents]:
        assert not (item.stat(follow_symlinks=False).st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT),f'Linked path: {item}'
def files(base):
    result={}
    for part in parts:
        start=base/part;unlinked(start)
        for directory,dirs,names in os.walk(start,followlinks=False):
            for name in dirs+names:
                path=Path(directory)/name
                assert not(path.stat(follow_symlinks=False).st_file_attributes&stat.FILE_ATTRIBUTE_REPARSE_POINT),f'Linked input: {path}'
            rel=Path(directory).relative_to(base).as_posix()
            if rel==retired:
                assert not dirs and not names,'Retired directory must stay empty.'
                continue
            for name in names:
                path=Path(directory)/name;rel=path.relative_to(base).as_posix()
                if rel==retired+'.meta':continue
                result[rel]=path
    return result
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not evidence.exists(),'Fresh snapshot required.'
unlinked(source);unlinked(project)
current=files(source);old=files(project)
evidence.mkdir(parents=True)
manifest=[];archived=[]
for rel,path in current.items():manifest.append({'Path':rel,'Bytes':path.stat().st_size,'SHA256':sha(path)})
expected={row['Path']:row for row in manifest}
for rel,path in old.items():
    oldsha=sha(path)
    if rel in expected and oldsha==expected[rel]['SHA256']:continue
    archive=evidence/'PreviousValidationInputs'/rel
    assert archive.absolute().is_relative_to(evidence.absolute())
    archive.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(path,archive);assert sha(archive)==oldsha
    archived.append({'Path':rel,'Bytes':path.stat().st_size,'SHA256':oldsha})
    if rel not in expected:
        assert path.absolute().is_relative_to(project.absolute())
        path.unlink()
for row in manifest:
    rel=row['Path'];path=current[rel];target=project/rel
    if not target.exists() or sha(target)!=row['SHA256']:
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
    assert sha(target)==row['SHA256']
    # Preserve code, metadata and configuration, while retaining asset inputs by manifest.
    if Path(rel).suffix.lower() in ['.cs','.meta','.asmdef','.asmref','.json','.txt'] or rel.startswith(('ProjectSettings/','Packages/')):
        frozen=evidence/'FrozenCode'/rel;frozen.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,frozen)
assert current.keys()==files(source).keys()
for row in manifest:assert sha(current[row['Path']])==row['SHA256'],'Main source changed while copying.'
for name,rows in [('source-manifest.csv',manifest),('archived-inputs.csv',archived)]:
    with (evidence/name).open('w',encoding='utf-8',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=['Path','Bytes','SHA256']);writer.writeheader();writer.writerows(rows)
settings=project/'ProjectSettings/ProjectSettings.asset'
import re
text=settings.read_text(encoding='utf-8-sig')
text=re.sub(r'^  companyName:.*$','  companyName: ShatteredSailCampaignAudit',text,flags=re.M)
text=re.sub(r'^  productName:.*$','  productName: Campaign20261027NewGame',text,flags=re.M)
settings.write_text(text,encoding='utf-8')
metadata={'Result':'PREPARED','Source':str(source),'Project':str(project),'Files':len(manifest),'ArchivedInputs':len(archived),
          'SourceManifestSHA256':sha(evidence/'source-manifest.csv'),'Company':'ShatteredSailCampaignAudit','Product':'Campaign20261027NewGame',
          'FrozenCode':str(evidence/'FrozenCode'),'AssetPolicy':'Asset bytes synchronized into existing isolated checkout and covered by full source manifest; text/code/config/meta separately frozen.',
          'CachePolicy':'Reuse existing shared editor import/package cache; do not duplicate or remove whole Library. Task caches cleaned after validation.'}
(evidence/'preparation.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(metadata,ensure_ascii=False))
