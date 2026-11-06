"""Summarize the independent pressure groups without claiming population win rates."""
import csv,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
output=root/'outputs/boss-behavior-20261027'
calibration=output/'calibration'
groups=[('最终源码精确遥测验收',calibration/'final-seed1-telemetry'),('早一处移动修正的固定牌组压力样本',calibration/'final-candidate-seeds8'),('最终源码追加实际部件来源牌',calibration/'final-candidate-sources-seeds2')]
datasets=[]
for label,path in groups:
    data=json.loads((path/'calibration-summary.json').read_text(encoding='utf-8'))
    cleanup=json.loads((path/'cleanup.json').read_text(encoding='utf-8-sig'))
    datasets.append((label,path,data,cleanup))
with (groups[0][1]/'boss-profiles.csv').open(encoding='utf-8-sig',newline='') as f:profiles={r['id']:r for r in csv.DictReader(f)}
with (calibration/'before-seed1/boss-profiles.csv').open(encoding='utf-8-sig',newline='') as f:oldprofiles={r['id']:r for r in csv.DictReader(f)}
for boss,profile in profiles.items():
    assert all(profile[k]==oldprofiles[boss][k] for k in ['boardingHealth','boardingMelee','boardingGuard','guard','initialGuard']),boss
report={'groups':[],'boardingAndGuardUnchanged':True,
        'sourceQualification':'Only the 8-seed pressure run predates the final empty-sea non-rush boss movement-path fix. Its 364 shared seed-0 results and battle traces equal the final-source run exactly. Final telemetry and Sources groups use the final Core snapshot.'}
lines=['# 海战 Boss 基础数值校准压力样本','',
'以下为可复现的有限策略样本。两组牌源不同，分别报告；这些结果不表示真实玩家胜率。',
'只有 8 seeds 压力组冻结在最终空海图非冲锋 Boss 移动路径修正之前；其共同 seed 0 的 364 场与最终源码全部结果字段及战斗 trace 完全一致。精确遥测组与追加部件来源组均使用最终源码。','',
'两组保留实际 600 米入场、准备、合法移动、资源消耗与敌方执行，最多 30 海战回合；接舷转换单独记作停止观察，不算击沉或失败。起始满血/70%，伤害/生存两种策略，七流派，十三 Boss。接舷生命/伤害/格挡及海战格挡、初始格挡与基准一致。','']
for label,path,data,cleanup in datasets:
    report['groups'].append({'label':label,'directory':str(path),'parameters':data['runParameters'],
                            'samples':data['samples'],'outcomes':data['outcomes'],'meanTurns':data['meanTurns'],
                            'firstBurstExecutionPercent':data['firstBurstExecutionPercent'],'firstBurstPossiblyMissedSamples':data['firstBurstPossiblyMissedSamples'],
                            'maxEnemyWindowNetHullLossPercent':data['maxEnemyWindowNetHullLossPercent'],
                            'maxRoundNetHullLossPercent':data['maxRoundNetHullLossPercent'],
                            'cleanup':cleanup})
    lines += [f'## {label}', '',
              f"{data['samples']} 场，结果："+'、'.join(f'{k}={v}' for k,v in data['outcomes'].items())+'。',
              f"平均 {data['meanTurns']} 回合；首轮爆发已确认执行 {data['firstBurstExecutedSamples']} 场（{data['firstBurstExecutionPercent']}%），终局计数不完整而可能漏计 {data['firstBurstPossiblyMissedSamples']} 场。",'',
              '| Boss | 船体 / 炮击 | 满血平均回合 | 满血首轮爆发 | 满血剩余船体 | 满血/70%失败 | 最大敌方窗口净失血 |',
              '|---|---:|---:|---:|---:|---:|---:|']
    for boss in data['bosses']:
        profile=profiles[boss['boss']];full=boss['byStartingHealthPercent']['100'];damaged=boss['byStartingHealthPercent']['70']
        lines.append(f"| {profile['name']} | {profile['hull']} / {profile['cannon']} | {full['meanTurns']} | {full['firstBurstExecutionPercent']}% | {full['meanEndHullPercent']}% | {full['outcomes'].get('dead',0)} / {damaged['outcomes'].get('dead',0)} | {boss['maxEnemyWindowNetHullLossPercent']}% |")
    lines += ['',f"耗时 {cleanup['ElapsedSeconds']:.2f} 秒；已清理可重建编译文件 {cleanup['RemovedExecutableBytes']:,} 字节。",'']
lines += ['## 证据口径','',
'首轮爆发使用实际完成动作计数跨过源码的爆发步判定，未把展示意图计为执行。旧遥测在终局清空 session.Battle 时漏记最后计数，所以 8 seeds 爆发为已确认下界；最终精确遥测及 Sources 无此缺口。终局船体均以结果 CSV 补齐。最大敌方窗口净失血测量 EndBattleTurn 前后，包含场地/燃烧/下回合开始效果；治疗可抵消损失，不等于单炮或瞬间峰值。整个回合首尾净失血也保存在 JSON 中。',
'每场结果、逐窗口记录、源码哈希、冻结输入、日志、sample-telemetry-metrics.json 和 calibration-summary.json 全部保留。单 seed 旧基准只与共同 seed 0 做严格配对，新增 seeds 单独作为压力检查；配对文件明确记载未配对场数。',
'编译产物已按绝对路径和非链接检查安全回收，主工程 Library 与源码不因清理被改动。','']
(output/'calibration-pressure-summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
(output/'海战Boss校准-压力样本.md').write_text('\n'.join(lines),encoding='utf-8')
print(json.dumps({'report':str(output/'海战Boss校准-压力样本.md'),'samples':sum(d['samples'] for _,_,d,_ in datasets)},ensure_ascii=False))
