"""Emit the reviewed boarding-state reachability audit from frozen evidence.

This reports production sources only. It does not mutate game state or run Unity.
"""
import json
from pathlib import Path

ROOT = Path('D:/Game')
OUT = ROOT / 'outputs/battle-icon-reachability-20261026'
CORE = ROOT / 'UnityClient/Assets/_Project/Core/Runtime'
OLD = json.loads((OUT/'previous-inventory/statuses.json').read_text(encoding='utf-8-sig'))
CAT = json.loads((OUT/'card-catalog.json').read_text(encoding='utf-8-sig'))
entries = []


def ev(file, line, note):
    return {'source':str(CORE/file).replace('\\','/'),'line':line,'note':note}


def cards(keys):
    found = []
    for card in CAT['Cards']:
        for v in card['Variants']:
            spec = v['Definition'].get('BoardingSpec')
            if v['Stage'] == 'Boarding' and v['Level'] == 0 and spec and spec['Key'] in keys:
                found.append({'type':'published-player-card','id':card['Id'],
                              'name':v['Definition']['Name'],'boardingKey':spec['Key']})
    assert found, keys
    return found


def enemy_cards(mechanic=None, school=None, burns=False):
    found=[]
    for deck in CAT['EnemyBoardingDecks']:
        # Actual encounter decks demonstrate reachability independently of the
        # generic school templates, which can be overridden by a boss script.
        if deck['Kind'] != 'Encounter' or (school and deck['SchoolId'] != school):
            continue
        for turn in deck['Turns']:
            for card in turn['Cards']:
                if (mechanic and card['Mechanic'] != mechanic) or (burns and card['BurnPercent'] <= 0):
                    continue
                found.append({'type':'actual-enemy-encounter-card','encounter':deck['Id'],
                              'id':card['Id'],'name':card['Name'],'mechanic':card['Mechanic']})
    assert found
    return found


def row(id, name, side, line, condition, keys, writes, reason=None, sources=None):
    old = next(x for x in OLD if x['id']==id and x['name']==name)
    source_cards = cards(keys) if keys else (sources or [])
    item = {k:old[k] for k in ('id','name','category','tooltip','assetId','hasTooltip')}
    item.update({'availability':'reachable','stages':['Boarding'],'side':side,
                 'condition':condition,'sources':source_cards,
                 'reason':reason or '当前已发布接舷卡仍使用该效果键；正式出牌执行分支可写入并满足显示条件，并非仅测试赋值。',
                 'evidence':[ev('CoreBattleStatusPresentation.cs',line,'当前接舷显示条件及提示。')]+
                            [ev(*w) for w in writes]})
    if source_cards:
        item['evidence'].append({'source':str(OUT/'card-catalog.json').replace('\\','/'),
                                'line':1,'note':'冻结源码实际运行导出；sources 中的正式卡或敌方遭遇牌组均存在。'})
    entries.append(item)


# Player-side boarding states.
row('burn','燃烧','player',255,'Boarding.SelfBurn 中 Amount>0 且 Remaining>0 的层数之和 > 0',
    ['fire_11','fire_13','fire_14'], [('CoreBoardingFireArcaneThunder.cs',92,'浴火斩等现役接舷牌主动给自身燃烧。'),
    ('CoreBoardingBattleExecution.cs',298,'BApplyPlayerBurn 将燃烧加入 SelfBurn。'),
    ('CoreBoardingEnemySimulation.cs',308,'现役火焰敌人也可向玩家施加燃烧。'),
    ('CoreBoardingFieldEffects.cs',24,'继承火场在回合末施加燃烧。')])
fire_sources=[]
for c in CAT['Cards']:
    if c['Id'] in ['card_school_fire_b01','card_special_crew_twilight_tide_gunner_yilan',
                   'card_special_crew_salt_crown_gunner_rokan','card_special_bow_gilded_mermaid']:
        fire_sources.append({'type':'published-naval-carryover-card','id':c['Id'],'name':c['Name']})
