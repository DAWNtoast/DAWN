"""Publish the reviewed boss change notes from retained calibration evidence."""
import argparse
import csv
import hashlib
import json
import re
from pathlib import Path
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/boss-behavior-20261027'
CAL = OUT / 'calibration'
parser = argparse.ArgumentParser()
parser.add_argument('--final-run', default='final-seed1-telemetry')
parser.add_argument('--pressure-run', default='final-candidate-seeds8')
parser.add_argument('--sources-run', required=True)
args = parser.parse_args()

def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p): return list(csv.DictReader(p.open(encoding='utf-8-sig', newline='')))
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def link(p): return '[查看](<' + p.as_posix() + '>)'

final = CAL / args.final_run
pressure = CAL / args.pressure_run
sources = CAL / args.sources_run
before = {r['id']: r for r in rows(CAL / 'before-seed1/boss-profiles.csv')}
profiles = rows(final / 'boss-profiles.csv')
fs, ps, ss = (load(p / 'calibration-summary.json') for p in (final, pressure, sources))
cleanup_paths = list(CAL.glob('*/cleanup.json')) + list(
    (ROOT / 'Artifacts/BossCalibration20261024').glob('BossBehavior20261027-*/cleanup.json')) + [
    OUT / 'calibration-production-compile/cleanup.json']
cleanup_bytes = 0
for p in cleanup_paths:
    values = load(p)
    cleanup_bytes += next((int(values[k]) for k in ('FreedBytes', 'ReleasedBytes', 'RemovedExecutableBytes') if k in values), 0)
    assert not (p.parent / 'Build').exists(), f'Task build cache remains: {p.parent}'
result_path = ROOT / 'Artifacts/BossCalibration20261024/BossBehavior20261027-FinalCatalog/results.xml'
tests = ElementTree.parse(result_path).getroot().attrib
assert tests['result'] == 'Passed' and int(tests['failed']) == 0

# Frozen Core must still represent the delivered source, including the final path fix.
stable = []
for p in (final / 'CoreSnapshot').glob('*.cs'):
    current = ROOT / 'UnityClient/Assets/_Project/Core/Runtime' / p.name
    if digest(p) != digest(current): stable.append(p.name)
assert not stable, f'Core changed after final calibration: {stable}'
for row in rows(OUT / 'calibration-production-compile/source-hashes.csv'):
    source = Path(row.get('Source') or row.get('Path'))
    assert digest(source) == row['SHA256'].upper(), f'Production source changed: {source}'

chains = [
    ('烈焰', '火场封航 → 续焰添薪 → 焰沿扩散 → 引火压舷 → 爆燃齐射 → 炉舱回整', '6', '延长2回合；扩域8格；火炮与燃烧施压'),
    ('奥术', '折射蓄能 → 星阵续航 → 镜域展开 → 星轨试射 → 聚束校准 → 聚束释放 → 星帆回整', '7', '延长3/2回合；扩域8格；提高法力上限并消耗法力爆发'),
    ('咒术', '禁域缚魂 → 幽界漫延 → 咒域续契 → 续印咒击 → 蚀甲穿魂 → 静默换防', '6', '扩域8格；延长3回合；叠咒印并穿透格挡'),
    ('潮汐', '引潮壁垒 → 长潮固守 → 水脉扩张 → 退潮反击 → 撤盾回整', '5', '延长2回合；扩域8格；格挡吸伤积累潮势后反击'),
    ('雷霆', '雷栅布阵 → 电网蔓延 → 雷场续能 → 电弧试射 → 终雷蓄势 → 连锁终雷 → 余电回整', '7', '扩域8格；延长2回合；连锁蓄力，评估过载后选择位置'),
    ('血契', '血池仪式 → 献血重炮 → 血契续池 → 血潮漫溢 → 偿血炮击 → 赤帆回整', '6', '延长3回合；扩域8格；付血返盾与吸血炮击'),
    ('守藏舰', '宝库潮锁 → 锁海扩界 → 潮锁续阵 → 守藏齐射 → 炮舱换防', '5', '水流扩域8格；延长2回合；防御后齐射'),
]

md = ['# Boss 行为与数值调整', '', '适用当前工程中的6名路线首领、6艘传奇战舰和守藏舰。已修改源码并完成验证，尚未重新出包。', '',
      '## 行为链', '', '不再统一为四步循环。行动序号与海战回合分别计数：追击但尚未执行技能时不推进技能链。每一步都预告下一步；最后一步提供回整窗口，只有此窗口可以选择合法冲撞，极速行进回合不会冲撞。', '',
      '| 体系 | 步数 | 行为顺序 | 场地与特色 |', '|---|---:|---|---|']
for name, chain, length, feature in chains:
    md.append(f'| {name} | {length} | {chain} | {feature} |')
