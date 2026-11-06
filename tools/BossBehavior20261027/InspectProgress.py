"""Read complete sample-end rows from a running probe, without touching it."""
import argparse,collections,csv,io,json
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('directory',type=Path);args=parser.parse_args()
text=(args.directory/'turn-observations.csv').read_text(encoding='utf-8-sig')
text=text[:text.rfind('\n')+1]
samples=[r for r in csv.DictReader(io.StringIO(text)) if r['window']=='sample_end']
def outcome(r):
    if int(r['playerHull'])<=0:return 'dead'
    if int(r['enemyHull'])<=0:return 'sunk'
    if r['stage']=='Boarding':return 'boarding'
    return 'other'
print(json.dumps({'completed':len(samples),'outcomes':dict(collections.Counter(map(outcome,samples))),
                  'deathsByBoss':dict(collections.Counter(r['boss'] for r in samples if outcome(r)=='dead'))},ensure_ascii=False))
