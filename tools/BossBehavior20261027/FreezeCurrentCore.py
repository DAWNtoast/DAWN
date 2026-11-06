"""Freeze live Core only after every file is unchanged across capture and verification."""
import argparse, csv, hashlib, json
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--tag',required=True)
args=parser.parse_args()
assert args.tag.replace('-','').replace('_','').isalnum()
root=Path(__file__).resolve().parents[2]
source=root/'UnityClient/Assets/_Project/Core/Runtime'
destination=root/'outputs/boss-behavior-20261027/calibration'/args.tag
assert not destination.exists(),'Choose a fresh snapshot tag.'
def capture():return {p.name:p.read_bytes() for p in sorted(source.glob('*.cs'))}
for attempt in range(1,4):
    values=capture()
    assert len(values)>=140
    if values==capture():break
else:raise RuntimeError('Core kept changing; no snapshot published.')
core=destination/'CoreSnapshot';core.mkdir(parents=True)
rows=[]
for name,data in values.items():
    (core/name).write_bytes(data)
    rows.append({'Name':name,'SHA256':hashlib.sha256(data).hexdigest()})
with (destination/'source-hashes.csv').open('w',encoding='utf-8',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=['Name','SHA256']);writer.writeheader();writer.writerows(rows)
metadata={'source':str(source),'files':len(rows),'captureAttempt':attempt,'verifiedStableAcrossTwoReads':True}
(destination/'snapshot.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
print(json.dumps(metadata))
