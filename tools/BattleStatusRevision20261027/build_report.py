"""Refresh battle icon reports from an explicit revision of the last audit.

The user manuscript is immutable input. Run --prepare before runtime edits are
finished; run --publish after them. The old audit builder is never imported.
"""
import argparse
import base64
import copy
import hashlib
import html
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'outputs/battle-icon-reachability-20261026'
PREVIOUS_REVISION = ROOT / 'outputs/battle-status-revision-20261027'
OUT = ROOT / 'outputs/blood-field-immunity-20261027'
FIXED = ROOT / 'outputs/battle-icon-tooltips-20261026'
INPUT = PREVIOUS_REVISION / 'user-input/战斗图标与悬停提示-用户改稿.md'
CORE = ROOT / 'UnityClient/Assets/_Project/Core/Runtime'
PRESENTATION = ROOT / 'UnityClient/Assets/_Project/Presentation/Runtime'
STATUS = CORE / 'CoreBattleStatusPresentation.cs'
HUD = PRESENTATION / 'CoreGameplayCompactBattleHud.cs'
LABELS = {'reachable': '当前会出现', 'legacy_unreachable': '无正常触发来源',
          'training_only': '仅训练/测试入口', 'uncertain': '待确认'}
ASSETS = ['energy','action_point','mana','tide','thunder_chain','curse_mark','blood',
          'powder','cannonball','planks','medicine','gold','food','draw_pile',
          'discard_pile','supplies','cannon','melee','ram','guard','repair','empower',
          'curse','move','wait','fire','load','morale','armor','attack','healing',
          'weakness','arcane','thunder','anchor','slow','immunity','seal','draw',
          'settings','deck']
REMOVED = {
    'resource_chain': '连锁移入持有者状态栏；数值大于0时显示，不再占资源HUD。',
    'resource_tide': '潮势移入持有者状态栏；数值大于0时显示，不再占资源HUD。',
    'resource_curse': '资源HUD咒印入口删除，复用持有者已有的curse_marks状态图标。',
    'crew_shortage': '普通水手不足的战斗惩罚及其状态图标按本次要求移除。',
    'curse_guard': '幽帆牵制改为普通格挡；咒术护盾独立状态图标删除。',
}


def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def unique(values):
    return list(dict.fromkeys(x for x in values if x))


def string(value):
    return '、'.join(map(str, value)) if isinstance(value, list) else str(value or '')


def esc(value):
    return html.escape(string(value), quote=True)


def location(value):
    stages = {'Preparation':'准备', 'Naval':'海战', 'Boarding':'接舷'}
    sides = {'player':'己方', 'enemy':'敌方', 'both':'双方'}
    return '、'.join(stages.get(x,x) for x in value.get('stages',[])) + ' / ' + sides.get(value.get('side'), string(value.get('side')))


def manuscript_tooltips(path):
    """Read only actual quoted tooltip blocks, ignoring line-ending-only edits."""
    text = path.read_text(encoding='utf-8-sig')
    result = {}
    pattern = r'^#### (.+?) \(`([^`]+)`\)\s*$'
    matches = list(re.finditer(pattern, text, re.M))
    for index, match in enumerate(matches):
        end = matches[index+1].start() if index+1 < len(matches) else len(text)
        section = text[match.end():end]
        tooltip = section.split('**悬停文字**',1)[1].split('**证据：**',1)[0]
        lines = [line[2:] if line.startswith('> ') else '' for line in tooltip.splitlines() if line.startswith('>')]
        if lines and lines[0] == match.group(1):
            lines = lines[1:]
        category = list(re.finditer(r'^### (.+?)\s*$', text[:match.start()], re.M))[-1].group(1)
        result[(match.group(2),match.group(1),category)] = '\n'.join(lines)
    return result