row('fire_rounds','附火','player',256,'Battle.PlayerFireRounds > 0（海战获得后尚未攻击，可带入接舷）',[],
    [('CoreSchoolBattleExecution.cs',209,'FireLoad/相关学校分支增加 PlayerFireRounds。'),
     ('CoreNavalBattleCombat.cs',289,'FireRounds 动作增加附火层数。'),
     ('CoreFireRules.cs',59,'仅实际触发附火后清零，阶段转换不清除此 Battle 字段。'),
     ('CoreBoardingBattleExecution.cs',148,'接舷主动攻击继续触发附火。')],
    '现役准备/海战附火卡仍存在；通过主动接舷且在此前不攻击，可以把未消耗附火带入接舷。', fire_sources)
row('curse_marks','咒印','player',258,'BoardingEnemy.CurseMarks > 0',[],
    [('CoreBoardingEnemySimulation.cs',155,'CurseMark 敌方行动给玩家增加咒印。'),
     ('CoreBoardingBossCatalog.cs',70,'月潮航线Boss的蚀魂印刃为正式 CurseMark 来源。'),
     ('CoreBoardingFieldEffects.cs',28,'继承禁域也会给玩家增加咒印。')],
    '正式咒术Boss的牌组及继承禁域仍会施加；不依赖玩家卡牌的旧咒术机制。',enemy_cards('CurseMark'))
simple = [
 ('fire_pact','共燃契约',259,'Boarding.FirePactTurns > 0',['fire_12'],'CoreBoardingFireArcaneThunder.cs',97),
 ('fire_retaliation','焰盾反击',261,'Boarding.FireRetaliations > 0',['fire_13'],'CoreBoardingFireArcaneThunder.cs',99),
 ('fire_pots','燃烧火壶',393,'Boarding.FirePots 有 Amount>0 且 Remaining>0 的项',['fire_04'],'CoreBoardingFireArcaneThunder.cs',77),
 ('free_powder','免耗火药',264,'Boarding.FreePowderUses > 0',['fire_10'],'CoreBoardingFireArcaneThunder.cs',90),
 ('retained_guard','奥术保留格挡',361,'min(PlayerBlock, Boarding.ArcaneRetainedGuard) > 0',['arcane_08'],'CoreBoardingFireArcaneThunder.cs',126),
 ('tide_guard','蓄潮格挡',267,'min(PlayerBlock, Boarding.TideGuard) > 0',['tide_01','tide_02','tide_03','tide_05'],'CoreBoardingCurseTideBlood.cs',169),
 ('tide_guard_conversion','留潮',269,'Boarding.TideGuardConversionActive == true',['tide_05'],'CoreBoardingCurseTideBlood.cs',176),
 ('arcane_echo','回响刻印',271,'Boarding.ArcaneEchoReady == true',['arcane_13'],'CoreBoardingFireArcaneThunder.cs',138),
 ('thunder_ready','电弧导流',273,'Boarding.ThunderReady > 0',['thunder_06'],'CoreBoardingFireArcaneThunder.cs',151),
 ('thunder_residual','雷网残响',275,'Boarding.ThunderResidualCharges > 0',['thunder_10'],'CoreBoardingFireArcaneThunder.cs',157),
 ('curse_contract','恶兆契约',277,'Boarding.CurseContractTurns > 0',['curse_04'],'CoreBoardingCurseTideBlood.cs',137),
 ('curse_link','连环咒印',279,'Boarding.CurseLinkCharges > 0',['curse_05'],'CoreBoardingCurseTideBlood.cs',138),
 ('curse_counter','逆刃咒',281,'Boarding.CurseCounterTurns > 0',['curse_09'],'CoreBoardingCurseTideBlood.cs',149),
 ('tide_ambush','潜流伏击',283,'Boarding.TideAmbushTurns > 0',['tide_09'],'CoreBoardingCurseTideBlood.cs',188),
 ('blood_debt','血债追偿',285,'Boarding.BloodDebtTurns > 0',['blood_08'],'CoreBoardingCurseTideBlood.cs',225),
 ('blood_oath','绝境誓约',287,'Boarding.BloodOathPlayerEnds > 0',['blood_15'],'CoreBoardingCurseTideBlood.cs',239),
 ('healing','持续恢复',370,'Boarding.HealingTicks 中 Amount>0 且 Remaining>0 的总值 > 0',['basic_07','tide_12'],'CoreBoardingBattleExecution.cs',275),
 ('retained_cards','封存手牌',292,'HeldCards 中 Retain && !RetainUsed 的卡数 > 0',['arcane_12'],'CoreBoardingCardChoices.cs',218),
]
for id,name,line,cond,keys,file,write in simple:
    row(id,name,'player',line,cond,keys,[(file,write,'当前效果键的正式执行写入。')])

