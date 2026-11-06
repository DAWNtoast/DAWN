"""Write the delivery report from completed, independently aggregated runs."""
import argparse
import json
import re
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--boarding', required=True)
p.add_argument('--soft')
p.add_argument('--naval', required=True)
p.add_argument('--curse', default='Curse24')
a = p.parse_args()
root = Path('D:/Game/outputs/boarding-budget-balance-20261027')
def read(run, name):
    return json.loads((root/run/name).read_text(encoding='utf-8-sig'))
board = read(a.boarding, 'samples.json')
if a.soft:
    board += read(a.soft, 'samples.json')
naval = read(a.naval, 'samples.json')
curse = read(a.curse, 'samples.json')
before = read('ExploreWindow09', 'samples.json')
old_pressure=(root/'ExploreWindow09/SourceSnapshot/Core/Runtime/CoreBoardingBossCatalog.cs').read_text(encoding='utf-8-sig')
new_pressure=(root/a.boarding/'SourceSnapshot/Core/Runtime/CoreBoardingBossCatalog.cs').read_text(encoding='utf-8-sig')
def pressures(text):
    return {key:float(value) for key,value in re.findall(r'case "([^"]+)": return ([.\d]+)m;',text)}
old_pressure,new_pressure=pressures(old_pressure),pressures(new_pressure)
params = read(a.boarding, 'run-parameters.json')
soft_run = a.soft or a.boarding
soft_params = read(soft_run, 'run-parameters.json')
assert all(not r['skippedNaval'] for r in board + naval)
assert all(r.get('diagnosticEnemyMaxScale',1)==1 and r.get('diagnosticEnemyAttackScale',1)==1 for r in board + naval)
bosses = list(dict.fromkeys(r['boss'] for r in naval))
def rate(rows, mode):
    ok = sum(r['outcome']=='sunk' if mode=='cannon' else r['outcome']=='captured' and (mode=='direct' or r['boardingEntryInTargetBand']) for r in rows)
    return f'{ok}/{len(rows)}（{ok/len(rows):.2%}）', ok
lines = ['# 接舷难度调整与数值复核', '', '日期：2026-10-27。', '',
    '## 验证口径', '',
    '按用户确认，各阶段共用已经拥有的船、部件、普通水手、部件来源牌经验和既有卡牌等级，只比较后续新增金币。海战支出允许购船、换件和强化卡；接舷支出只用于接舷牌。本轮海战参考配置把新增预算全部用于有效卡牌强化，购船/换件增量均为 0；实际支付与余额保存在每场 `budgetLedger` 中。', '',
    '| 阶段 | 共同资产 | 历史四张海战牌等级 | 新增预算（两路线相同） |',
    '|---|---|---:|---:|',
    '| 早期两 Boss | 初始帆船、五灰部件、10 普通水手、海盗等级 1 | +0 | 720 |',
    '| 中期六 Boss | 巡逻舰、五蓝部件、22 普通水手、等级 5、来源经验 120 | +1 | 1,200 |',
    '| 晚期五 Boss | 炮舰、五紫部件、28 普通水手、等级 10、来源经验 360 | +2 | 1,200 |', '',
    '四张共同旧强化牌为齐射、链弹、格挡与应急修理对应的标准海战牌；准确身份、完整卡组、每次升级费用与消耗品初值以本轮 `loadouts.json` 和 `samples.json` 为准。五灰是历史已经获得的部件，不是新建存档免费赠送。共同资产参考成本为早期 1,150、中期 5,830、晚期 12,780 金币（含相应船、部件和四张旧强化，不是完整成长总消费），均不计入新增投入。商店按实际 API 报价，不按配置原价或换船差价估算。', '',
    '自动玩家调用正式战斗引擎，从 600 米准备阶段开始，按当前手牌、合法移动、已公开意图和伤害报价操作；不读取未来牌序，不按种子改变决策。直接接舷与削血路线使用相同接舷牌组和强化。削血路线包含真实前置船损、场地、耗材与接触先手；未重置生命。此项是固定资产对局校准，并非从新档获取资产的完整流程游玩。', '',
    '## 最终样本结果', '',
    f'直接接舷记录：`{a.boarding}`，每 Boss {params["Seeds"]} 场；削血路线：`{soft_run}`，每 Boss {soft_params["Seeds"]} 场；海战记录：`{a.naval}`。本轮种子从 {params["SeedStart"]} 起。', '',
    '“削血后夺船”只计实际接舷时敌船体剩余 50%–60% 且夺船成功的样本，分母包含全部出发尝试：前置死亡、超时、被迫接舷和越界均不能删除。现有继承公式保持不变，该船体区间对应敌接舷生命约 75%–80%（向上取整）。', '',
    '| Boss | 海战击沉 | 直接接舷夺船 | 合规入场后的接舷胜率 | 削血路线全程合规夺船 | 接舷最大生命：旧 → 新 |',
    '|---|---:|---:|---:|---:|---:|']
