import csv
import hashlib
import json
import math
from pathlib import Path

root = Path('D:/Game/outputs/boarding-budget-balance-20261027')
runs = ['FinalBoard44Direct','FinalBoard44Soft','FinalNaval45','Curse46']
report = {'runs': runs, 'samples': 0, 'sourceFiles': 0, 'pairedInitialStates': 0,
          'enemyCarryoverChecks': 0, 'directSoftDeckPairs': 0, 'violations': []}
source_sets=[]
input_sets=[]
for run in runs:
    folder=root/run
    records=list(csv.DictReader((folder/'source-hashes.csv').open(encoding='utf-8-sig')))
    manifest={r['Source']:r['SHA256'].lower() for r in records}
    source_sets.append(manifest)
    inputs={}
    for row in csv.DictReader((folder/'input-hashes.csv').open(encoding='utf-8-sig')):
        path=Path(row['Path'])
        assert hashlib.sha256(path.read_bytes()).hexdigest()==row['Hash'].lower()
        relative=path.relative_to(folder).as_posix()
        inputs[relative]=row['Hash'].lower()
        live=Path('D:/Game/tools/BoardingBudgetBalance20261027')/path.name if relative.startswith('Inputs/') else Path('D:/Game/UnityClient/Assets/_Project')/relative
        assert hashlib.sha256(live.read_bytes()).hexdigest()==row['Hash'].lower(), ('input drift',live)
    input_sets.append(inputs)
    for row in records:
        for path in [row['Source'],row['Snapshot']]:
            actual=hashlib.sha256(Path(path).read_bytes()).hexdigest()
            assert actual==row['SHA256'].lower(), ('source drift',run,path)
    assert not (folder/'Build').exists(), ('build remains',run)
assert all(s==source_sets[0] for s in source_sets)
assert all(s==input_sets[0] for s in input_sets)
report['sourceFiles']=len(source_sets[0])
report['toolAndDataInputs']=len(input_sets[0])
data={run:json.loads((root/run/'samples.json').read_text(encoding='utf-8-sig')) for run in runs}
common=('ship','level','hull','maxHull','armor','firepower','ordinaryCrew','crew','components',
        'environment','commonExistingUpgrades','specialXP','powder','balls','wood','medicine','cargoVolume','capacity')
baseline={}
boarding_pairs={}
for run,rows in data.items():
    report['samples']+=len(rows)
    for r in rows:
        assert not r['skippedNaval']
        assert r.get('diagnosticEnemyMaxScale',1)==1 and r.get('diagnosticEnemyAttackScale',1)==1
        assert r['budget']==r['spent']==r['budgetLedger']['actualSpent']
        assert r['budgetLedger']['unused']==0
        assert r['budgetLedger']['newShipCost']==r['budgetLedger']['newComponentCost']==0
        assert r['budgetLedger']['cardUpgradeCost']==r['spent']
        if r['mode']!='cannon':
            assert all(x['id'].startswith('card_boarding_') for x in r['budgetLedger']['upgrades'])
        state={k:r['initial'][k] for k in common}
        key=(r['cohort'],r['boss'],r['seed'])
        if key in baseline:
            assert state==baseline[key], ('initial mismatch',run,key)
            report['pairedInitialStates']+=1
        else: baseline[key]=state
        if run!='Curse46' and r['mode']!='cannon':
            deck=(r['initial']['boarding'],r['initial']['upgrades'])
            if key in boarding_pairs:
                assert deck==boarding_pairs[key], ('boarding loadout differs',key)
                report['directSoftDeckPairs']+=1
            else: boarding_pairs[key]=deck
        if r['boardingEntryNavalTurn']>=0:
            loss=min(r['enemyMaxHull'],max(0,r['startEnemyHull']-r['boardingEntryEnemyHull']))
            expected=max(1,math.ceil(r['enemyMaxCrew']*(2*r['enemyMaxHull']-loss)/(2*r['enemyMaxHull'])))
            assert expected==r['boardingEntryEnemyCrew'], ('enemy carryover',run,key,expected,r['boardingEntryEnemyCrew'])
            report['enemyCarryoverChecks']+=1
cleanup=[]
for path in root.rglob('cleanup.json'):
    item=json.loads(path.read_text(encoding='utf-8-sig'))
    if 'ReleasedBytes' not in item: continue
    assert not item.get('BuildRemaining',False), path
    assert not (path.parent/'Build').exists(), path
    cleanup.append({'record':str(path),'releasedBytes':item['ReleasedBytes']})
report['cleanupRecords']=len(cleanup)
report['outputBuildReleasedBytes']=sum(x['releasedBytes'] for x in cleanup)
report['cleanup']=cleanup
report['pass']=True
(root/'independent-final-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='cleanup'},ensure_ascii=False))