# Enemy-side status states; distinguish player-inflicted debuffs from enemy cards.
row('burn','燃烧','enemy',298,'Boarding.EnemyBurn 有有效层数，或接舷初始化前的 EnemyBurnDamage/Turns > 0',
    ['fire_02','fire_03','fire_04','fire_06','fire_07','fire_15'],
    [('CoreBoardingFireArcaneThunder.cs',73,'现役火焰接舷牌施加敌方燃烧。'),
     ('CoreBoardingBattleExecution.cs',31,'接舷初始化绑定已有敌方燃烧池。'),
     ('CoreBoardingFieldEffects.cs',26,'继承火场也可施加敌方燃烧。')])
row('curse_marks','咒印','enemy',301,'Boarding.CurseMarks > 0',
    ['curse_01','curse_02','curse_03','curse_04','curse_05'],
    [('CoreBoardingCurseTideBlood.cs',132,'当前咒术接舷牌给敌方施加咒印。'),
     ('CoreBoardingFieldEffects.cs',28,'继承禁域也会给敌方施加咒印。')])
row('retained_guard','留潮盾','enemy',302,'min(EnemyBlock, BoardingEnemy.RetainedGuard) > 0',[],
    [('CoreBoardingEnemySimulation.cs',194,'正式敌方 TideGuard 增加并保留蓄潮格挡。'),
     ('CoreBoardingBossCatalog.cs',41,'礁壁伏阵等航线Boss卡使用 TideGuard。'),
     ('CoreBoardingBossCatalog.cs',136,'镇魂留潮等传奇敌舰卡使用 TideGuard。'),
     ('CoreBoardingEnemyCatalog.cs',174,'真实遭遇优先选择Boss牌组。')],
    '敌方独立Boss牌组仍使用 TideGuard；玩家留潮卡规则变化不能用来删除此敌方图标。',enemy_cards('TideGuard'))
for id,name,line,cond,keys,file,write in [
 ('thunder_weakness','雷霆弱化',305,'ThunderWeaknessTurns > 0 且 ThunderWeakness > 0',['thunder_12'],'CoreBoardingFireArcaneThunder.cs',167),
 ('curse_weakness','衰弱咒',308,'CurseWeakTurns > 0 且当前弱化值 > 0',['curse_07'],'CoreBoardingCurseTideBlood.cs',142),
 ('tide_weakness','潮汐弱化',310,'TideWeakTurns > 0 且当前弱化值 > 0',['tide_10'],'CoreBoardingCurseTideBlood.cs',192),
 ('curse_armor','蚀甲咒',311,'Boarding.CurseArmorTurns > 0',['curse_08'],'CoreBoardingCurseTideBlood.cs',145),
 ('delayed_damage','延迟攻击',385,'Boarding.DelayedDamage 中 Amount>0 且 Remaining>0 的总量 > 0',['basic_13','arcane_11','arcane_13','thunder_05','blood_13'],'CoreBoardingBattleExecution.cs',256),
]:
    row(id,name,'enemy',line,cond,keys,[(file,write,'当前正式玩家牌写入敌方对应状态。')])
