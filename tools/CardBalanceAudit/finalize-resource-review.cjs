'use strict';
// Persist explicit review limits alongside the raw, immutable audit results.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'Artifacts/CardSchoolRebalance20261012');
const core = process.argv[2] || 'CoreValidated';
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/^\uFEFF/, '');
const failureNames = p => read(p).trim().split(/\r?\n/).slice(1).map(r => r.split('\t')[0]);
const old = failureNames('Artifacts/CardDeckSupplyAudit20261012/CoreCompleteFinal/failures.tsv');
const current = failureNames(`Artifacts/CardSchoolRebalance20261012/${core}/failures.tsv`);
const result = read(`Artifacts/CardSchoolRebalance20261012/${core}/results.xml`);
const counts = Object.fromEntries(['total','passed','failed','skipped','inconclusive'].map(k => [k, Number(result.match(new RegExp(`\\b${k}="(\\d+)"`))?.[1] || 0)]));
const unity = JSON.parse(read('Artifacts/CardResourceRulesAudit20261012/validation-summary.json'));
const added = current.filter(n => !old.includes(n));
const fixed = old.filter(n => !current.includes(n));
const validation = {
  status: 'review_required_not_packaged', packaged: false,
  summary: `Core 卡牌相关测试 ${counts.passed}/${counts.total} 通过；剩余 ${counts.failed} 项是旧战役木桩引用失效，与此前失败名单比较新增 ${added.length} 项、解决 ${fixed.length} 项。Unity 定向 EditMode ${unity.editMode.passed}/${unity.editMode.total}、PlayMode ${unity.playMode.passed}/${unity.playMode.total} 通过；风暴续令最终单卡修改另经 ${unity.lastCardSupplement?.passed || 0}/${unity.lastCardSupplement?.total || 0} 项补充检查。不能表述为全项目测试全部通过。`,
  core: { directory: core, ...counts, existingFailures: current, newFailures: added, resolvedFailures: fixed,
    cause: 'NavalBattleBalanceSimulationTests still expects removed campaign enemies and seven chart-three bosses; the current catalog has one of those bosses. New fixed dummy tests do not replace or conceal these failures.' },
  unity, unitySummary: '../CardResourceRulesAudit20261012/validation-summary.json',
  liveScreens: '../CardResourceRulesAudit20261012/Unity/LiveScreensVerified/index.html',
  limits: ['定向验证不等于完整 Unity 项目验收。', '此报告停留在用户审核阶段，没有生成发布包。']
};
const decision = {
  status: 'mechanics_implemented_balance_targets_not_met', packaged: false,
  headline: '新规则已落地；六系等强、约高于普通牌 30% 和耗材接近尚未达标。',
  summary: '同配置第三档接舷胜局中，火焰平均耗材 12.75 件、潮汐 10 件，其他体系为 2.5–5.25 件。诅咒仍有第四档零耗材胜局。完整表同时展示胜率，不能用少数胜局的低消耗代表整体强度。',
  agreedRules: { mana: '0/3 起步；星盘护幕每次打出本场法力上限 +1，不回蓝。常规法力余额保留，不自动恢复；临时法力可超上限，玩家回合末清除。', upgrade: [1,1.2,1.5,2], ordinaryMultiplier: 1, basicCopies: 2, deckBasicRange: [10,20], materialMetric: '实际火药、炮弹、木板、药品扣除数之和', buildGate: '用户审核完成后才能打包' },
  implementedTuning: ['诅咒施咒层数下调，免费咒盾系数由每层 2 降至 1；强力咒盾仍需消耗咒印。', '火焰有火药强化与固定弱保底；潮汐付费盾必须实际吸收伤害才产潮势。', '血契提高付血、限制免费吸血/失血回复；无物品治疗保底最多 2。', '风暴续令连锁门槛由 3 改为 2，使两种雷攻后能在正常 3 点行动内支付此牌；费用、抽牌量不变。'],
  resourceWindowRegression: { path: 'ResourceWindow78/results.xml', passed: 78, evidence: '新增 3 例：原级与三阶风暴续令在正常 3AP 下抽 2/3 张，雷链保持 2；扩容和蓄蓝后天涯星坠真实支付 4MP/3AP/1粉/1弹。仅重排合法牌组的现有卡牌，不注入战斗资源。' },
  remainingFindings: [
    { issue: '诅咒仍独享更高难度零耗材路径', evidence: '主矩阵三阶零补给，诅咒第四档 10/48；其余组最高第三档。', recommendation: '下一轮可审核每批咒印独立到期，减少续咒刷新全部旧层数带来的持续增长；或提高强咒盾消耗。仍遵守咒盾无数值上限。', implemented: false },
    { issue: '物品总消耗差距明显', evidence: '同配置第三档接舷，特殊体系 2.5–12.75 件。炮击路径为 20.25–38 件。', recommendation: '先调整火焰每份火药、潮汐每份木板的有效收益，再对比同构筑规模与打法的胜率和耗材；避免直接给低消耗体系添加隐藏物品税。', implemented: false },
    { issue: '特殊体系未稳定超过普通牌', evidence: '同配置第四档，普通接舷 3/4、普通炮击 4/4；各特殊体系并未稳定达到该胜率。', recommendation: '下一轮针对 10/15/20 张牌组各自优化组牌与出牌策略，再确定普通与特殊的强度预算。当前不能换算或承诺 130%。', implemented: false }
  ],
  statisticalLimits: ['主矩阵每格 4 个配对种子；来源牌敏感性每格 2 个种子。', '启发式决策并非最优构筑证明；无胜局项不当作零消耗。', '120 回合未决标为超时，不计胜利。', '来源牌单张加入测试不等于完整装备套装测试，不能与基础主矩阵混算。']
};
fs.writeFileSync(path.join(out, 'validation.json'), JSON.stringify(validation, null, 2));
fs.writeFileSync(path.join(out, 'balance-decision.json'), JSON.stringify(decision, null, 2));
console.log(JSON.stringify({ counts, newFailures: added.length, resolvedFailures: fixed.length, unityEdit: unity.editMode.passed, unityPlay: unity.playMode.passed }));