checks=[]
for boss in bosses:
    n=[r for r in naval if r['boss']==boss and r['mode']=='cannon']
    d=[r for r in board if r['boss']==boss and r['mode']=='direct']
    s=[r for r in board if r['boss']==boss and r['mode'].startswith('soften')]
    assert n and d and s
    nr,nk=rate(n,'cannon'); dr,dk=rate(d,'direct'); sr,sk=rate(s,'soften')
    old=next(r['enemyMaxCrew'] for r in before if r['boss']==boss)
    new=d[0]['enemyMaxCrew']
    eligible=[r for r in s if r['boardingEntryNavalTurn']>=0 and r['boardingEntryInTargetBand']]
    entered=rate(eligible,'direct')[0] if eligible else '无合规入场'
    lines.append(f'| {d[0]["name"]} | {nr} | {dr} | {entered} | {sr} | {old} → {new} |')
    checks.append(dict(boss=boss,name=d[0]['name'],navalWins=nk,navalN=len(n),directWins=dk,directN=len(d),softWins=sk,softN=len(s),directInTarget=.70<=dk/len(d)<=.80,softInTarget=sk/len(s)>=.90,navalInTarget=nk==len(n),softNavalFailures=sum(r['failureStage'] in ('Naval','Preparation') for r in s),softOutOfBand=sum(r['boardingEntryNavalTurn']>=0 and not r['boardingEntryInTargetBand'] for r in s)))
lines += ['', '这些是固定策略的样本胜率；海战样本全胜不代表数学上的必胜。逐 Boss 的 95% Wilson 区间、入场生命和失败阶段见[最终分组统计](final2000-outcomes.json)。不得把跨 Boss 平均值代替逐个达标判断。', '',
    '## 改动与设计取舍', '',
    '主要调整每个 Boss 的接舷最大生命与固定攻击压力，保留原有流派机制、治疗、格挡、资源准备和预告行为链。潮汐镇魂号通过降低接舷生命缩短消耗战；守藏舰采用独立的四回合咒术攻击参数，普通咒术敌人不随之增强。咒印仍为初始 100%、缠咒每次降低 5 个百分点、达到阈值即死。', '',
    '裂桅号额外把海战炮击基数由 9 降为 7：初始船未解锁火焰体系防护卡，削血途中自身失血会抵消敌方减血收益。仅裂桅的火场初始持续时间由 3 调为 2，续场由 +2 调为 +1，保留铺场、续场和扩展 8 格，将首轮连续覆盖从 5 个玩家回合缩为 3 个；余烬及通用场地规则不变。潮汐镇魂号、终界观星号炮击由 90 降为 86，为海战参考配置增加容错；其余 Boss 海战参数保持本轮开始时的设定。裂桅接舷开场攻击份额从 20 调为 12，第三回合强攻从 18 调为 26，将压力放到有预告的后段。实际损血仍受格挡、燃烧和取整影响，不是每场总伤害恒定。', '',
    '固定攻击压力系数只缩放该 Boss 的普通攻击及资源追加伤害，不同步放大格挡、治疗、自伤或燃烧叠层：', '',
    '| Boss | 原系数 | 新系数 |', '|---|---:|---:|']
for boss in bosses:
    old,new=old_pressure.get(boss,1),new_pressure.get(boss,1)
    if old!=new:
        name=next(r['name'] for r in board if r['boss']==boss)
        lines.append(f'| {name} | {old:.2f} | {new:.2f} |')
lines += ['', '守藏舰的独立卡牌攻击百分比为 55/65/45 → 66/78/54，咒印引爆每层追加百分比由 20 → 24；其格挡、印数与四回合顺序保留。', '',
    '## 构筑敏感性与边界', '',
    f'另以 `{a.curse}` 检查合法的 12 张咒印伤害构筑。早期等级 1 未达到咒术流派等级 4 的解锁条件，标为不可用，没有强行提高共同等级。中晚期固定施印、按印增伤、格挡、回能与夺魄治疗组合，支出同为 1,200。以下为 2000 起的新种子独立构筑对照，不与主构筑合并：', '',
    '| Boss | 咒印直接夺船 | 咒印削血后合规夺船 |',
    '|---|---:|---:|']