row('desperate','绝境','enemy',315,'敌方牌组 SchoolId==blood，且敌方接舷生命不高于最大值40%',[],
    [('CoreBoardingBossCatalog.cs',50,'黄金潮航线Boss使用鲜血牌组。'),
     ('CoreBoardingBossCatalog.cs',115,'红寡妇传奇敌舰使用鲜血牌组。'),
     ('CoreBoardingEnemySimulation.cs',177,'敌方鲜血收割按当前生命检查绝境加成。')],
    '现役鲜血Boss存活且降至40%生命即可显示；玩家绝境规则仍在，但此状态图标只绑定敌方。',enemy_cards(school='blood'))

field_ids={'fire':('火场','FireField','card_school_fire_n01'),
           'arcane':('折射阵','ArcaneField','card_school_arcane_n01'),
           'curse':('禁域','CurseField','card_school_curse_n01'),
           'thunder':('雷栅','ThunderField','card_school_thunder_n01'),
           'blood':('血池','BloodField','card_school_blood_n01')}
for suffix,(name,mechanic,cardid) in field_ids.items():
    card=next(c for c in CAT['Cards'] if c['Id']==cardid)
    variant=next(v for v in card['Variants'] if v['Stage']=='Naval' and v['Level']==0)
    assert variant['Definition']['NavalExpansionSpec']['Mechanic']==mechanic
    row('boarding_field_'+suffix,name,'both',346,'BoardingFields 中对应 Kind 的 RemainingTurns > 0',[],
        [('CoreNavalExpansionCardChoices.cs',183,'现役海战场地牌映射到对应场地类型。'),
         ('CoreBoardingFields.cs',56,'主动接舷/冲撞捕获船只所在格的剩余场地。'),
         ('CoreBoardingFields.cs',65,'过滤水流，其他五类按剩余持续时间合并并保留归属。'),
         ('CoreNavalBattleCombat.cs',996,'进入接舷阶段接收已捕获场地。')],
        '对应海战场地牌仍在正式卡库；在该场地格主动接舷或冲撞进入接舷即可继承，作用于双方。',
        [{'type':'published-naval-field-card','id':cardid,'name':variant['Definition']['Name'],'mechanic':mechanic}])

assert len(entries)==35, len(entries)
assert all(x['sources'] for x in entries)
report={
 'scope':'CoreBattleStatusPresentation.AddBoarding and its status helpers; frozen current production code and card export.',
 'method':'Read-only data-flow and final runtime card/deck definition audit; no synthetic state injection and no Unity launch.',
 'entries':entries,
 'summary':{'variants':len(entries),'reachable':len(entries),'legacy_unreachable':0,'uncertain':0,
            'playerVariants':sum(x['side']=='player' for x in entries),
            'enemyVariants':sum(x['side']=='enemy' for x in entries),
            'bothSideFieldVariants':sum(x['side']=='both' for x in entries)},
 'scopeNotes':[
     '本文件不重复通用Build中的guard、voyage_curse、healing_ban、curse_threshold；由海战/通用审计汇总。',
     '通用healing_ban分支虽在接舷Build存在，进入接舷会NavalExpansionClear，当前正常写入只来自海战止愈血契，因此不应把禁疗列作正常接舷状态。',
     '通用curse_threshold由现役接舷curse_10缠咒改写，仍正常可达。',
     '玩家绝境影响卡牌条件和数值，但AddBoarding只给血系敌方产生desperate独立状态图标。',
     '玩家护盾由tide_guard呈现，敌方蓄潮保留盾以retained_guard/留潮盾呈现，不能混淆同名或共用图形。',
     '现役接舷卡fire/arcane/curse/tide/thunder/blood的01–15键仍在正式卡库；本范围未发现因当前改卡而失去正常来源的状态图标。',
     '所有当前AddBoarding状态ID都已在旧70条状态清单中覆盖，未发现新的遗漏ID。'
 ],
 'cleanup':{'temporaryCachesCreated':False,'mainProjectModified':False}
}
(OUT/'boarding-status-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report['summary'],ensure_ascii=False))
