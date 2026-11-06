"""Keep original death assets byte-identical and publish verified four-action Unity captures."""
import argparse
import csv
import hashlib
import json
import os
import shutil
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/boarding-boss-actions-20261106'
CAPTURE = OUT / 'validation'
ORIGINAL = ROOT / 'outputs/boarding-remaster-20261106/validation/named-enemies'
BACKUP = OUT / 'source-before'
DATA = ROOT / 'UnityClient/Assets/_Project/Resources/BoardingBattle/Defeats'
ACTIONS = ('Attack', 'Hit', 'Cast', 'Guard')
SCRATCH = ROOT / 'tmp/shipyard-assembly-20261017'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def safe(path, base):
    if not path.is_relative_to(base) or path.resolve() != path or any(
        parent.is_symlink() or getattr(parent,'is_junction',lambda:False)() for parent in (path,*path.parents)):
        raise ValueError('Unexpected path: '+str(path))

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def write(path, data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')

def snapshot():
    safe(BACKUP,OUT)
    if (BACKUP/'files.json').exists():
        verify(True)
        return
    manifest=read(ORIGINAL/'manifest.json')
    gifs=[ORIGINAL/c['gif'] for c in manifest['clips']]
    deaths=[p for p in gifs if p.name=='Death.gif']
    sources=sorted(DATA.glob('*.png'))
    if len(set(gifs))!=65 or len(deaths)!=13 or len(sources)!=13:
        raise ValueError('Previous named roster is incomplete')
    paths=[ORIGINAL/'manifest.json',*gifs,DATA.with_suffix('.meta'),*sources,*[Path(str(p)+'.meta') for p in sources]]
    records=[]
    for path in paths:
        safe(path,ROOT)
        rel=path.relative_to(ROOT)
        destination=BACKUP/rel
        safe(destination,BACKUP)
        destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(path,destination)
        digest=sha(path)
        if digest!=sha(destination):
            raise ValueError(path)
        records.append(dict(path=rel.as_posix(),hash=digest,bytes=path.stat().st_size,
            frozen=(path in deaths or path.parent==DATA or path==DATA.with_suffix('.meta'))))
    write(BACKUP/'files.json',records)
    verify(True)

def verify(include_mutable=False):
    records=read(BACKUP/'files.json')
    for rec in records:
        if (include_mutable or rec['frozen']) and sha(ROOT/rec['path'])!=rec['hash']:
            raise ValueError('Unintended change: '+rec['path'])
        if sha(BACKUP/rec['path'])!=rec['hash']:
            raise ValueError('Corrupt backup: '+rec['path'])
    print('Verified',len(records),'originals;',sum(r['frozen'] for r in records),'protected files.')

def record_inputs(run):
    source = CAPTURE/run
    safe(source, CAPTURE)
    if not (source/'launch.json').is_file(): raise ValueError(source)
    rows = list(csv.DictReader((CAPTURE/'main-source-hashes.csv').read_text(encoding='utf-8-sig').splitlines()))
    sources = {}
    for row in rows:
        path = Path(row['Path'])
        if path.parent != DATA.parent/'BossActions' or path.suffix != '.png': continue
        relative = path.relative_to(ROOT/'UnityClient')
        actual = sha(SCRATCH/relative)
        if actual != row['SHA256'].lower(): raise ValueError('Different from synced source: '+str(relative))
        sources[path.stem] = actual
    if len(sources) != 13: raise ValueError('Incomplete captured inputs')
    path = source/'boss-action-inputs.json'
    if path.exists() and read(path)!=sources: raise ValueError('Previous captured inputs differ: '+str(path))
    write(path,sources)

def current_inputs(source):
    sources = read(source/'boss-action-inputs.json')
    for identity,expected in sources.items():
        path = DATA.parent/'BossActions'/(identity+'.png')
        if sha(path)!=expected: raise ValueError('The live action art was revised after capture: '+str(path))
    return sources

def release_smoke():
    source = CAPTURE/'iron-smoke/liveFrames'
    safe(source, CAPTURE)
    report = read(source/'live-report.json')
    clip, = report['clips']
    final = next(entry for entry in read(CAPTURE/'gameplay/manifest.json')['clips'] if entry['id']==clip['id'])
    if clip['id'] != 'iron_hit' or final['sourceCard'] != clip['sourceCard'] or sha(CAPTURE/'gameplay'/final['video'])!=final['sha256']:
        raise ValueError('Missing verified replacement recording')
    audit = source/'superseded-frames.json'
    if audit.exists(): return
    files = [source/name for name in clip['frames']]
    if not files or sorted(files) != sorted(source.glob('*.jpg')): raise ValueError('Unexpected sample files')
    rows = []
    for index,path in enumerate(files):
        safe(path, source)
        if path.name != f'iron_hit_{index:03d}.jpg': raise ValueError(path)
        rows.append(dict(name=path.name,hash=sha(path),bytes=path.stat().st_size))
    write(audit,dict(replacement=final['video'],reclaimedBytes=sum(row['bytes'] for row in rows),frames=rows))
    for path,row in zip(files,rows):
        if sha(path)!=row['hash']: raise ValueError(path)
        path.unlink()

def release_draft(run, replacement):
    source = CAPTURE/run
    final = CAPTURE/replacement
    safe(source,CAPTURE)
    safe(final,CAPTURE)
    if source==final or not read(final/'media-verification.json')['allOutputHashesMatch']:
        raise ValueError('An installed and verified replacement capture is required')
    before=read(source/'encoded/manifest.json')['clips']
    after=read(final/'encoded/manifest.json')['clips']
    if len(before)!=52 or {(c['identity'],c['action']) for c in before}!={(c['identity'],c['action']) for c in after}:
        raise ValueError('Different capture coverage')
    audit=source/'superseded-media.json'
    if audit.exists(): return
    files={}
    for clip in before:
        for p,digest in ((source/clip['sheet'],clip['sheetSha256']),(source/'encoded'/clip['gif'],clip['sha256'])):
            safe(p,source)
            if sha(p)!=digest: raise ValueError(p)
            files[p]=p.stat().st_size
    for p in files: p.unlink()
    write(audit,dict(replacement=replacement,verifiedIntermediates=len(files),reclaimedBytes=sum(files.values())))

def encode(run):
    verify()
    source=CAPTURE/run
    safe(source,CAPTURE)
    input_hashes = current_inputs(source)
    output=source/'encoded'
    report=read(source/'capture-report.json')
    keys={(c['identity'],c['action']) for c in report['clips']}
    ids={p.stem for p in DATA.glob('*.png')}
    if keys!={(i,a) for i in ids for a in ACTIONS}:
        raise ValueError('Incomplete four-action capture')
    width,height,frames,cols=[report[k] for k in ('width','height','frames','columns')]
    entries=[]
    for clip in report['clips']:
        identity,action=clip['identity'],clip['action']
        file=source/clip['sheet']
        safe(file,source)
        if file.name!=identity+'_'+action+'.png': raise ValueError(file)
        with Image.open(file) as image: sheet=image.convert('RGB')
        if sheet.size!=(width*cols,height*(frames//cols)):raise ValueError(file)
        cells=[sheet.crop((i%cols*width,i//cols*height,(i%cols+1)*width,(i//cols+1)*height)) for i in range(frames)]
        if len({hashlib.sha256(c.tobytes()).digest() for c in cells})<5:
            raise ValueError('Insufficient distinct Unity poses: '+str(file))
        palette=sheet.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
        quantized=[c.quantize(palette=palette,dither=Image.Dither.NONE) for c in cells]
        rel=identity+'/'+action+'.gif'
        target=output/rel
        safe(target,output)
        target.parent.mkdir(parents=True,exist_ok=True)
        duration=max(20,round(1000*clip['duration']/(frames-1)/10)*10)
        timing=[duration]*frames
        timing[0]+=140
        timing[-1]+=220
        quantized[0].save(target,save_all=True,append_images=quantized[1:],duration=timing,loop=0,optimize=False,disposal=1)
        with Image.open(target) as gif:
            if gif.n_frames<5 or gif.size!=(width,height):raise ValueError(rel)
            for j in range(gif.n_frames): gif.seek(j);gif.load()
            decoded=gif.n_frames
        if sha(target)==sha(BACKUP/(ORIGINAL/rel).relative_to(ROOT)):raise ValueError('Old GIF '+rel)
        entries.append(dict(**clip,renderRun=run,gif=rel,sha256=sha(target),actionAtlasSha256=input_hashes[identity],
            sheetSha256=sha(file),sheetBytes=file.stat().st_size,decodedFrames=decoded))
    write(output/'manifest.json',dict(scope=report['scope'],width=width,height=height,frames=frames,clips=entries))
    print('Validated',len(entries),'new GIFs.')

def install(run):
    source=CAPTURE/run
    safe(source,CAPTURE)
    record=read(source/'encoded/manifest.json')
    current_inputs(source)
    old=read(BACKUP/(ORIGINAL/'manifest.json').relative_to(ROOT))
    verify()
    entries={(c['identity'],c['action']):c for c in record['clips']}
    if len(entries)!=52:raise ValueError('Expected all four actions across 13 casts')
    combined={**old,'clips':[entries.get((c['identity'],c['action']),c) for c in old['clips']]}
    for c in combined['clips']:
        p=ORIGINAL/c['gif']
        safe(p,ORIGINAL)
        if c['action']=='Death' and sha(p)!=c['sha256']:raise ValueError(p)
        if c['action']!='Death' and (c['action'] not in ACTIONS or sha(source/'encoded'/c['gif'])!=c['sha256']):raise ValueError(c)
    touched=[]
    try:
        for c in entries.values():
            target=ORIGINAL/c['gif']
            tmp=target.with_suffix('.boss-update-tmp')
            if tmp.exists(): raise ValueError(tmp)
            touched.append(target)
            shutil.copyfile(source/'encoded'/c['gif'],tmp)
            os.replace(tmp,target)
        m=ORIGINAL/'manifest.json'
        touched.append(m)
        tmp=m.with_suffix('.boss-update-tmp')
        write(tmp,combined)
        os.replace(tmp,m)
        verify()
        for c in combined['clips']:
            if sha(ORIGINAL/c['gif'])!=c['sha256']: raise ValueError(c['gif'])
    except Exception:
        for p in touched:
            shutil.copyfile(BACKUP/p.relative_to(ROOT),p)
            p.with_suffix('.boss-update-tmp').unlink(missing_ok=True)
        raise
    released=0
    for c in entries.values():
        for p,expected in ((source/c['sheet'],c['sheetSha256']),(source/'encoded'/c['gif'],c['sha256'])):
            safe(p,source)
            if sha(p)!=expected:raise ValueError(p)
            released+=p.stat().st_size
            p.unlink()
    write(source/'media-verification.json',dict(updated=52,protectedDeathGifs=13,protectedDefeatSheets=13,
        protectedDefeatMetas=14,stagingBytesReclaimed=released,allOutputHashesMatch=True))
    print('Installed 52; verified 13 previous death GIFs. Reclaimed',released,'bytes.')

if __name__=='__main__':
    p=argparse.ArgumentParser()
    p.add_argument('command',choices=['snapshot','verify','encode','install','record-inputs','release-smoke','release-draft'])
    p.add_argument('--run',default='capture1')
    p.add_argument('--replaced-by')
    a=p.parse_args()
    if a.command=='snapshot':snapshot()
    elif a.command=='verify':verify()
    elif a.command=='record-inputs':record_inputs(a.run)
    elif a.command=='release-smoke':release_smoke()
    elif a.command=='release-draft':release_draft(a.run,a.replaced_by)
    elif a.command=='encode':encode(a.run)
    else:install(a.run)
