"""Compare the sealed r03 input manifest with current Unity inputs, without Unity."""
import concurrent.futures, csv, datetime, difflib, hashlib, json, os
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/release-diff-20261027'
LIVE=ROOT/'UnityClient'
OLD=ROOT/'Artifacts/GameplayUiRelease20261026/FrozenSource03/UnityClient'
MANIFEST=ROOT/'Artifacts/GameplayUiRelease20261026/Snapshot03/source-manifest.csv'
RELEASE=ROOT/'Builds/第七港口-场地与界面更新20261026-r03/release-manifest.json'
TEXT={'.cs','.shader','.cginc','.hlsl','.asmdef','.json','.txt','.csv','.asset','.prefab','.unity','.meta','.md','.xml','.rsp','.uss','.uxml'}
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for block in iter(lambda:f.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def save(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def classify(p):
    if p.endswith('.meta'):return 'metadata'
    if '/Core/Runtime/' in p:return 'core'
    if '/Content/Runtime/' in p:return 'content'
    if '/Presentation/Runtime/' in p:return 'presentation'
    if '/Tests/' in p:return 'tests'
    if '/Editor/' in p:return 'editor'
    if '/Resources/' in p:return 'resources'
    if '/Scenes/' in p:return 'scenes'
    if '/Prefabs/' in p:return 'prefabs'
    if p.startswith(('Packages/','ProjectSettings/')):return 'configuration'
    return 'other'

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    if (OUT/'file-diff.json').exists():raise RuntimeError('Preserve existing audit; use its frozen evidence.')
    release=load(RELEASE)
    assert digest(MANIFEST)==release['SourceManifestSHA256']
    old={r['Path'].replace('\\','/'):{'bytes':int(r['Bytes']),'sha256':r['SHA256']} for r in csv.DictReader(MANIFEST.open(encoding='utf-8-sig'))}
    paths=[];excluded=[]
    for part in ('Assets','Packages','ProjectSettings'):
        for directory,dirs,files in os.walk(LIVE/part,followlinks=False):
            for name in list(dirs):
                p=Path(directory)/name
                if name=='UIPreviewsForMe':dirs.remove(name);excluded.append(str(p.relative_to(LIVE)));continue
                assert not p.is_symlink() and not (hasattr(p,'is_junction') and p.is_junction()),str(p)
            for name in files:
                p=Path(directory)/name
                if name=='UIPreviewsForMe.meta':excluded.append(str(p.relative_to(LIVE)));continue
                assert not p.is_symlink(),str(p)
                paths.append(p)
    def inspect(p):return p.relative_to(LIVE).as_posix(),{'bytes':p.stat().st_size,'sha256':digest(p)}
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as executor:current=dict(executor.map(inspect,paths))
    changes=[]
    for relative in sorted(set(old)|set(current)):
        before=old.get(relative);after=current.get(relative)
        if before and after and before['sha256']==after['sha256']:continue
        entry={'path':relative,'change':'added' if before is None else 'removed' if after is None else 'modified','category':classify(relative),'before':before,'after':after}
        oldpath=OLD/relative;live=LIVE/relative
        if before:assert oldpath.exists() and digest(oldpath)==before['sha256'],str(oldpath)
        if Path(relative).suffix.lower() in TEXT:
            oldbytes=oldpath.read_bytes() if before else b'';newbytes=live.read_bytes() if after else b''
            if after:assert hashlib.sha256(newbytes).hexdigest().upper()==after['sha256'],'Input changed during capture: '+relative
            frozen=OUT/'current-snapshot/UnityClient'/relative
            frozen.parent.mkdir(parents=True,exist_ok=True)
            if after:frozen.write_bytes(newbytes);entry['currentSnapshot']=str(frozen)
            patch=OUT/'diffs'/(relative+'.diff');patch.parent.mkdir(parents=True,exist_ok=True)
            patch.write_text(''.join(difflib.unified_diff(oldbytes.decode('utf-8-sig',errors='replace').splitlines(True),newbytes.decode('utf-8-sig',errors='replace').splitlines(True),fromfile='release-r03/'+relative,tofile='current/'+relative)),encoding='utf-8')
            entry['diff']=str(patch)
        changes.append(entry)
    with (OUT/'current-source-manifest.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['Path','Bytes','SHA256'])
        for p,r in sorted(current.items()):writer.writerow([p,r['bytes'],r['sha256']])
    summary={'capturedAt':datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat(),'releaseVersion':release['Version'],'releaseDirectory':str(RELEASE.parent),'releaseManifest':str(RELEASE),'releaseSourceManifest':str(MANIFEST),'releaseSourceManifestSha256':digest(MANIFEST),'baselineFiles':len(old),'currentFiles':len(current),'changedFiles':len(changes),'unchangedFiles':len(set(old)&set(current))-sum(e['change']=='modified' for e in changes),'changeCounts':{k:sum(e['change']==k for e in changes) for k in ('added','modified','removed')},'categories':{k:sum(e['category']==k for e in changes) for k in sorted({e['category'] for e in changes})},'excludedRetiredPaths':excluded,'gameCodeModifiedByAudit':False}
    save(OUT/'file-diff.json',changes);save(OUT/'comparison-summary.json',summary)
    print(json.dumps(summary,ensure_ascii=True,indent=2))
if __name__=='__main__':main()