def prepare():
    OUT.mkdir(parents=True, exist_ok=True)
    baseline = manuscript_tooltips(BASE/'战斗图标与悬停提示.md')
    manuscript = manuscript_tooltips(INPUT)
    changes = []
    expected = {'resource_chain','resource_tide','resource_curse','guard','voyage_curse',
                'healing_ban','curse_threshold','burn','curse_marks'}
    for key, text in manuscript.items():
        old = baseline.get(key)
        if old != text:
            changes.append({'id':key[0], 'name':key[1], 'category':key[2], 'previousTooltip':old,
                            'userTooltip':text,
                            'destination':{'resource_chain':'thunder_chain','resource_tide':'tide_power',
                                           'resource_curse':'archived_removed_hud_entry'}.get(key[0],key[0])})
    assert {c['id'] for c in changes} == expected, 'Unexpected manuscript changes; inspect instead of silently discarding them.'
    planned=copy.deepcopy(load(PREVIOUS_REVISION/'audit-summary.json')['counts'])
    for key in ['totalRecords','normalIcons','normalStatuses']: planned[key]+=1
    report = {'input':str(INPUT), 'inputSha256':digest(INPUT), 'substantiveChanges':len(changes),
              'changes':changes, 'removedUses':REMOVED,
              'plannedCounts':planned,
              'baseline':str(BASE/'audit-entries.json'), 'baselineSha256':digest(BASE/'audit-entries.json'),
              'publicationState':'prepared; runtime validation required before --publish'}
    save(OUT/'user-tooltip-overrides.json', report)
    return report


def line_with(path, text):
    for index, line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(),1):
        if text in line:
            return index
    raise RuntimeError(f'Missing finalized runtime evidence: {path.name}: {text}')


def evidence(path, text, note):
    return {'source':str(path).replace('\\','/'), 'line':line_with(path,text), 'note':note}


def replace_display_evidence(row, marker, note):
    item = evidence(STATUS,marker,note)
    row['evidence'] = [item] + [x for x in row.get('evidence',[]) if Path(x.get('source','').replace('\\','/')).name != STATUS.name]
    row['source'], row['line'] = item['source'], item['line']
    for variant in row.get('variants',[]):
        variant['tooltip'] = row['tooltip']
        variant['assetId'] = row.get('assetId')
        variant['evidence'] = [item] + [x for x in variant.get('evidence',[]) if Path(x.get('source','').replace('\\','/')).name != STATUS.name]


def check_runtime():
    status = STATUS.read_text(encoding='utf-8-sig')
    hud = HUD.read_text(encoding='utf-8-sig')
    for text in ['"thunder_chain", "连锁"', '"tide_power", "潮势"', '死神，来找你了！',
                 '下个己方回合开始时清空。', '无法恢复船体或生命值。']:
        assert text in status, f'Runtime edit not yet finalized: {text}'
    for removed in ['"crew_shortage"','"curse_guard"']:
        assert removed not in status, f'Removed status still emitted: {removed}'
    protection=status.split('foreach (CoreNavalFieldKind kind in new[]',1)[1].split('Add(result, "anchor"',1)[0]
    assert 'CoreNavalFieldKind.Blood' in protection,'Blood field protection status not yet emitted.'
    resource = hud.split('private void RefreshCompactBattleResources',1)[1].split('private void SetCompactResource',1)[0]
    assert resource.count('SetCompactResource(') == 7, 'Resource HUD must contain exactly seven entries.'
    assert not any(key in resource for key in ['BattleCommandIconKind.ThunderChain','BattleCommandIconKind.TidePower','BattleCommandIconKind.Curse'])
    pile = hud.split('private void RefreshCompactBattlePile',1)[1].split('private void ConfigureCompactBattlePileBack',1)[0]
    assert 'ConfigureCompactBattleHover(' not in pile, 'Pile hover still configured.'


def inherited_evidence(rows):
    """Keep prior evidence auditable even when current source line numbers move."""
    frozen = BASE/'source-snapshot'
    for row in rows:
        for item in [row]+row.get('variants',[]):
            for ev in item.get('evidence',[]):
                path = str(ev.get('source','')).replace('\\','/')
                if '/UnityClient/' in path:
                    relative = 'UnityClient/'+path.split('/UnityClient/',1)[1]
                    archived = frozen/relative
                    if archived.is_file():
                        ev['source'] = str(archived).replace('\\','/')
                        ev['auditRevision'] = '2026-10-26 inherited source evidence'


