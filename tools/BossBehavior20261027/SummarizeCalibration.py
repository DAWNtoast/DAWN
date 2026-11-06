"""Summarize finite policy samples; optionally require strictly paired A/B inputs."""
import argparse,collections,csv,hashlib,json,re,statistics
from pathlib import Path

def read(path):
    with path.open(encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))
def number(row,key):return float(row[key])
def key(row):return tuple(row[x] for x in ['cohort','loadout','school','target','policy','seed'])
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def observation_key(r):return tuple(r[x] for x in ['boss','school','startingHealthPercent','policy','seed'])
def sample_observation_key(r):
    boss,hp=r['target'].rsplit('-hp',1)
    return boss,r['school'],hp,r['policy'],r['seed']
def cycle_definitions(path):
    code=(path/'CoreSnapshot/CoreNavalBossBehavior.cs').read_text(encoding='utf-8-sig')
    chunk=code.split('switch (school)',1)[1].split('if (plan.Attack',1)[0]
    result={}
    for school,body in re.findall(r'(?:case "([^"]+)":|default:)(.*?)(?=\n\s*(?:case "|default:)|\Z)',chunk,re.S):
        school=school or 'treasure'
        arrays=re.findall(r'new\[\]\s*\{([^}]+)\}',body)
        if len(arrays)>=2:
            names=re.findall(r'"([^"]+)"',arrays[0]);stages=re.findall(r'"([^"]+)"',arrays[1])
            assert len(names)==len(stages) and stages.count('爆发')==1
            step=stages.index('爆发')+1
        else:
            call=re.search(r'NameNavalBossMove\(move, step,([^;]+)\);',body)
            assert call,school
            names=re.findall(r'"([^"]+)"',call[1]);step=3
            assert len(names)==4,'Review legacy first-burst assumption.'
        result[school]={'length':len(names),'firstBurstStep':step,'firstBurstName':names[step-1]}
    assert len(result)==7
    return result
def telemetry_metrics(path,rows):
    observations=read(path/'turn-observations.csv');groups=collections.defaultdict(list)
    for observation in observations:groups[observation_key(observation)].append(observation)
    profiles={r['id']:r for r in read(path/'boss-profiles.csv')}
    definitions=cycle_definitions(path);metrics={}
    for sample in rows:
        k=sample_observation_key(sample);items=groups.get(k,[]);boss=k[0]
        definition=definitions[profiles[boss]['school']]
        actions=max((int(r['actionsCompleted']) for r in items),default=0)
        windows=collections.defaultdict(dict)
        for item in items:windows[int(item['turn'])][item['window']]=item
        terminal=windows[int(sample['turns'])]
        missing_terminal_enemy=bool(terminal.get('before_enemy') and not terminal.get('after_enemy'))
        if 'sample_end' not in terminal:terminal['sample_end']={'playerHull':sample['endHP'],'recoveredFromResult':'true'}
        enemy_losses=[];round_losses=[];first_burst_turn=None
        for turn,window in windows.items():
            pre=window.get('before_enemy');post=window.get('after_enemy')
            health_post=post or (window.get('sample_end') if pre else None)
            if pre and health_post and pre['stage']=='Naval':
                enemy_losses.append(max(0,number(pre,'playerHull')-number(health_post,'playerHull'))/number(sample,'maxHP')*100)
            if pre and post and pre['stage']=='Naval':
                if int(pre['actionsCompleted'])<definition['firstBurstStep']<=int(post['actionsCompleted']):first_burst_turn=turn
            start=window.get('start_turn');end=window.get('sample_end',post)
            if start and end and start['stage']=='Naval':round_losses.append(max(0,number(start,'playerHull')-number(end,'playerHull'))/number(sample,'maxHP')*100)
        metrics[k]={'actionsCompleted':actions,'firstBurstExecuted':actions>=definition['firstBurstStep'],
                    'actionsAreLowerBound':missing_terminal_enemy,
                    'firstBurstPossiblyMissed':missing_terminal_enemy and actions==definition['firstBurstStep']-1,
                    'terminalEnemyHullWindowRecoveredFromResult':missing_terminal_enemy,
                    'firstBurstTurn':first_burst_turn,'maxEnemyWindowNetHullLossPercent':round(max(enemy_losses,default=0),2),
                    'maxRoundNetHullLossPercent':round(max(round_losses),2) if round_losses else None}
    return observations,metrics,profiles,definitions
def summarize_group(items,metrics):
    values=[metrics[sample_observation_key(r)] for r in items]
    kills=[r for r in items if r['outcome'] in ['sunk','kill']]
    return {'samples':len(items),'outcomes':dict(collections.Counter(r['outcome'] for r in items)),
            'medianTurnsOnKills':statistics.median(number(r,'turns') for r in kills) if kills else None,
            'meanTurns':round(statistics.mean(number(r,'turns') for r in items),2),
            'meanEndHullPercent':round(statistics.mean(number(r,'endHP')/number(r,'maxHP')*100 for r in items),2),
            'meanMinimumHullPercent':round(statistics.mean(number(r,'minimumHP')/number(r,'maxHP')*100 for r in items),2),
            'meanEnemyHullLostPercent':round(statistics.mean((number(r,'enemyMax')-number(r,'enemyHP'))/number(r,'enemyMax')*100 for r in items),2),
            'meanActionsCompleted':round(statistics.mean(r['actionsCompleted'] for r in values),2),
            'medianActionsCompleted':statistics.median(r['actionsCompleted'] for r in values),
            'maxActionsCompleted':max(r['actionsCompleted'] for r in values),
            'firstBurstExecutedSamples':sum(r['firstBurstExecuted'] for r in values),
            'firstBurstExecutionPercent':round(statistics.mean(r['firstBurstExecuted'] for r in values)*100,2),
            'actionCountLowerBoundSamples':sum(r['actionsAreLowerBound'] for r in values),
            'firstBurstPossiblyMissedSamples':sum(r['firstBurstPossiblyMissed'] for r in values),
            'terminalEnemyHullWindowsRecoveredFromResult':sum(r['terminalEnemyHullWindowRecoveredFromResult'] for r in values),
            'maxEnemyWindowNetHullLossPercent':max(r['maxEnemyWindowNetHullLossPercent'] for r in values),
            'maxRoundNetHullLossPercent':max((r['maxRoundNetHullLossPercent'] for r in values if r['maxRoundNetHullLossPercent'] is not None),default=None)}