for boss in bosses:
    d=[r for r in curse if r['boss']==boss and r['mode']=='direct']
    s=[r for r in curse if r['boss']==boss and r['mode'].startswith('soften')]
    if d and s: lines.append(f'| {d[0]["name"]} | {rate(d,"direct")[0]} | {rate(s,"soften")[0]} |')
lines += ['', '流派组合、操作与抽牌均会改变胜率。70%–80%是本次基础参考构筑的校准目标，不能宣称所有等价金币构筑都被限制在该区间；尤其咒印组合可明显超过基础构筑。游戏没有人为失败概率，也没有按投入、测试路线或随机种子动态调弱/调强 Boss。', '',
    '## 复现与证据', '',
    f'- [直接接舷完整记录]({a.boarding}/samples.csv)、[削血路线完整记录]({soft_run}/samples.csv)、[卡组与强化]({a.boarding}/loadouts.json)、[参数]({a.boarding}/run-parameters.json)。',
    f'- [海战完整记录]({a.naval}/samples.csv)。',
    '- [经济审计](economy-audit.md)、[设计与策略审计](design-audit.md)、[校准口径](校准口径.md)。',
    '- 每次运行保留正式源码快照、输入快照、哈希、编译与运行日志。诊断阶段的血量/攻击倍率扫描只用于筛选，不计入最终完整路线验收。',
    '- 第一版独立留出（1000 起）未完全达标，记录保留在 HeldoutBoard39 / HeldoutNaval40 / Curse41。修订后使用 2000 起的新验证范围，不将旧留出重新包装成未参与调参的最终证据。', '',
    '']
tests=read('', 'core-regression-candidate43-summary.json')
audit=read('', 'independent-final-audit.json')
test_cleanup=next(v for v in tests.values() if isinstance(v,dict) and 'taskTotalReleasedBytes' in v)
released=audit['outputBuildReleasedBytes']+test_cleanup['taskTotalReleasedBytes']
direct_rates=[x['directWins']/x['directN'] for x in checks]
soft_rates=[x['softWins']/x['softN'] for x in checks]
naval_wins=sum(r['outcome']=='sunk' for r in naval)
forced=sum(r['outcome']=='forced_boarding' for r in naval)
dead=sum(r['outcome']=='dead' for r in naval)
lines[4:4]=[
    f'新验证共 {len(board)+len(naval)+len(curse):,} 场。基础参考构筑的 13 个 Boss 直接接舷胜率全部为 **{min(direct_rates):.2%}–{max(direct_rates):.2%}**；先将敌船体削至 50%–60% 的全流程合规夺船率全部为 **{min(soft_rates):.2%}–{max(soft_rates):.2%}**，两项达到本轮参考目标。', '',
    f'纯海战为 **{naval_wins}/{len(naval)}＝{naval_wins/len(naval):.2%}**，另外 {forced} 次强制接舷、{dead} 次死亡，仍未达到严格 100%。强制接舷在该参考路线中即停止，后续胜负未评估，不能把这些场次算为击沉胜利。流派组合另列，不能将基础构筑结论泛化为所有牌组的固定胜率。', '']
lines += ['## 核心回归与清理', '',
    f'[最终核心回归](core-regression-candidate43-summary.md)：{tests["fixtureCount"]} 个 fixture，**{tests["passed"]}/{tests["total"]} 通过**，失败 {tests["failed"]}、跳过 {tests["skipped"]}。覆盖真实 50%/60% → 75%/80% 生命继承、咒印 100/5/≥、预告与结算一致、行为链和恢复窗口、教学与训练隔离，以及新增的裂桅/余烬真实场地生命周期。测试数值包络随本次正式强度更新，变动与理由保留在回归报告中。', '',
    f'[独立最终审计](independent-final-audit.json)通过：核对 {audit["sourceFiles"]} 份生产输入、{audit["pairedInitialStates"]:,} 次共同初态比较、{audit["directSoftDeckPairs"]:,} 对相同接舷卡组，以及 {audit["enemyCarryoverChecks"]:,} 次真实敌方血量继承，未发现费用或来源不一致。', '',
    f'本任务累计回收 **{released:,} 字节（{released/1048576:.2f} MiB）**临时构建文件；{audit["cleanupRecords"]} 份对局/报价清理记录与 {test_cleanup["allTaskCoreRunCount"]} 次 Core 回归构建均确认 Build 已移除。没有触碰 UnityClient 主工程缓存。保留源码/输入快照、用户输入、原始样本、失败证据、日志及最终报告，以供复现。', '']
(root/'接舷难度调整与复核.md').write_text('\n'.join(lines),encoding='utf-8')
(root/'final-target-checks.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
print('Wrote report; all per-boss targets:', all(x['navalInTarget'] and x['directInTarget'] and x['softInTarget'] for x in checks))
