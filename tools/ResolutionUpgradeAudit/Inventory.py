import json,re,hashlib,collections
from pathlib import Path
from PIL import Image
root=Path('D:/Game/UnityClient/Assets/_Project/Resources')
out=Path('D:/Game/Artifacts/ResolutionUpgradeAudit20261013')
rows=[]
for p in sorted(root.rglob('*')):
 if p.suffix.lower() not in ('.png','.jpg','.jpeg','.webp'):continue
 try:
  with Image.open(p) as im:w,h=im.size
 except Exception:continue
 meta=Path(str(p)+'.meta').read_text(encoding='utf-8-sig') if Path(str(p)+'.meta').exists() else ''
 def val(t,k):
  m=re.search(r'^\s*'+k+r':\s*(-?\d+)',t,re.M);return int(m[1]) if m else None
 platform=[]
 for block in re.split(r'\n  - serializedVersion:',meta)[1:]:
  m=re.search(r'buildTarget:\s*(\S+)',block)
  if m:platform.append({'target':m[1],'maxSize':val(block,'maxTextureSize'),'overridden':val(block,'overridden'),'format':val(block,'textureFormat'),'compression':val(block,'textureCompression')})
 rows.append({'resource':p.relative_to(root).with_suffix('').as_posix(),'file':p.as_posix(),'width':w,'height':h,'bytes':p.stat().st_size,'modifiedNs':p.stat().st_mtime_ns,'maxSize':val(meta,'maxTextureSize'),'npotScale':val(meta,'nPOTScale'),'mipmaps':val(meta,'enableMipMap'),'filter':val(meta,'filterMode'),'platforms':platform})
(out/'current-source-inventory.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
groups=collections.defaultdict(list)
for r in rows:groups[r['resource'].rsplit('/',1)[0]].append(r)
print('Raster resource files:',len(rows))
for g,rs in groups.items():
 dims=collections.Counter(str(r['width'])+'x'+str(r['height']) for r in rs)
 print(g,len(rs),dict(dims))
