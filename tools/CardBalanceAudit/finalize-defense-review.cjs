'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { csv, tsv } = require('./summarize-dummy-audit.cjs');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'Artifacts/CardDefenseReduction20261012');
const battles = path.join(root, 'UnityClient/Artifacts/CardDefenseReduction20261012');
const core = path.join(out, process.argv[2] || 'CoreFinal');
const readJson = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
const before = tsv(path.join(root, 'Artifacts/CardSchoolRebalance20261012/CoreReviewed/cards.tsv'));
const after = tsv(path.join(core, 'cards.tsv'));
const xml = fs.readFileSync(path.join(core, 'results.xml'), 'utf8');
const counts = Object.fromEntries(['total', 'passed', 'failed', 'skipped'].map(k => [k, Number(xml.match(new RegExp(`\\b${k}="(\\d+)"`))?.[1] || 0)]));
const failures = tsv(path.join(core, 'failures.tsv')).map(r => r.Test);
const priorFailures = tsv(path.join(root, 'Artifacts/CardSchoolRebalance20261012/CoreReviewed/failures.tsv')).map(r => r.Test);
const newFailures = failures.filter(n => !priorFailures.includes(n));
const unity = readJson(path.join(out, 'unity-validation.json'));
const completion = readJson(path.join(battles, 'ReviewAnalysis/completion.json'));
const manifest = readJson(path.join(out, 'base-defense-changes.json'));
const expected = n => Number(n) > 0 ? Math.max(1, Math.round(Number(n) * 0.7)) : 0;
const preserved = ['LibraryIndex','Id','ArtId','Name','Type','SchoolId','AllowedStages','Action','Energy','Damage','Draw','PowderCost','CannonballCost','MovementMeters','StrategyAmount','StrategyLimit','BurnDamage','BurnDuration','UpgradeLevel','RequiredCrew','RequiredComponent','DescriptionTemplate', ...Object.keys(before[0]).filter(k=>k.startsWith('SchoolEffect.'))];
const errors = [];
for (const a of after) {
  const b = before.find(c => c.Id === a.Id && c.UpgradeLevel === a.UpgradeLevel);
  if (!b) { errors.push('Unexpected card/rank ' + a.Id); continue; }
  for (const key of preserved) if (a[key] !== b[key]) errors.push(`${a.Id}+${a.UpgradeLevel} changed ${key}`);
  const original = before.find(c => c.Id === a.Id && c.UpgradeLevel === '0');
  const multiplier = [1,1.2,1.5,2][Number(a.UpgradeLevel)];
  for (const key of ['Block','Repair']) if (Number(a[key]) !== Math.round(expected(original[key]) * multiplier + 1e-9)) errors.push(`${a.Id}+${a.UpgradeLevel} unexpected ${key}`);
}
if (after.length !== 872 || errors.length) throw Error(JSON.stringify({ rows: after.length, errors }));
fs.writeFileSync(path.join(out, 'definition-verification.json'), JSON.stringify({ passed: true, ranks: after.length, fixedBaseFormula: manifest.formula, changedCards: manifest.changedCards, preservedFields: preserved, errors }, null, 2));
const cells = csv(path.join(battles, 'BaselineAfter/summary.csv'));
const oldCells = csv(path.join(root, 'UnityClient/Artifacts/CardSchoolRebalance20261012/BaselineFinal/summary.csv'));
const schools = ['ordinary','fire','arcane','curse','tide','thunder','blood'];
const chosen = all => schools.map(s => all.find(c=>c.school===s && c.rank==='3' && c.size==='10' && c.coreCopies===(s==='ordinary'?'0':'1') && c.target==='training_target_3' && c.tactic==='boarding' && c.supply==='full'));
const display = c => c ? {school:c.school,wins:Number(c.wins),battles:Number(c.battles),turns:c.meanWinTurns||null,powder:c.meanWinPowder||null,balls:c.meanWinCannonballs||null,wood:c.meanWinWood||null,medicine:c.meanWinMedicine||null,total:c.meanWinTotalSupplies||null} : null;
const comparison = schools.map((s,i)=>({school:s,before:display(chosen(oldCells)[i]),after:display(chosen(cells)[i])}));
const decision = {
  status: 'fixed_base_reduction_complete_balance_review_required', packaged: false,
  headline: '固定基础格挡、维修和治疗已统一下调，103 张卡牌实际改变。',
  summary: '原级固定值按 ×0.7 四舍五入、正数最低1，所有强化等级重新从新基础值计算。资源支付与条件产生的附加盾疗保持原规则；不能把本次固定值下调描述成所有实际总效果都降低30%，也不代表六系均衡已经达标。',
  changeManifest: 'base-defense-changes.json', definitionVerification: 'definition-verification.json',
  effectsNotChanged: manifest.exclusions,
  comparisonConditions: 'rank3, size10, coreCopies1 for schools/core0 ordinary, boarding, training_target_3, full supplies, four paired seeds; costs are conditional on winning.',
  comparison, zeroSupplyHighestRank3: completion.zeroSupplyHighestRank3,
  pairedSummary: completion.cohorts,
  limitations: ['配对种子与固定启发式不能证明最优构筑。', '胜局均值必须同时看胜率，失败不计为零耗材胜局。', '咒盾按层数、血契按比例/上限的机制收益不是固定Block/Repair，本轮未再次削减。', '潮汐基础盾下降会减少实际吸收量与可获得潮势。'],
  buildGate: '用户审核后再打包；本轮没有创建发布包。'
};
const validation = {
  status: 'review_ready_not_packaged', packaged:false,
  summary:`逐卡校验872个等级：固定基础项及强化计算正确，伤害/费用/抽牌/机制参数未改变。Core ${counts.passed}/${counts.total} 通过，${counts.failed} 项失败中新增 ${newFailures.length} 项。${unity.summary || 'Unity 定向检查详见验证记录。'}`,
  core: {...counts, failures, newFailures, result:path.relative(out,path.join(core,'results.xml')).replaceAll('\\','/')},
  unity, liveScreens:unity.liveScreens || null,
  benchmark:{battles:completion.totalPersistedTrials,result:path.relative(out,path.join(battles,'ReviewAnalysis/completion.json')).replaceAll('\\','/')}
};
fs.writeFileSync(path.join(out, 'balance-decision.json'), JSON.stringify(decision, null, 2));
fs.writeFileSync(path.join(out, 'validation.json'), JSON.stringify(validation, null, 2));
console.log(JSON.stringify({ counts, newFailures:newFailures.length, definitionRows:after.length, comparison }));
