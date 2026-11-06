"""Read-only audit notes for frozen naval/common status branches and final cards."""
import json
from pathlib import Path

ROOT = Path('D:/Game')
OUT = ROOT / 'outputs/battle-icon-reachability-20261026'
SRC = OUT / 'source-snapshot/UnityClient/Assets/_Project/Core/Runtime'
LIVE = ROOT / 'UnityClient/Assets/_Project/Core/Runtime'
catalog = json.loads((OUT/'card-catalog.json').read_text(encoding='utf-8-sig'))
previous = json.loads((OUT/'previous-inventory/statuses.json').read_text(encoding='utf-8-sig'))
base = previous[:41]
assert base[-1]['id'] == 'special_doctrine_blood'

def evidence(file, token, note):
    lines = (SRC/file).read_text(encoding='utf-8-sig').splitlines()
    at = next(i+1 for i,line in enumerate(lines) if token in line)
    return {'source':str(LIVE/file),'line':at,'note':note}

def cards(mechanics=(), actions=(), expansion=(), school=None, stages=('Preparation','Naval'), boarding_key=None):
    result=[]
    for c in catalog['Cards']:
        matched=[]
        for v in c['Variants']:
            if v['Level'] != 0 or v['Stage'] not in stages: continue
            d=v['Definition']; e=d.get('SchoolEffect') or {}; n=d.get('NavalExpansionSpec') or {}; b=d.get('BoardingSpec') or {}
            if school is not None and c['SchoolId'] != school: continue
            if e.get('Mechanic') in mechanics or d['Action'] in actions or n.get('Mechanic') in expansion or boarding_key and b.get('Key')==boarding_key:
                matched.append(v['Stage'])
        if matched: result.append({'id':c['Id'],'name':c['Name'],'stages':matched})
    return result

dead = {
 'natural_fire_guard':('FireNaturalGuard','CoreSchoolBattleExecution.cs','s.NaturalFireGuard =','余焰护甲旧写入仅由 FireNaturalGuard 执行；最终已发布卡及全部升级变体均不再使用该机制。'),
 'boarding_burn':('FireBoardingSeed','CoreSchoolBattleExecution.cs','s.BoardingBurn = Math.Max','接舷火种旧写入仅由 FireBoardingSeed 执行；当前最终卡库不存在该机制。'),
 'boarding_cover':('FireBoardingCover','CoreSchoolBattleExecution.cs','case CoreSchoolMechanic.FireBoardingCover:','接舷掩护旧写入仅由 FireBoardingCover 执行；当前最终卡库不存在该机制，阶段迁移只转移既有值，不创造来源。'),
 'scorch':('FireScorch','CoreSchoolBattleExecution.cs','case CoreSchoolMechanic.FireScorch:','灼烬追击旧写入仅由 FireScorch 执行；当前史诗/传说组件已改为场地卡，最终卡库不存在 FireScorch。'),
 'arcane_attack':('ArcaneAttackReserve','CoreSchoolBattleExecution.cs','s.ArcaneNextAttackDamage = SchoolSum','奥术蓄能旧写入需要 ArcaneAttackReserve；当前最终卡库没有该机制。普通法力、攻击伤害和场地增伤不产生此状态。'),
 'arcane_guard':('ArcaneGuardReserve','CoreSchoolBattleExecution.cs','s.ArcaneNextBlockBonus = SchoolSum','奥术护盾旧写入需要 ArcaneGuardReserve；当前最终卡库没有该机制。普通格挡/法力护盾的即时收益不产生这项待触发状态。'),
 'anchor':(None,'CoreNavalFields.cs','public void GrantNavalAnchor','仅保留 GrantNavalAnchor 公共方法及显示分支；冻结 Core、Content、Presentation 中没有任何调用，当前潮盾定锚实际授予定向水流免疫，不授予 AnchorCharges。'),
 'movement_slow':(None,'CoreNavalFields.cs','public void ApplyNavalMovementSlow','仅保留 ApplyNavalMovementSlow 公共方法及显示分支；冻结生产代码中没有任何调用。卸帆逆浪写入的是 NextMovementMaxCells，显示为卸帆限速，不是航速受阻。'),
}
for school,mechanic in [('fire','SpecialFireDoctrine'),('arcane','SpecialArcaneDoctrine'),('curse','SpecialCurseDoctrine'),('tide','SpecialTideDoctrine'),('thunder','SpecialThunderDoctrine'),('blood','SpecialBloodDoctrine')]:
    dead['special_doctrine_'+school]=(mechanic,'CoreSpecialCardBattleRules.cs','Battle.SpecialCards.Activate(doctrine);',
        '旧常驻特殊牌需要 '+mechanic+'；当前最终卡库及全部升级变体无此机制。史诗/传说组件先进入 CoreComponentFieldCards.Apply，旧 Doctrine 分支仍在但不再有实际卡源。')

