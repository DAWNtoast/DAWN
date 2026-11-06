"""Audit current controls, intents, costs, fields and card-tooltip keywords.

Uses the frozen production source and the freshly compiled runtime catalogue.
This does not modify game code or turn a catalogue entry into a status witness.
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/battle-icon-reachability-20261026'
SNAP = OUT / 'source-snapshot/UnityClient/Assets/_Project'
RUNTIME = ROOT / 'UnityClient/Assets/_Project'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def evidence(area, file, token, note):
    source = SNAP / area / 'Runtime' / file
    lines = source.read_text(encoding='utf-8-sig').splitlines()
    line = next((i+1 for i, text in enumerate(lines) if token in text), 1)
    return {'source':str(RUNTIME / area / 'Runtime' / file),'line':line,'note':note}


def special_cost(d, stage, cost_keys):
    n, b, e = d.get('NavalExpansionSpec'), d.get('BoardingSpec'), d.get('SchoolEffect') or {}
    if n:
        label = n.get('ResourceLabel')
        if n.get('ManaCost',0)>0 or label=='法力': return 'mana'
        if n.get('HullCost',0)>0: return 'health'
        if label=='咒印': return 'curse' if n.get('ConsumesAllResource') or n.get('ResourceMaximum',0)>0 else ''
        if label=='潮势': return 'tide'
        if label=='连锁' or n.get('Key')=='L06': return 'chain'
        return ''
    if b:
        k=b['Key']
        if k not in cost_keys: return ''
        if k=='fire_14': return 'health'
        return {'arcane':'mana','curse':'curse','tide':'tide','thunder':'chain','blood':'health'}.get(k.split('_')[0],'')
    m=e.get('Mechanic')
    if m in ['ArcaneRelease','ArcaneWard','ArcaneAttackReserve','ArcaneGuardReserve','ArcaneMend','ArcaneEcho']:
        return 'mana' if e.get('Threshold',0)>0 else ''
    if m in ['BloodRisk','BloodWard']: return 'health' if e.get('Threshold',0)>0 else ''
    if m in ['CurseDrain','CurseWard']: return 'curse' if e.get('Threshold',0)>0 else ''
    if m in ['TideRiposte','TideCleanse','TideRenew']: return 'tide'
    if m=='ThunderFinish':
        naval = d['AllowedStages'] in ['Naval','Ship','All'] or 'Naval' in d['AllowedStages']
        return '' if naval and d['Id'] not in ['card_school_thunder_b06','card_school_thunder_n06','card_school_thunder_n09'] else 'chain'
    return ''


def supply_cost(d, stage):
    n, b, e = d.get('NavalExpansionSpec'), d.get('BoardingSpec'), d.get('SchoolEffect') or {}
    if n and n.get('WoodCost',0)>0: return 'wood'
    m=e.get('Mechanic')
    if m=='TideWard' and e.get('WoodCost',0)>0: return 'wood'
    if b: return 'medicine' if d['Repair']>0 else ''
    if m in ['BloodMend','BloodDrain']: return ''
    repair=d['Repair']
    if m in ['ArcaneMend','CurseDrain','TideRenew','FireHeal']: repair+=e.get('Power',0)
    return ('medicine' if stage=='Boarding' else 'wood') if repair>2 else ''


def main():
    catalog=read(OUT/'card-catalog.json')
    cost_source=(SNAP/'Presentation/Runtime/CardSpecialCostPresentation.cs').read_text(encoding='utf-8-sig')
    cost_body=cost_source.split('private static string BoardingCostText')[1].split('public static CardOrnamentKind')[0]
    cost_keys=set(re.findall(r'case "([^"]+)"',cost_body))
    examples={k:[] for k in ['energy','powder','cannonball','mana','health','curse','tide','chain','wood','medicine']}
    for c in catalog['Cards']:
        for v in c['Variants']:
            if v['Level']!=0: continue
            d=v['Definition']; keys={'energy',special_cost(d,v['Stage'],cost_keys),supply_cost(d,v['Stage'])}
            if d['PowderCost']>0: keys.add('powder')
            if d['CannonballCost']>0: keys.add('cannonball')
            for k in keys-{''}: examples[k].append({'id':c['Id'],'name':d['Name'],'stage':v['Stage']})
    rows=[]
    for file in ['controls.json','other.json','keywords.json']:
        for original in read(OUT/'previous-inventory'/file):
            e=dict(original); e['origin']=file; e['availability']='reachable'; e['reason']='当前现役界面入口及生产规则仍可到达此显示分支。'
            e['evidence']=[{'source':e['source'],'line':e['line'],'note':'当前冻结版本的显示入口。'}]
            key=e['id']
            if file=='controls.json':
                if key=='boarding_clash_mark':
                    e['availability']='legacy_unreachable'
                    e['reason']='BuildBoardingMeleeMarker、ConfigureBoardingMeleeMarker及其双剑绘制仅剩定义；全部现役Presentation源码无调用入口。'
                    e['evidence']=[evidence('Presentation','CoreGameplayNavalBattlePresentation.cs','private void BuildBoardingMeleeMarker','未被现役构建/刷新路径调用。')]
                elif key.startswith('resource_'):
                    e['reason']='资源栏每次刷新都建立此项；余额为0仍显示，不因旧卡牌效果移除而消失。'
                    e['evidence']=[evidence('Presentation','CoreGameplayCompactBattleHud.cs','private void RefreshCompactBattleResources','10项资源始终建立；七项已取消独立悬停。')]
                elif key.startswith('pile_'):
                    e['reason']='战斗刷新固定建立抽牌堆/弃牌堆，不依赖卡牌附加效果。'
                    e['evidence']=[evidence('Presentation','CoreGameplayCompactBattleHud.cs','private void RefreshCompactBattlePile','现役牌堆刷新。')]
            elif file=='keywords.json':
                uses=catalog['TriggeredKeywordUsage'].get(key,[])
                e['sources']=uses
                e['availability']='reachable' if uses else 'legacy_unreachable'
                e['reason']=(f'当前已发布卡牌正文中有实际匹配，覆盖{len(set(x["Id"] for x in uses))}张卡；这是整卡提示，非独立图标。' if uses else
                    '1062个当前已发布阶段×升级变体的可见正文均未触发此关键词；仅保留词条定义。不能据此判定同名战斗状态也不可达。')
                e['tooltip']=next(g['Meaning'] for g in catalog['Glossary'] if g['Id']==key)
                e['evidence']=[evidence('Core','CoreBattleCardKeywordText.cs','Build(','按可见效果正文匹配；不因为卡牌名称相同而触发。')]
            else:
                if key in ['enemy_heal_naval','enemy_heal_boarding_legacy','enemy_melee']:
                    e['availability']='training_only'
                    e['reason']='当前通用计划的修复/通用白刃分支只由CoreTrainingTarget进入。正常接舷战提前转入独立敌牌意图；正常海战计划不赋Repair。训练入口为Core API，不计入常规流程清单。'
                    e['evidence'].append(evidence('Core','CoreNavalBattleCombat.cs','CoreTrainingTarget target =','训练目标专用分支，普通接舷改用BuildBoardingEnemyPlan。'))
                elif key.startswith('card_cost_'):
                    cost=key[len('card_cost_'):]; e['sources']=examples[cost]
                    e['availability']='reachable' if examples[cost] else 'legacy_unreachable'
                    e['reason']=f'当前最终卡库中有{len(set(x["id"] for x in examples[cost]))}张卡进入此费用显示规则；卡框内费用图形与独立悬停无关。'
                elif key.startswith('enemy_boss_') and key not in ['enemy_boss_rush','enemy_boss_payment_execution']:
                    e['reason']='路线Boss、传奇战舰或第三宝库守卫仍使用相应四步海战脚本；不以玩家卡库是否保留旧效果为依据。'
                    e['evidence'].append(evidence('Core','CoreNavalBossBehavior.cs','private bool TryBuildNavalBossPlan','现役独立Boss脚本。'))
                elif key.startswith('boarding_'):
                    e['reason']='当前19套可选敌方接舷牌组仍有此类动作；敌方牌库独立于玩家卡库。'
                    e['evidence'].append(evidence('Core','CoreBoardingEnemyCatalog.cs','public static CoreBoardingEnemyDeck ForEncounter','普通流派牌组及Boss覆盖均已枚举。'))
                elif key.startswith('field_') and key not in ['field_direction_arrow','field_invalid_cross']:
                    e['reason']='现役场地卡及对应海战Boss仍可生成此场地；地图绘制读取实际场地实例。'
                if key=='enemy_guard':
                    e['tooltip']=e['tooltip'].replace('当前减盾效果和移动途中获得的护盾','当前减盾效果、移动途中护盾及可实际支付的血池返盾')
                e['tooltip']=e['tooltip'].replace('（回整窗口）','（回整；近身船首可能冲撞）')
            rows.append(e)
    fallback=[c for deck in catalog['EnemyBoardingDecks'] for turn in deck['Turns'] for c in turn['Cards']
              if c['AttackPercent']==0 and c['GuardPercent']==0 and c['HealPercent']==0]
    assert {c['Mechanic'] for c in fallback}=={'FireSelfBurn','FireTransfer'}
    result={'entries':rows,'costExamples':examples,'enemyFallbacks':fallback,
            'unusedEnemyFallbackKinds':['ArcaneGain','CurseMark','TideGuard','ThunderStrike','ThunderFinisher','BloodStrike','BloodHarvest'],
            'unusedFallbackReason':'当前牌组中这些机制均同时带攻击/格挡/治疗，先生成对应意图，纯资源fallback不会单独运行。'}
    (OUT/'non-status-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'entries':len(rows),'availability':{k:sum(r['availability']==k for r in rows) for k in ['reachable','training_only','legacy_unreachable']},'costTypes':{k:len(v) for k,v in examples.items()}},ensure_ascii=False))


if __name__=='__main__': main()