md += ['', '## 免疫与极速行进', '',
       '| 海战回合 | 防护 | 行进 |', '|---|---|---|',
       '| 3、8、13、18、23… | 仅免疫所属体系场地的负面效果 | 正常移动预算 |',
       '| 5、10、15、20、25… | 免疫所有体系场地的负面效果 | 最多5格，根据收益与危险选择落点 |',
       '| 其他回合 | 无上述周期防护 | 正常移动预算 |', '',
       '- 防护在对应敌方回合开始时生效，敌方回合结束时清除。行动意图提前显示防护类型，状态栏只显示已生效的防护。',
       '- 场地的法力、伤害强化、减伤、连锁保留/免耗及付血返盾等正面收益保留；已有燃烧、咒印不会被清除。',
       '- 守藏舰以自己的定向水流作为所属场地。训练与教学练习不启用这套周期；教学最终实战保留极速行进例外。',
       '- 移动会比较射程、船体损失、燃烧、咒印、逆流、折射阵易伤、雷栅过载和可实际兑现的付血返盾；有利时可以留守，不再一律贴近目标。',
       '- 最多5格是主动行进上限，不要求每次走满；不会落在玩家船所在格。普通敌人的原有移动评分保留。', '',
       '## 延长与扩大场地', '',
       '- 延长只作用于4格内可触及的、自有同体系且仍有效的场地实例；同一实例整体延长，最多剩余6个玩家回合。',
       '- 扩域从原边界向正交相邻空格生长，单次最多8格，继承原场地剩余时间与水流方向。不会覆盖其他已有场地。',
       '- 火场/禁域/水流优先向玩家所在方向扩展；折射阵/血池/雷栅优先维持自身可利用区域。',
       '- 自有同体系总面积上限：火场25格、折射阵20格、禁域20格、水流32格、雷栅65格、血池20格。重新铺场替换原来的自有同系场地，避免无限叠加。',
       '- 延长或扩域时若没有可用自有场地，则补铺基础场地。意图显示本次实际延长/新增格数；延长播放续场提示，不使用消失动画。', '',
       '## 数值', '', '下表为游戏中的最终船体值，已经计入统一1.5倍率。适度增加存活时间，四处下调基础炮击以控制峰值；海战格挡、初始格挡、接舷船员/生命/攻击/格挡、奖励与掉落不变。', '',
       '| 首领 | 船体：原 → 新 | 基础炮击：原 → 新 |', '|---|---:|---:|']
for p in profiles:
    old = before[p['id']]
    md.append(f"| {p['name']} | {old['hull']} → {p['hull']} | {old['cannon']} → {p['cannon']} |")

def outcome_text(s):
    names = {'sunk': '击沉敌舰', 'dead': '玩家沉没', 'boarding': '进入接舷', 'timeout': '达到回合上限'}
    return '、'.join(f"{names.get(k,k)}{v}场" for k,v in s['outcomes'].items())

md += ['', '## 验证与平衡样本', '',
       f"- 核心目标回归：{tests['passed']}项通过、0失败。覆盖不同链长、3/5/8/10及后续周期、正负效果分离、预览无副作用、扩域边界、场地到期、快照恢复、相邻侧移、空场转向、训练与教学等。", 
       '- Core、Content、Persistence、Presentation四个正式程序集编译通过，验证期间源码哈希稳定。',
       f"- 最终源码复核组：{fs['samples']}场，{outcome_text(fs)}；平均{fs['meanTurns']}个海战回合，首轮爆发实际执行比例{fs['firstBurstExecutionPercent']}%。", 
       f"- 多随机种子压力组：{ps['samples']}场，{outcome_text(ps)}。该快照早于最后一处空场移动路径统一修复，最终同种子组用于核对结果一致性；不将单种子旧基准与整个压力组混算前后差。",
       f"- 带组件附卡的独立组：{ss['samples']}场，{outcome_text(ss)}。与无组件附卡组分开统计。", '',
       '这些是有限策略的自动海战样本，不能等同于真实玩家胜率。快速击杀流派可以在首领完成全链前获胜；进入接舷只算海战转阶段，不算胜败。后续较晚免疫回合以受控机制测试覆盖，不为了覆盖回合而强行堆高船体。', '',
       '未修改玩家卡牌定义，因此本次不重新导出固定卡牌清单。仓库、收集等同时存在的其他改动不计入本次Boss修改。', '',
       '## 保留证据', '',
       f'- 核心测试：{link(result_path)}',
       f'- 最终模拟汇总：{link(final / "calibration-summary.json")}',
       f'- 压力组汇总：{link(pressure / "calibration-summary.json")}',
       f'- 带组件组汇总：{link(sources / "calibration-summary.json")}',
       f'- 修改文件与哈希：{link(OUT / "changed-files.json")}',
       f'- 独立集成复查：{link(OUT / "behavior-integration-review.json")}', '',
       f'本任务测试/编译/模拟二进制已按具体目录清理，现存清理记录核验至少回收{cleanup_bytes / 1024 / 1024:.1f} MiB；保留输入、源码快照、日志与验收记录。主工程Library及其他任务缓存未清理。', '']
(OUT / 'Boss行为与数值调整.md').write_text('\n'.join(md), encoding='utf-8')
acceptance = {'tests': tests, 'productionAssemblies': ['Core','Content','Persistence','Presentation'],
              'finalCoreStable': not stable, 'finalRun': args.final_run, 'pressureRun': args.pressure_run,
              'sourcesRun': args.sources_run, 'cardsModified': False, 'gamePackageCreated': False,
              'verifiedCleanupBytesAtLeast': cleanup_bytes, 'cleanupEvidence': [str(p) for p in cleanup_paths],
              'report': str(OUT / 'Boss行为与数值调整.md')}
(OUT / 'acceptance.json').write_text(json.dumps(acceptance, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'report': str(OUT / 'Boss行为与数值调整.md'), 'testsPassed': tests['passed']}, ensure_ascii=True))