config={
 'guard':(['Preparation','Naval','Boarding'],'双方','当前格挡 > 0','正常卡牌、敌人防御及场地可获得格挡。',[],[('CoreNavalBattleCombat.cs','Battle.PlayerBlock = (int)Math.Min','玩家卡牌格挡累加；双方显示条件由 Build 统一处理。'),('CoreNavalBattleCombat.cs','Battle.EnemyBlock = BActive ?','敌方计划的 Guard 实际写入格挡；接舷来源由接舷审计补充。')]),
 'voyage_curse':(['Preparation','Naval','Boarding'],'己方','Cargo.Curse > 0','航行事件/寻宝可获得航行诅咒，战斗中按 CurseCount 派生承伤加成。',[],[('CoreCurseRisk.cs','public int CurseDamageBonusPercent','诅咒层数派生百分比。'),('CoreVoyageEventCatalog.cs','Curse = 1','当前航行事件可取得诅咒。')]),
 'healing_ban':(['Naval'],'敌方','EnemyHealingBanTurns > 0','止愈血契可施加敌方禁疗。正常己方无来源，进入接舷会清空 NavalExpansion，因此不列为正常接舷图标。',cards(expansion=['HealingBan']),[('CoreNavalExpansionCards.cs','case CoreNavalExpansionMechanic.HealingBan:','正常来源只传入 player=false。'),('CoreNavalBattleCombat.cs','private void EnterBattleStageWithFields','进入接舷调用 NavalExpansionClear；通用 Build 中其他阶段/方向只保留造态分支。')]),
 'curse_threshold':(['Boarding'],'敌方','EnemyCurseExecutionThresholdPercent != 50','接舷缠咒牌降低敌方咒印消灭比例，且敌方尚未立即被消灭时可见；海战没有正常写入源。',cards(stages=['Boarding'],boarding_key='curse_10'),[('CoreBoardingCurseTideBlood.cs','case "curse_10":','唯一实际阈值降低来源。')]),
 'burn':(['Naval'],'双方','对应燃烧层数 > 0','玩家火焰攻击/附火、火场和火焰流派敌舰均仍能施加燃烧；接舷来源另行合并。',cards(mechanics=['FireIgnite','FireIgniteOrExtend'],expansion=['FireMixedCannon','FireField']),[('CoreFireRules.cs','private void ApplyFireBurnFromSource','燃烧统一规则；非玩家卡来源不能按玩家卡改动删去。'),('CoreNavalBossBehavior.cs','move.Burn = Math.Max(1','火焰Boss实际施加燃烧。'),('CoreNavalFieldsExecution.cs','CoreNavalFieldKind.Fire','火场对在场船只施加燃烧。')]),
 'curse_marks':(['Preparation','Naval'],'敌方；海战也可在己方显示','对应咒印层数 > 0','准备/海战诅咒牌施加敌方咒印；海战禁域和诅咒Boss也能给己方施加。',cards(mechanics=['CurseMark'],expansion=['CurseField']),[('CoreSchoolBattleExecution.cs','ApplyNavalEnemyCurseMarks(e.Power);','当前 CurseMark 仍有最终卡源。'),('CoreNavalBossBehavior.cs','move.Marks = Math.Max(1','诅咒Boss可对玩家施加。'),('CoreNavalFieldsExecution.cs','if (player) ApplyNavalPlayerCurseMarks','禁域双方来源。')]),
 'firepower':(['Preparation','Naval'],'己方','始终显示，包括值为 0','直接显示现役船只有效火力；不依赖某个卡牌效果。',[],[('CoreVoyageSession.cs','public int EffectiveFirepower','火力与水手处罚的派生值。')]),
 'armor':(['Preparation','Naval'],'己方','始终显示，包括值为 0','直接显示现役船只有效装甲；不依赖某个卡牌效果。',[],[('CoreVoyageSession.cs','public int EffectiveArmor','装甲与水手处罚的派生值。')]),
 'crew_shortage':(['Preparation','Naval'],'己方','普通水手少于船员承载的 50%','出航最低普通水手为40%，而战斗处罚阈值为50%；该区间是合法出航配置，故并未退役。',[],[('CoreDepartureCrewRules.cs','public int MinimumDepartureOrdinaryCrew','最低40%即可出航。'),('CoreVoyageSession.cs','public int MinimumOrdinaryCrew','低于50%进入水手不足。')]),
 'loaded_shot':(['Preparation','Naval'],'己方；海战敌方','己方 PlayerLoadedShotDamageBonus 或敌方 EnemyLoadedShot > 0','装填弹药、火控解算仍产生己方装填；普通敌舰准备/推进/防御行动仍装填。旧火焰溢出加伤无实际卡源，但此共享状态仍存活。',cards(actions=['Load']),[('CoreSchoolBattleExecution.cs','Battle.PlayerLoadedShot = SchoolSum','玩家正常装填。'),('CoreNavalBattleCombat.cs','Battle.EnemyLoadedShot = Math.Min','敌方行动正常装填。')]),
 'deck_buff':(['Preparation','Naval'],'己方','PlayerDeckBuff > 0','打磨甲板和六缺口旧币仍使用 Polish，提供本场海战甲板强化。',cards(actions=['Polish']),[('CoreNavalBattleCombat.cs','Battle.PlayerDeckBuff = Math.Max','Polish 正常赋值。')]),
 'fire_rounds':(['Preparation','Naval'],'己方','PlayerFireRounds > 0','引火装填、静歌蓝焰的 FireLoad 及盐硝白焰/金像静歌的 FireRounds 仍写入；可保留至接舷下一次攻击消耗，接舷记录另合并。',cards(mechanics=['FireLoad'],actions=['FireRounds']),[('CoreSchoolBattleExecution.cs','Battle.PlayerFireRounds = SchoolSum','FireLoad Secondary 正数。'),('CoreNavalBattleCombat.cs','Battle.PlayerFireRounds = SchoolSum','FireRounds StrategyAmount 正数。')]),
 'prepare_fire_guard':(['Preparation','Naval'],'己方','PrepareFireGuard > 0','防火布置仍在准备阶段写入5起的引火防护，保留到下一次成功施加燃烧。',cards(mechanics=['FirePrepareGuard']),[('CoreSchoolBattleExecution.cs','case CoreSchoolMechanic.FirePrepareGuard:','当前准备牌仍实际赋值。')]),
 'thunder_ready':(['Preparation','Naval'],'己方','ThunderReadyDamage > 0','引雷铜线、电荷整备、疾电转舵、钟摆导电仍使用正数 ThunderReady。',cards(mechanics=['ThunderReady']),[('CoreSchoolBattleExecution.cs','case CoreSchoolMechanic.ThunderReady:','四张有效牌提供下次雷霆攻击加伤。')]),
 'tide_guard':(['Preparation','Naval'],'己方；海战潮汐Boss','min(当前格挡,有效蓄潮格挡) > 0','木板付费 TideWard、潮盾定锚仍产生己方蓄潮格挡；潮汐Boss的引潮壁垒/伏浪守势仍产生敌方蓄潮格挡。',cards(mechanics=['TideWard'],expansion=['TideAnchor']),[('CoreNavalBattleCombat.cs','if (tideWoodCost > 0) Battle.Schools.TidePaidBlock','普通付费潮盾。'),('CoreNavalExpansionCards.cs','Battle.Schools.TidePaidBlock = Battle.PlayerBlock;','潮盾定锚付木板。'),('CoreNavalBossBehavior.cs','move.TidalGuard = plan.Guard;','潮汐Boss正常动作。'),('CoreNavalBossExecution.cs','Battle.EnemyTidalGuard = Math.Min','敌方蓄潮格挡实写入。')]),
 'curse_guard':(['Naval'],'己方','min(PlayerBlock, Schools.CurseGuard) > 0','幽帆牵制仍使用 CurseWard，实际获得的格挡记为临时咒术护盾；当前无准备阶段 CurseWard 牌。',cards(mechanics=['CurseWard']),[('CoreNavalBattleCombat.cs','Battle.Schools.CurseGuard = SchoolSum','CurseWard 真实格挡记入临时值。')]),
 'burn_prevention':(['Naval'],'己方','隔焰防护尚未到期','隔焰护舷与火焰组件防御牌仍授予阻止新增燃烧；当前正常敌方无授予来源。',cards(expansion=['FireWard'])+cards(mechanics=['ComponentFieldShelter'],school='fire'),[('CoreNavalExpansionCards.cs','GrantFireBurnPrevention(true, true)','隔焰护舷到下次己方回合结束。'),('CoreComponentFieldExecution.cs','if (kind == CoreNavalFieldKind.Fire)','组件火焰防御到下次己方回合开始。')]),
 'movement_cap':(['Naval'],'敌方','NextMovementMaxCells > 0','卸帆逆浪仍限制敌方下一次主动移动至多1格。',cards(expansion=['RecedingWave']),[('CoreNavalExpansionCards.cs','ApplyNavalMovementCap(false, 1);','当前扩展牌有效写入。')]),
 'precharge':(['Naval'],'己方','Precharge == true','预充线圈仍授予本回合下次雷霆攻击额外连锁。',cards(expansion=['Precharge']),[('CoreNavalExpansionCards.cs','Battle.NavalExpansion.Precharge = true;','现役扩展牌实写入。')]),
 'grounding':(['Naval'],'己方','GroundingReady && !GroundingUsed','接地回流仍可准备本回合首个雷霆伤害恢复能量。',cards(expansion=['Grounding']),[('CoreNavalExpansionCards.cs','Battle.NavalExpansion.GroundingReady = true;','现役扩展牌实写入。')]),
 'oil':(['Naval'],'敌方','OilPlayerEndsRemaining > 0','浸油标记仍施加下次燃烧翻倍状态。',cards(expansion=['OilMark']),[('CoreNavalExpansionCards.cs','case CoreNavalExpansionMechanic.OilMark:','现役扩展牌实写入。')]),
 'rune_clamp':(['Naval'],'敌方','RuneClamp == true','符文钳制仍压低敌方下次获得的格挡。',cards(expansion=['RuneClamp']),[('CoreNavalExpansionCards.cs','Battle.NavalExpansion.RuneClamp = true;','现役扩展牌实写入。')]),
 'load_seal':(['Naval'],'敌方','LoadSeal == true','蚀印封装仍降低敌方下次炮击伤害。',cards(expansion=['LoadSeal']),[('CoreNavalExpansionCards.cs','Battle.NavalExpansion.LoadSeal = true;','现役扩展牌实写入。')]),
 'pulse_reduction':(['Naval'],'敌方','PulseReduction > 0','脉冲失准仍削弱敌方攻击伤害至下个敌方回合结束。',cards(expansion=['Pulse']),[('CoreNavalExpansionCards.cs','Battle.NavalExpansion.PulseReduction = SchoolSum','现役扩展牌实写入。')]),
}
for school in ['curse','tide','thunder']:
    config['field_protection_'+school]=(['Naval'],'己方','对应场地免疫掩码/水流免疫尚未到期',
        '现役该流派组件防御牌仍授予对应场地防护。'+('潮盾定锚还提供持续至下次己方回合结束的水流免疫。' if school=='tide' else ''),
        cards(mechanics=['ComponentFieldShelter'],school=school)+(cards(expansion=['TideAnchor']) if school=='tide' else []),
        [('CoreComponentFieldExecution.cs','ship.FieldImmunityMask |= protection;','组件场地防护只写己方。')]+([('CoreNavalExpansionCards.cs','GrantNavalTideImmunityThroughNextPlayerTurnEnd(true);','潮盾定锚付木板时追加水流免疫。')] if school=='tide' else []))

