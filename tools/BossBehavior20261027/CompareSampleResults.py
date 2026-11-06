"""Compare all substantive CSV fields and actual battle traces for shared sample keys."""
import argparse,csv,hashlib,json
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('--reference',type=Path,required=True)
parser.add_argument('--candidate',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--allow-extra-candidate',action='store_true')
args=parser.parse_args()
def read(path):
    with (path/'samples.csv').open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
    values={tuple(r[k] for k in ['cohort','loadout','school','target','policy','seed']):r for r in rows}
    assert len(values)==len(rows)
    return values
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
a=read(args.reference);b=read(args.candidate)
assert a.keys()<=b.keys()
assert args.allow_extra_candidate or a.keys()==b.keys()
changes=[];trace_changes=[]
for key,old in a.items():
    new=b[key]
    fields={k:{'reference':v,'candidate':new[k]} for k,v in old.items() if k!='trace' and new[k]!=v}
    if fields:changes.append({'key':list(key),'fields':fields})
    oldtrace=args.reference/Path(old['trace'].replace('\\','/'))
    newtrace=args.candidate/Path(new['trace'].replace('\\','/'))
    if digest(oldtrace)!=digest(newtrace):trace_changes.append(list(key))
oldsource={p.name:digest(p) for p in (args.reference/'CoreSnapshot').glob('*.cs')}
newsource={p.name:digest(p) for p in (args.candidate/'CoreSnapshot').glob('*.cs')}
source_changes=[name for name in sorted(oldsource.keys()|newsource.keys()) if oldsource.get(name)!=newsource.get(name)]
result={'reference':str(args.reference),'candidate':str(args.candidate),'comparedSamples':len(a),
        'candidateAdditionalSamples':len(b)-len(a),'allSubstantiveCsvFieldsEqual':not changes,
        'allBattleTracesEqual':not trace_changes,'sourceDifferences':source_changes,
        'sampleDifferences':changes,'traceDifferences':trace_changes}
args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['sampleDifferences','traceDifferences']},ensure_ascii=False))