def revised_rows(manuscript):
    original = load(BASE/'audit-entries.json')
    rows = copy.deepcopy(original)
    inherited_evidence(rows)
    removed = [e for e in rows if e['id'] in REMOVED and e['origin'] != 'keywords.json']
    rows = [e for e in rows if e not in removed]
    assert len(removed)==5
    changes = {c['id']:c for c in manuscript['changes']}
    for row in rows:
        if row['id'] in changes and row['category']==changes[row['id']]['category']:
            row['tooltip'] = changes[row['id']]['userTooltip']
            marker = {'burn':'private static void AddBurn','curse_marks':'private static void AddCurse',
                      'curse_threshold':'"curse_threshold", "缠咒"'}.get(row['id'],f'"{row["id"]}", "{row["name"]}"')
            replace_display_evidence(row,marker,'本次运行时提示同步用户改稿；该状态的既有触发来源不变。')
        if row['id'] == 'curse_marks' and row['origin']=='statuses.json':
            row['assetId'] = 'curse_mark'
            for variant in row.get('variants',[]): variant['assetId']='curse_mark'
            row['reason'] += '；资源HUD的重复入口已移除，本状态使用原资源咒印图案并显示在实际持有者一侧。'
        if row['category']=='资源 HUD':
            row['reason']='资源栏保留能量、行动点、法力、火药、炮弹、木板、药品七项；余额为0仍显示，均无悬停提示。'
            row['evidence']=[evidence(HUD,'private void RefreshCompactBattleResources','本次资源HUD固定保留七项，连锁/潮势/咒印移入持有者状态栏。')]
        if row['id'] in ['pile_draw','pile_discard']:
            row['hasTooltip']=False
            row['tooltip']='无悬停提示。'
            row['reason']='牌堆图标保留数量与点击查看入口；鼠标悬停不显示提示。'
            row['evidence']=[evidence(HUD,'private void RefreshCompactBattlePile','抽牌堆和弃牌堆保留按钮行为，移除悬停绑定。')]
    for id, old_id, name, asset, condition in [
        ('thunder_chain','resource_chain','连锁','thunder_chain',
         '准备/海战/接舷：持有者连锁 > 0；己方取 Schools.ThunderChain，敌方海战取 NavalFields.Enemy.ThunderChain，敌方接舷取 BoardingEnemy.ThunderChain。'),
        ('tide_power','resource_tide','潮势','tide',
         '准备/海战/接舷：持有者潮势 > 0；己方准备/海战取 Schools.TidePower，己方接舷取 Boarding.TidePower；敌方海战取 EnemyTidePower，敌方接舷取 BoardingEnemy.TidePower。')]:
        old=next(e for e in original if e['id']==old_id)
        ev=evidence(STATUS,f'"{id}", "{name}"','本次新增持有者状态栏显示；只在对应数值大于0时建立该图标。')
        row={'origin':'statuses.json','id':id,'name':name,'category':'通用状态','availability':'reachable',
             'assetId':asset,'hasTooltip':True,'tooltip':changes[old_id]['userTooltip'],
             'condition':condition,'reason':'从资源HUD移到实际持有者的状态栏，双方分别展示自己的数值；为0时不显示。',
             'stages':['Preparation','Naval','Boarding'],'side':'both',
             'sources':[], 'evidence':[ev], 'source':ev['source'],'line':ev['line']}
        if id=='thunder_chain':
            row['sources']=['现役雷霆攻击牌','海战雷霆Boss行动','独立敌方接舷雷霆牌']
            row['evidence'] += [
                evidence(CORE/'CoreThunderRules.cs','else if (attack) GainNavalThunderChain(true, 1);','玩家正式雷霆攻击牌累加连锁。'),
                evidence(CORE/'CoreNavalBossExecution.cs','GainNavalThunderChain(false, move.ChainGain)','海战Boss正式行动给敌方增加连锁。'),
                evidence(CORE/'CoreBoardingEnemySimulation.cs','c.State.ThunderChain = SchoolSum(c.State.ThunderChain, 1);','接舷敌方雷霆牌累加敌方连锁。')]
        else:
            row['sources']=['双方蓄潮格挡实际吸收伤害','现役潮汐卡牌与海战潮汐Boss','独立敌方接舷潮汐牌']
            row['evidence'] += [
                evidence(CORE/'CoreSchoolResourceRules.cs','if (gainTide) s.TidePower = SchoolSum(s.TidePower, paidAbsorbed);','海战玩家蓄潮格挡吸收伤害后获得潮势。'),
                evidence(CORE/'CoreNavalBossExecution.cs','Battle.EnemyTidePower = SchoolSum(Battle.EnemyTidePower, paid);','海战敌方蓄潮格挡吸收伤害后获得潮势。'),
                evidence(CORE/'CoreBoardingEnemySimulation.cs','state.TidePower = SchoolSum(state.TidePower, tideAbsorbed);','接舷敌方蓄潮格挡吸收伤害后获得潮势。'),
                evidence(CORE/'CoreBoardingCurseTideBlood.cs','BState.TideOverflow = SchoolSum(BState.TideOverflow, gained);','接舷玩家潮势加入当前持有值。')]
        row['variants']=[{key:copy.deepcopy(row[key]) for key in ['id','name','availability','assetId','tooltip','condition','reason','stages','side','evidence']}]
        # Place new shared states next to the existing curse-mark state.
        index=next(i for i,e in enumerate(rows) if e['id']=='curse_marks' and e['origin']=='statuses.json')
        rows.insert(index,row)
    # The final immunity implementation supplies these reviewed dynamic templates.
    field_path=OUT/'field-tooltip-overrides.json'
    if not field_path.is_file():
        raise RuntimeError('Await finalized field-tooltip-overrides.json before publishing.')
    field_updates=load(field_path)
    for update in field_updates['entries']:
        if update.get('newStatus'):
            row={'origin':'statuses.json','id':update['id'],'name':update['name'],
                 'category':'海战场地防护与移动状态','availability':'reachable','assetId':'immunity',
                 'hasTooltip':True,'tooltip':update['tooltip'],'condition':update['condition'],
                 'reason':update['reason'],'stages':['Naval'],'side':'player','value':'1',
                 'sources':copy.deepcopy(update['sources']),'evidence':[]}
            row['variants']=[{key:copy.deepcopy(row[key]) for key in ['id','name','availability','assetId','tooltip','condition','reason','stages','side','value','sources','evidence']}]
            index=next(i for i,e in enumerate(rows) if e['id']=='field_protection_thunder')+1
            rows.insert(index,row)
        row=next(e for e in rows if e['id']==update['id'] and e['origin']=='statuses.json')
        row['tooltip']=update['tooltip']
        if update.get('condition'): row['condition']=update['condition']
        if update.get('reason'): row['reason']=update['reason']
        replace_display_evidence(row,update['sourceMarker'],'本次场地免疫提示与实际到期规则一致。')
        rules=[evidence(CORE/'CoreComponentFieldExecution.cs','ship.FieldImmunityExpires = FieldOwnerTurn(true) + 1;','组件防护牌把场地免疫统一写为下个我方回合开始到期。'),
               evidence(CORE/'CoreNavalFieldsExecution.cs','if (ship.FieldImmunityExpires <= FieldOwnerTurn(player))','回合开始移除已经到期的场地免疫。')]
        if row['id']=='burn_prevention':
            rules=[evidence(CORE/'CoreNavalExpansionCards.cs','AddSchoolGuard(SchoolSum(card.Block, Battle.PlayerDeckBuff)); GrantFireBurnPrevention(true, false);','隔焰护舷使用下次开始到期的火场防护。'),
                   evidence(CORE/'CoreComponentFieldExecution.cs','GrantFireBurnPrevention(true, false);','火焰组件防护牌使用同一期限。'),
                   evidence(CORE/'CoreNavalFieldsExecution.cs','(!ship.BurnPreventionExpiresAtTurnEnd && ship.BurnPreventionExpiresAtOwnerTurn <= FieldOwnerTurn(player)))','回合开始移除到期火场防护。')]
        elif row['id']=='field_protection_tide':
            rules += [evidence(CORE/'CoreNavalExpansionCards.cs','GrantNavalTideImmunity(true);','潮盾定锚现使用下次开始到期的水流免疫。'),
                      evidence(CORE/'CoreNavalFields.cs','ship.FieldImmunityExpires = SchoolSum(FieldOwnerTurn(player), 1);','水流免疫写入下个本方回合开始的期限。')]
        elif row['id']=='field_protection_blood':
            rules += [evidence(CORE/'CoreComponentFieldCards.cs','else if (kind == CoreNavalFieldKind.Blood)','血契部件防御卡定义增加海战血池负面免疫，仍保留位于己方血池时的额外收益。'),
                      evidence(CORE/'CoreNavalFields.cs','(1 << (int)CoreNavalFieldKind.Thunder) | (1 << (int)CoreNavalFieldKind.Blood);','有效场地防护掩码包含血池。'),
                      evidence(CORE/'CoreNavalFieldsExecution.cs','!NavalFieldImmune(ship, CoreNavalFieldKind.Blood, FieldOwnerTurn(player))','血池回合末负面结算检查当前持有者的血池免疫。'),
                      {'source':str(OUT/'card-list-export/card-list-data.json').replace('\\','/'),'line':1,
                       'note':'本轮最终卡库导出确认雾铁偿伤、活舟还生均在海战卡面授予血池负面免疫。'}]
        row['evidence']=row['evidence'][:1]+rules
        for variant in row.get('variants',[]):
            variant['condition']=row['condition']
            variant['reason']=row['reason']
            variant['evidence']=copy.deepcopy(row['evidence'])
    assert len(rows)==manuscript['plannedCounts']['totalRecords']
    assert len({(e['origin'],e['id'],e['name']) for e in rows})==len(rows)
    return rows,removed