entries=[]
for old in base:
    row=dict(old)
    row['hasTooltip']=True
    row['evidence']=[{'source':old['source'],'line':old['line'],'note':'冻结快照中的显示分支与原清单完全相同；悬停文字沿用已核对模板。'}]
    if old['id'] in dead:
        mechanic,file,token,reason=dead[old['id']]
        if mechanic:
            assert not catalog['SchoolMechanicUsage'].get(mechanic), mechanic
        row.update(availability='legacy_unreachable',reason=reason,stages=['Naval'] if old['id'] in ['anchor','movement_slow'] or old['id'].startswith('special_doctrine_') else ['Preparation','Naval'],side='双方旧分支' if old['id'] in ['anchor','movement_slow'] else '己方',sources=[],missingMechanic=mechanic)
        row['evidence'].append(evidence(file,token,reason))
        if mechanic:
            row['evidence'].append(evidence('CoreSpecialCardRedesign.cs','return CoreComponentFieldCards.Apply(card, key);','最终组件覆写早于旧特殊机制/Doctrine分支；324已发布卡的1062阶段升级变体已运行时枚举，目标机制使用数为0。'))
    else:
        stages,side,condition,reason,sources,ev=config[old['id']]
        row.update(availability='reachable',reason=reason,stages=stages,side=side,condition=condition,sources=sources)
        row['evidence'] += [evidence(*e) for e in ev]
    entries.append(row)
assert len(entries)==41 and sum(x['availability']=='legacy_unreachable' for x in entries)==14
result={'basis':'Frozen 482-file production snapshot, final 324 published cards / 1062 stage-level variants, source writer and caller tracing. No gameplay files changed.',
        'scope':'Build common statuses, AddNaval, AddSpecialCardDoctrines; shared boarding variants must be merged with boarding audit.',
        'counts':{'entries':41,'reachable':27,'legacy_unreachable':14,'uncertain':0},
        'notes':['No new status IDs found relative to the original list in these methods.',
                 'Reachable means a normal production source exists; visual snapshots/tests alone are not considered a source.',
                 'Public debug/API setters, arbitrary old saves and direct test field assignments are excluded from normal production reachability.'],
        'entries':entries}
(OUT/'naval-status-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result['counts']))
