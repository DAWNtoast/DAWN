"""Create a concise reviewable A/B calibration note from preserved measurements."""
import csv,json
from pathlib import Path

root=Path(__file__).resolve().parents[2]
output=root/'outputs/boss-behavior-20261027'
base=output/'calibration/before-seed1'
after=output/'calibration/after-untuned-seed1'
before_data=json.loads((base/'calibration-summary.json').read_text(encoding='utf-8'))
after_data=json.loads((after/'calibration-summary.json').read_text(encoding='utf-8'))
before={b['boss']:b for b in before_data['bosses']}
with (after/'boss-profiles.csv').open(encoding='utf-8-sig',newline='') as f:profiles={r['id']:r for r in csv.DictReader(f)}
rows=[]
for current in after_data['bosses']:
    old=before[current['boss']];full=current['byStartingHealthPercent']['100'];profile=profiles[current['boss']]
    rows.append({'boss':current['boss'],'name':profile['name'],'hull':profile['hull'],'cannon':profile['cannon'],
                 'beforeMeanTurns':old['meanTurns'],'afterMeanTurns':current['meanTurns'],
                 'beforeMeanActions':old['meanActionsCompleted'],'afterMeanActions':current['meanActionsCompleted'],
                 'beforeBurstPercent':old['firstBurstExecutionPercent'],'afterBurstPercent':current['firstBurstExecutionPercent'],
                 'afterFullHealthMeanTurns':full['meanTurns'],'afterFullHealthBurstPercent':full['firstBurstExecutionPercent'],
                 'afterMaxEnemyWindowLossPercent':current['maxEnemyWindowNetHullLossPercent']})
with (output/'calibration-comparison.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
lines=['# 海战 Boss 行为校准：未调基础数值的配对结果','',
'这一轮是固定装备、合法牌组、两种有限策略的诊断，不能视为真实玩家胜率。',
'before 来自 r03 权威源码与上一轮审查后快照的逐文件哈希重建；after 来自本轮行为与免疫落盘后的稳定双读冻结。两轮都不读取主工程 Library，也没有启动 Unity。','',
'- 每轮 13 Boss × 7 流派 × 满血/70% × 伤害/生存策略 × 1 seed = 364 场，最多 30 海战回合。',
'- 配对验证通过：装备输入、牌组、实际使用牌组哈希和玩家初始参数相同。',
'- 两轮均 360 击沉、4 玩家沉没；2 场由沉没变为击沉，另 2 场相反。平均剩余船体增加 10.316 点。',
'- 新链首轮爆发已确认至少执行 223/364（61.26%），另有 2 场终局计数缺失可能已执行；平均已记录 5.39 个行为、战斗 6.39 回合。旧遥测终局空引用问题已修复，最终验收使用修复版。',
'- 第 3/5/8/10 回合有 354/210/49/11 场到达敌方窗口；第 13 回合以后没有自然样本。3/8/13 与 5/10/15 的长周期正确性应交由受控机制测试，不靠增加正常血量强行覆盖。','',
'| Boss | 当前船体 | 炮击基数 | 旧→新平均回合 | 旧→新实际完成行为 | 新首轮爆发率 | 新满血回合/爆发率 | 最大敌方窗口净失血/满船体 |',
'|---|---:|---:|---:|---:|---:|---:|---:|']
for r in rows:
    lines.append(f"| {r['name']} | {r['hull']} | {r['cannon']} | {r['beforeMeanTurns']}→{r['afterMeanTurns']} | {r['beforeMeanActions']}→{r['afterMeanActions']} | {r['afterBurstPercent']}% | {r['afterFullHealthMeanTurns']} / {r['afterFullHealthBurstPercent']}% | {r['afterMaxEnemyWindowLossPercent']}% |")
lines += ['', '## 下一轮候选范围（尚未验证，不是已应用的最终数值）','',
'目标是对应装备满血多数战斗约 7–10 回合，并能看到首轮爆发；保留咒印、雷霆的速杀特性，不按统一胜率反推所有 Boss。先只改船体，炮击/灼烧/吸血系数不一起上调，以便解释 A/B 结果。','',
'- 月织、终界：先试船体 +20%～30%；当前满血不到 6 回合，首轮爆发低于半数。',
'- 绯刃、裂桅、余烬：先试船体 +10%～20%，保留伤害基数；火系已有明显失败样本，裂桅最大单敌方窗口净失血达满船体 41.82%。',
'- 幽礁、税金、极光、雷冠、月蚀、守藏、潮汐镇魂：先试船体 +10%～20%；若自然速度仍未达目标再小幅追加，不先改持续格挡。',
'- 铁誓：先试 +10%～15%，平均满血已 7 回合；优先观察普通/火/血等较慢流派，不因少量速杀把所有流派拖长。',
'- 将伤害调整留到耐久定稿后：未见爆发的样本无法估计爆发危害。建议炮击每次只变动 5%～10%，火系第一轮保持；保留休整步。潮系格挡同时会储存反击资源，不宜作为中性的血量替代。',
'- 场地已为常规 3 回合/雷栅 5 回合，续场 2 或 3、扩域最多 8 格；先保持这些新规则，以便把数值变化归因到船体。','',
'## 统一复现','',
'当前工具：`FreezeCurrentCore.py --tag <新标签>` 双读冻结；`RunCalibration.ps1 -RunTag <新标签> -SourceSnapshot <冻结目录> -Seeds 1 -MaxTurns 30` 执行；`SummarizeCalibration.py --before <基准目录> --after <新目录>` 严格配对并汇总。`CompileCalibrationProduction.ps1` 离线编译 Core/Content/Persistence/Presentation，无需 Unity。','',
'本轮 after 全 364 样本 56.66 秒（新增意图/回合遥测比最初基准更详细）。3 seeds 为 1092 场，按线性估算约 3 分钟/轮；先用 1 seed 收敛数值，再用多 seeds 检查结论是否稳健。额外部件来源 `-Sources` 必须另列 cohort，两边一致，不能混入本组配对。','',
'## 指标边界与清理','',
'“实际完成行为”读取真实执行计数器；爆发率要求计数器跨过源码中标记的首轮爆发步，不用预览或回合数推断。此轮有 12 场在终局清空 session.Battle，未采到最后一次计数，所以行为/爆发数字为已确认下界；其中 2 场可能恰好执行首轮爆发。终局船体已由结果 CSV 精确补齐，因此最大敌方窗口净失血没有丢失终局窗口。该值包含场地、燃烧和下回合开始效果，且治疗会抵消，不能称为单发伤害或瞬时伤害峰值。新样本另记录整个回合首尾净损失；旧样本没有该回合起点，因此该项不可做旧新同比。','',
'before/after 编译 exe 已分别清理 1,172,992 / 1,181,696 字节，共 2,354,688 字节。保留冻结源码、输入、逐回合遥测、哈希、日志及清理记录。主工程缓存保留。','']
(output/'海战Boss校准-未调参对比.md').write_text('\n'.join(lines),encoding='utf-8')
print(output/'海战Boss校准-未调参对比.md')
