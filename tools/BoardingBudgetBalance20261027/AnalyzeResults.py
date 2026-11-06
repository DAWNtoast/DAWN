import argparse, csv, json, math, statistics
from collections import Counter, defaultdict
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('run',type=Path)
a=parser.parse_args(); p=a.run.resolve()
rows=json.loads((p/'samples.json').read_text(encoding='utf-8-sig'))
groups=defaultdict(list)
for r in rows:
    groups[(r['cohort'],r['boss'],r['mode'],r.get('diagnosticEnemyMaxScale',1),r.get('diagnosticEnemyAttackScale',1))].append(r)

def wilson(k,n):
    z=1.96; d=1+z*z/n; mid=(k/n+z*z/(2*n))/d
    half=z*math.sqrt(k/n*(1-k/n)/n+z*z/(4*n*n))/d
    return [round(max(0,mid-half),4),round(min(1,mid+half),4)]

summary=[]
for (cohort,boss,mode,scale,attack_scale),v in groups.items():
    outcomes=Counter(r['outcome'] for r in v)
    captured=sum(r['outcome']=='captured' for r in v)
    inband=[r for r in v if .5-1e-9<=r.get('boardingEntryEnemyRatio',-1)<=.6+1e-9 and not r.get('skippedNaval',False)]
    entries=[r for r in v if r.get('boardingEntryEnemyRatio',-1)>=0]
    is_naval=mode.startswith('cannon') or mode=='naval'
    success=sum(r['outcome']=='sunk' for r in v) if is_naval else captured
    qualified=sum(r['outcome']=='captured' for r in inband)
    item=dict(cohort=cohort,boss=boss,name=v[0]['name'],mode=mode,scale=scale,attackScale=attack_scale,n=len(v),success=success,
              rate=round(success/len(v),4),wilson95=wilson(success,len(v)),outcomes=dict(outcomes),
              inBandEntries=len(inband),inBandCaptures=qualified,inBandCaptureRate=round(qualified/len(v),4),
              navalFailures=sum(r.get('failureStage') in ('Naval','Preparation') for r in v),
              meanEntryHullRatio=round(statistics.mean(r['boardingEntryEnemyRatio'] for r in entries),4) if entries else None,
              meanEntryPlayerCrewRatio=round(statistics.mean(r.get('boardingEntryPlayerCrewRatio',0) for r in entries),4) if entries else None,
              diagnostic=any(r.get('skippedNaval',False) for r in v))
    summary.append(item)
(p/'independent-outcomes.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
fields=[k for k in summary[0] if k not in ('outcomes','wilson95')] if summary else []
with (p/'independent-outcomes.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=fields,extrasaction='ignore'); w.writeheader(); w.writerows(summary)
print('run='+p.name+' samples='+str(len(rows)))
for r in summary:
    print(r['cohort'],r['name'],r['mode'],'scale='+str(r['scale']),str(r['success'])+'/'+str(r['n']),r['outcomes'],'band='+str(r['inBandCaptures'])+'/'+str(r['n']))