def counts_for(rows):
    count=lambda f:sum(f(e) for e in rows)
    return {'totalRecords':len(rows),'normalIcons':count(lambda e:e['origin']!='keywords.json' and e['availability']=='reachable'),
            'normalStatuses':count(lambda e:e['origin']=='statuses.json' and e['availability']=='reachable'),
            'inactiveStatuses':count(lambda e:e['origin']=='statuses.json' and e['availability']=='legacy_unreachable'),
            'normalKeywords':count(lambda e:e['origin']=='keywords.json' and e['availability']=='reachable'),
            'inactiveKeywords':count(lambda e:e['origin']=='keywords.json' and e['availability']=='legacy_unreachable'),
            'inactiveIcons':count(lambda e:e['origin']!='keywords.json' and e['availability']=='legacy_unreachable'),
            'trainingIcons':count(lambda e:e['availability']=='training_only' and e['origin']!='keywords.json'),
            'uncertain':count(lambda e:e['availability']=='uncertain')}


def tooltip(row):
    return row['tooltip'] if not row['hasTooltip'] or row['origin'] in ['other.json','keywords.json'] else row['name']+'\n'+row['tooltip']


def source_text(row):
    return '、'.join(unique(s if isinstance(s,str) else str(s.get('name') or s.get('Name') or s.get('id') or s.get('Id') or '') for s in row.get('sources',[])))