def summary(path):
    rows=read(path/'samples.csv');groups=collections.defaultdict(list)
    for row in rows:groups[row['target'].rsplit('-hp',1)[0]].append(row)
    observations,metrics,profiles,definitions=telemetry_metrics(path,rows)
    result=[]
    for boss,items in groups.items():
        result.append({'boss':boss,**summarize_group(items,metrics),'cycle':definitions[profiles[boss]['school']],
                       'byStartingHealthPercent':{hp:summarize_group([r for r in items if r['target'].endswith('-hp'+hp)],metrics) for hp in ['100','70']},
                       'byFullHealthSchool':{s:summarize_group([r for r in items if r['school']==s and r['target'].endswith('-hp100')],metrics) for s in sorted({r['school'] for r in items})},
                       'bySchool':{s:summarize_group([r for r in items if r['school']==s],metrics) for s in sorted({r['school'] for r in items})}})
    coverage={str(t):len({(r['boss'],r['school'],r['startingHealthPercent'],r['seed'],r['policy']) for r in observations if r['turn']==str(t) and r['window']=='before_enemy'}) for t in [3,5,8,10,13,15,18,20,23,25,28,30]}
    data={**summarize_group(rows,metrics),'runParameters':json.loads((path/'run-parameters.json').read_text(encoding='utf-8-sig')),
          'bosses':result,'scheduledTurnCoverage':coverage,
          'limitation':'Finite deterministic captain policies. Boarding is a censored naval transition, not a win or death. First burst execution uses the real completed-action counter crossing the authored burst step (not a displayed intention or PhaseTurn). Older telemetry omitted observations when settlement cleared session.Battle: terminal health is recovered exactly from samples.csv, but action counts in those samples remain lower bounds; firstBurstPossiblyMissedSamples explicitly bounds ambiguous first bursts. New telemetry retains the original battle reference and has no such gaps. Enemy-window loss covers EndBattleTurn net change; it includes field/burn/start-turn effects and is not raw attack damage. Full-round net loss is present only when start_turn telemetry was captured and may net healing against damage; neither measure estimates peak intra-action damage. Long schedules require scenario tests when natural samples end early.'}
    (path/'sample-telemetry-metrics.json').write_text(json.dumps([{'boss':k[0],'school':k[1],'startingHealthPercent':int(k[2]),'policy':k[3],'seed':int(k[4]),**v} for k,v in metrics.items()],ensure_ascii=False,indent=2),encoding='utf-8')
    (path/'calibration-summary.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    return rows,data

parser=argparse.ArgumentParser();parser.add_argument('--before',type=Path,required=True);parser.add_argument('--after',type=Path)
parser.add_argument('--pair-common',action='store_true',help='Explicitly compare only shared keys when the new run adds seeds; report excluded counts.')
args=parser.parse_args();before,bs=summary(args.before)
if args.after:
    after,as_=summary(args.after);old={key(r):r for r in before};new={key(r):r for r in after}
    assert len(old)==len(before) and len(new)==len(after),'Duplicate pair keys.'
    if args.pair_common:
        paired_keys=old.keys()&new.keys()
        assert paired_keys and paired_keys==old.keys(),'All baseline sample keys must appear in the expanded candidate.'
    else:
        assert old.keys()==new.keys(),'Before and after samples not paired.'
        paired_keys=old.keys()
    for filename in ['calibration-loadouts.csv','decks.csv','used-decks.csv']:
        assert digest(args.before/filename)==digest(args.after/filename),'Player input changed: '+filename
    transitions=collections.Counter();changes=[]
    for k in sorted(paired_keys):
        a=old[k]
        b=new[k]
        for field in ['maxHP','initialHP','navalDeckSize']:
            assert a[field]==b[field],(k,field)
        transitions[(a['outcome'],b['outcome'])]+=1
        changes.append({'key':'|'.join(k),'before':a['outcome'],'after':b['outcome'],
                        'endHullDelta':number(b,'endHP')-number(a,'endHP'),
                        'enemyHullLostPercentDelta':round((1-number(b,'enemyHP')/number(b,'enemyMax'))*100-(1-number(a,'enemyHP')/number(a,'enemyMax'))*100,3)})
    paired={'pairs':len(changes),'beforeSamples':len(before),'afterSamples':len(after),'excludedBefore':len(before)-len(changes),'excludedAfter':len(after)-len(changes),'pairedSubset':bool(args.pair_common),'samePlayerInputs':True,'outcomeTransitions':[{'before':a,'after':b,'count':n} for (a,b),n in transitions.items()],
            'meanEndHullDelta':round(statistics.mean(r['endHullDelta'] for r in changes),3),'samples':changes}
    (args.after/'paired-comparison.json').write_text(json.dumps(paired,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in paired.items() if k!='samples'},ensure_ascii=False))
else:print(json.dumps(bs,ensure_ascii=False))
