"""Stage validated 4K images/meta in scratch; install into production only after acceptance."""
import argparse,hashlib,json,re,shutil
from pathlib import Path
from PIL import Image
ROOT=Path('D:/Game');OUT=ROOT/'Artifacts/BackgroundClarity4K20261013'
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
parser=argparse.ArgumentParser();parser.add_argument('--install',action='store_true');args=parser.parse_args()
target=ROOT/'UnityClient' if args.install else ROOT/'tmp/card-clarity-20261013'
assert target.is_dir()
if args.install:
 report=json.loads((OUT/'verification/capture/report.json').read_text(encoding='utf-8-sig'))
 assert report['status']=='passed' and report['passedFrames']==19 and not report['errors']
 assert (OUT/'visual-acceptance.json').exists(),'Visual acceptance is required'
 accepted=json.loads((OUT/'visual-acceptance.json').read_text(encoding='utf-8'))
 assert accepted['status']=='passed' and accepted['count']==19
staged=[]
for item in manifest['items']:
 source=Path(item['source']);meta=Path(str(source)+'.meta');final=Path(item['finalOutput'])
 assert sha(source)==item['beforeSha256'],'Production source changed: '+str(source)
 assert sha(meta)==item['beforeMetaSha256'],'Production metadata changed: '+str(meta)
 with Image.open(final) as im:assert im.size==(3840,2160)
 old=meta.read_text(encoding='utf-8-sig');new=old
 if item['group']!='A02':
  assert re.findall(r'(?m)^\s+maxTextureSize: (\d+)$',old)==['2048','2048','2048']
  new=re.sub(r'(?m)^(\s+maxTextureSize:) 2048$',r'\1 4096',new)
 if item['group'] in ['A01','A06']:
  assert new.count('        width: 1672\n        height: 941')==1
  new=new.replace('        width: 1672\n        height: 941','        width: 3840\n        height: 2160')
 guid=re.search(r'(?m)^guid: (\w+)$',old).group(1)
 assert re.search(r'(?m)^guid: (\w+)$',new).group(1)==guid
 staged.append((item,source.relative_to(ROOT/'UnityClient'),new,guid))
for item,relative,new,guid in staged:
 dest=target/relative;dest.parent.mkdir(parents=True,exist_ok=True)
 shutil.copy2(item['finalOutput'],dest)
 metadest=Path(str(dest)+'.meta')
 if metadest.read_text(encoding='utf-8-sig')!=new:metadest.write_text(new,encoding='utf-8',newline='\n')
 item['finalSha256']=sha(dest);item['installedMetaSha256']=sha(metadest);item['guid']=guid
 item['status']='installed' if args.install else 'staged_for_validation'
manifest['status']='installed_and_validated' if args.install else 'staged_for_validation'
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'mode':'install' if args.install else 'stage','files':len(staged),'target':str(target)}))