def render(rows, summary):
    counts=summary['counts']
    intro=f'当前正常流程可出现 {counts["normalIcons"]} 条独立图标用途，其中状态 {counts["normalStatuses"]} 条；另有 {counts["normalKeywords"]} 条整卡提示关键词。'
    notice='新增血池防护：雾铁偿伤、活舟还生在海战中可授予，持续到下个我方回合开始。连锁、潮势、咒印在实际持有者的状态栏显示；资源栏保留七项。抽牌堆、弃牌堆无悬停提示。水手不足及咒术护盾图标已删除。'
    method='基于2026-10-26已核对的324张已发布卡牌、1062个阶段×升级变体、19套独立敌方接舷牌组做明确增量更新；本次变更以当前运行时代码为准。'
    limit='未在Unity中逐条触发全部图标。未改动项目美术素材；旧14项不可达状态及训练意图继续保留在筛选项。'
    categories=unique(e['category'] for e in rows)
    atlas=base64.b64encode((OUT/'battle_icons_v1.png').read_bytes()).decode('ascii')
    body=[]
    for category in categories:
        trs=[]
        for e in [r for r in rows if r['category']==category]:
            n=ASSETS.index(e['assetId']) if e.get('assetId') in ASSETS else -1
            icon=f'<span class="sprite" style="background-position:{-(n%8)*64}px {-(n//8)*64}px"></span>' if n>=0 else '<span class="texticon">'+('整卡词条' if e['origin']=='keywords.json' else '见说明')+'</span>'
            scopes=[LABELS[v['availability']]+' · '+location(v)+'：'+string(v.get('reason')) for v in e.get('variants',[])]
            evs=[str(x.get('source',''))+(':'+str(x['line']) if x.get('line') else '')+' '+str(x.get('note','')) for x in e.get('evidence',[])]
            detail='\n'.join(unique(scopes+evs)); sources=source_text(e)
            trs.append(f'<tr data-id="{esc(e["id"])}" data-category="{esc(category)}" data-availability="{e["availability"]}" data-kind="{"keyword" if e["origin"]=="keywords.json" else "icon"}" data-tooltip="{"yes" if e["hasTooltip"] else "no"}"><td><div class="identity">{icon}<div><b>{esc(e["name"])}</b><span class="badge {e["availability"]}">{LABELS[e["availability"]]}</span><small>{esc(e["id"])}</small></div></div></td><td><p>{esc(e["condition"])}</p><p>{esc(e["reason"])}</p>'+(f'<p class="muted">有效来源：{esc(sources)}</p>' if sources else '')+f'<details><summary>分阶段与源码证据</summary><pre>{esc(detail)}</pre></details></td><td><pre>{esc(tooltip(e))}</pre></td></tr>')
        body.append(f'<section class="group"><h2>{esc(category)}</h2><div class="tablewrap"><table><thead><tr><th>图标用途</th><th>当前条件与结论</th><th>现有悬停文字</th></tr></thead><tbody>'+''.join(trs)+'</tbody></table></div></section>')
    template=(Path(__file__).parent/'report-template.html').read_text(encoding='utf-8')
    document=template.replace('@@ATLAS@@',atlas).replace('@@INTRO@@',esc(intro)).replace('@@NOTICE@@',esc(notice)).replace('@@METHOD@@',esc(method+' '+limit)).replace('@@CATEGORIES@@',''.join(f'<option>{esc(c)}</option>' for c in categories)).replace('@@BODY@@',''.join(body))
    md=['# 当前战斗图标与悬停提示','',intro,'',notice,'',method,'',limit,'','用户亲改提示已同步；原稿单独备份，未被导出结果覆盖。','','## 本次移除或迁移的图标用途','']
    md += ['- `'+key+'`：'+value for key,value in REMOVED.items()]
    md += ['',f'## 不再正常出现的 {counts["inactiveStatuses"]} 项旧状态','']
    md += ['- **'+e['name']+'** (`'+e['id']+'`)：'+e['reason'] for e in rows if e['origin']=='statuses.json' and e['availability']=='legacy_unreachable']
    for avail in LABELS:
        chosen=[e for e in rows if e['availability']==avail]
        if not chosen: continue
        md+=['','## '+LABELS[avail],'']
        for category in unique(e['category'] for e in chosen):
            md+=['### '+category,'']
            for e in [x for x in chosen if x['category']==category]:
                md+=['#### '+e['name']+' (`'+e['id']+'`)','','- 条件：'+e['condition'].replace('\n','；'),'- 结论：'+e['reason']]
                if source_text(e): md+=['- 有效来源：'+source_text(e)]
                for v in e.get('variants',[]): md+=['- '+LABELS[v['availability']]+' · '+location(v)+'：'+string(v.get('reason'))]
                md+=['','**悬停文字**（'+('整卡词条，非独立图标' if e['origin']=='keywords.json' else '有提示' if e['hasTooltip'] else '无独立提示')+'）：','']
                md+=['> '+line for line in tooltip(e).split('\n')]
                md+=['','**证据：**','']
                for x in e.get('evidence',[]):
                    path=string(x.get('source')).replace('\\','/')
                    if path: md+=['- ['+Path(path).name+(':'+str(x['line']) if x.get('line') else '')+'](</'+path+(':'+str(x['line']) if x.get('line') else '')+'>) '+string(x.get('note'))]
                md+=['']
    return document,'\n'.join(md)


def publish(manuscript):
    check_runtime()
    rows,removed=revised_rows(manuscript)
    cards=load(OUT/'card-list-export/card-list-data.json')
    def card_records(value):
        if isinstance(value,dict):
            if value.get('Id') in {'card_special_plating_fogiron_carapace','card_special_plating_living_hull'} and 'StageVariants' in value:
                yield value
            for child in value.values(): yield from card_records(child)
        elif isinstance(value,list):
            for child in value: yield from card_records(child)
    blood_cards=list(card_records(cards))
    assert len(blood_cards)==2,'Both final published blood shelter cards must be present.'
    for card in blood_cards:
        assert any('海战' in v['Stages'] and '免疫血池负面效果影响' in v['Effect'] for v in card['StageVariants']),card['Id']
    counts=counts_for(rows)
    for key,value in manuscript['plannedCounts'].items(): assert counts[key]==value,(key,counts[key])
    summary={'counts':counts,'catalogue':load(BASE/'audit-summary.json')['catalogue'],
             'basis':'2026-10-26冻结完整审计的明确增量修订；本次变更由最终运行时代码及用户改稿复核。',
             'runtimeCodeModified':True,'userInputSha256':digest(INPUT),'userSubstantiveChangesPreserved':9,
             'baselineSha256':manuscript['baselineSha256'],'removedUses':REMOVED,
             'thisRevisionAddedIds':['field_protection_blood'],'previousRevision':str(PREVIOUS_REVISION),
             'limitation':'未在Unity中逐条触发全部图标；未重跑不受本次改动影响的完整卡库审计。'}
    atlas=ROOT/'UnityClient/Assets/_Project/Resources/BattleIcons/battle_icons_v1.png'
    shutil.copyfile(atlas,OUT/'battle_icons_v1.png')
    document,markdown=render(rows,summary)
    for folder in [OUT,FIXED]:
        (folder/'战斗图标与悬停提示.html').write_text(document,encoding='utf-8')
        (folder/'战斗图标与悬停提示.md').write_text(markdown,encoding='utf-8')
    save(OUT/'audit-entries.json',rows); save(OUT/'audit-summary.json',summary)
    save(OUT/'revision-delta.json',{'removedRecords':removed,'removedReasons':REMOVED,
         'addedIds':['thunder_chain','tide_power','field_protection_blood'],
         'thisRevisionAddedIds':['field_protection_blood'],'userTooltipOverrides':manuscript['changes'],
         'additionalChanges':['pile_draw/pile_discard hover removed','curse_marks uses curse_mark art','field immunity expiry templates synchronized']})
    save(FIXED/'all-icons.json',rows); save(FIXED/'reachability-summary.json',summary)
    current_files={Path(ev['source']) for row in rows for ev in row.get('evidence',[]) if str(ev.get('source','')).startswith(str(ROOT/'UnityClient').replace('\\','/'))}
    save(OUT/'source-manifest.json',{'files':[{'source':str(p),'sha256':digest(p)} for p in sorted(current_files|{INPUT,BASE/'audit-entries.json',STATUS,HUD,atlas})],
                                   'sourceEvidencePolicy':'Unchanged rules retain frozen 2026-10-26 evidence; changed display rules link to current source.'})
    assert digest(INPUT)==manuscript['inputSha256'],'User manuscript changed during export.'
    save(FIXED/'status-revision.json',{'revision':'2026-10-27-blood-field-immunity','builder':'tools/BattleStatusRevision20261027/build_report.py',
                                     'reportDirectory':'outputs/blood-field-immunity-20261027','counts':counts})
    print(json.dumps(summary,ensure_ascii=False,indent=2))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--prepare',action='store_true')
    parser.add_argument('--publish',action='store_true')
    args=parser.parse_args()
    if args.prepare==args.publish: parser.error('Choose --prepare or --publish.')
    manuscript=prepare()
    if args.publish: publish(manuscript)
    else: print(json.dumps({'inputChanges':manuscript['substantiveChanges'],'plannedCounts':manuscript['plannedCounts']},ensure_ascii=False))
