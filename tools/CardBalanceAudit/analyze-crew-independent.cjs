'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { csv, mean, group } = require('./summarize-dummy-audit.cjs');
const root = path.resolve(__dirname, '../..');
const runRoot = path.join(root, 'UnityClient/Artifacts/CardCrewIndependent20261012');
const priorRoot = path.join(root, 'UnityClient/Artifacts/CardDefenseReduction20261012');
const out = path.join(runRoot, 'ReviewAnalysis');
const schools = ['ordinary','fire','arcane','curse','tide','thunder','blood'];
const files = [['baseline','BaselineAfter',36480],['sources','SourcesAfter',11200]];
const key = r => [r.deck,r.rank,r.tactic,r.supply,r.target,r.seed].join('/');
const groupKey = r => [r.deck,r.rank,r.tactic,r.supply,r.target].join('/');
const numbers = ['size','coreCopies','rank','turns','cards','powderUsed','cannonballsUsed','woodUsed','medicineUsed','totalSuppliesUsed','hull','crewHealth','hullDamageTaken','crewDamageTaken','boardingCarryover','hullHealed','crewHealed','ordinaryCrew','initialHull','maxHull','initialCrewHealth','maxCrewHealth','netHullLoss','netCrewLoss','netHullLossFraction','netCrewLossFraction','hullRemainingFraction','crewRemainingFraction','hullBloodPaid','crewBloodPaid','grossHullLossIncludingBlood','grossCrewLossExcludingCarryoverIncludingBlood','effectiveHullRecovered','effectiveCrewRecovered','minHullFraction','minCrewFraction','victoryHealthScore'];
const metrics = ['turns','totalSuppliesUsed','powderUsed','cannonballsUsed','woodUsed','medicineUsed','netHullLoss','netCrewLoss','netHullLossFraction','netCrewLossFraction','hullRemainingFraction','crewRemainingFraction','boardingCarryover'];
const read = p => csv(p).map(r => Object.fromEntries(Object.entries(r).map(([k,v])=>[k,numbers.includes(k)&&v!==''?Number(v):v])));
const json = p => JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function writeCsv(name,rows) {
  if (!rows.length) {fs.writeFileSync(path.join(out,name),'');return;}
  const headers=[...new Set(rows.flatMap(Object.keys))], quote=v=>'"'+String(v??'').replaceAll('"','""')+'"';
  fs.writeFileSync(path.join(out,name),'\uFEFF'+headers.join(',')+'\n'+rows.map(r=>headers.map(h=>quote(r[h])).join(',')).join('\n')+'\n');
}
function check(condition,message) {if(!condition)throw Error(message);}
function enrichOld(r,current) {
  // The old action-boundary sums do not expose every blood payment, but their
  // signed difference must still equal final minus initial health exactly.
  const initialHull=r.hull+r.hullDamageTaken-r.hullHealed;
  const initialCrew=r.crewHealth+r.crewDamageTaken+r.boardingCarryover-r.crewHealed;
  check(initialHull===current.initialHull&&initialHull===current.maxHull,`Old hull/max reconstruction mismatch ${key(r)}`);
  check(initialCrew===current.initialCrewHealth&&initialCrew===current.maxCrewHealth,`Old crew/max reconstruction mismatch ${key(r)}`);
  Object.assign(r,{initialHull,maxHull:current.maxHull,initialCrewHealth:initialCrew,maxCrewHealth:current.maxCrewHealth,
    netHullLoss:initialHull-r.hull,netCrewLoss:initialCrew-r.crewHealth,
    netHullLossFraction:(initialHull-r.hull)/current.maxHull,netCrewLossFraction:(initialCrew-r.crewHealth)/current.maxCrewHealth,
    hullRemainingFraction:r.hull/current.maxHull,crewRemainingFraction:r.crewHealth/current.maxCrewHealth});
  r.victoryHealthScore=r.outcome==='win'?50*(r.hullRemainingFraction+r.crewRemainingFraction):0;
  return r;
}
function stats(rows) {
  const wins=rows.filter(r=>r.outcome==='win');
  const result={trials:rows.length,battles:rows.length,wins:wins.length,losses:rows.filter(r=>r.outcome==='loss').length,
    turnWatchdogs:rows.filter(r=>r.outcome==='turn-watchdog').length,actionWatchdogs:rows.filter(r=>r.outcome==='action-watchdog').length,
    winRate:wins.length/rows.length,victoryHealthScore:mean(rows.map(r=>r.victoryHealthScore)),
    criticalWins:wins.filter(r=>r.hullRemainingFraction<=.25||r.crewRemainingFraction<=.25).length,
    criticalWinRate:mean(wins.map(r=>r.hullRemainingFraction<=.25||r.crewRemainingFraction<=.25?1:0))};
  for(const m of [...metrics,'hullBloodPaid','crewBloodPaid','grossHullLossIncludingBlood','grossCrewLossExcludingCarryoverIncludingBlood','effectiveHullRecovered','effectiveCrewRecovered','minHullFraction','minCrewFraction'])
    result['meanWin_'+m]=wins.length&&wins.every(r=>Number.isFinite(r[m]))?mean(wins.map(r=>r[m])):null;
  result.meanWinTurns=result.meanWin_turns;
  return result;
}
function paired(before,after) {
  const byKey=new Map(before.map(r=>[key(r),r]));
  check(byKey.size===before.length&&new Set(after.map(key)).size===after.length&&before.length===after.length,'Pair count/uniqueness mismatch');
  const pairs=after.map(r=>{check(byKey.has(key(r)),'Missing old pair '+key(r));return [byKey.get(key(r)),r];});
  const common=pairs.filter(([b,a])=>b.outcome==='win'&&a.outcome==='win');
  const b=stats(before),a=stats(after);
  const result={trials:after.length,beforeWins:b.wins,afterWins:a.wins,lostWins:pairs.filter(([b,a])=>b.outcome==='win'&&a.outcome!=='win').length,
    gainedWins:pairs.filter(([b,a])=>b.outcome!=='win'&&a.outcome==='win').length,commonWins:common.length,
    winRateDelta:a.winRate-b.winRate,beforeVictoryHealthScore:b.victoryHealthScore,afterVictoryHealthScore:a.victoryHealthScore,
    beforeTurnWatchdogs:b.turnWatchdogs,afterTurnWatchdogs:a.turnWatchdogs,beforeActionWatchdogs:b.actionWatchdogs,afterActionWatchdogs:a.actionWatchdogs};
  for(const m of metrics) {
    result['beforeMeanWin_'+m]=b['meanWin_'+m];result['afterMeanWin_'+m]=a['meanWin_'+m];
    result['beforeCommonWin_'+m]=mean(common.map(p=>p[0][m])); result['afterCommonWin_'+m]=mean(common.map(p=>p[1][m]));
    result['commonWinDelta_'+m]=mean(common.map(p=>p[1][m]-p[0][m]));
  }
  return result;
}
function strongest(rows) {
  return schools.map(s=>{
    const wins=rows.filter(r=>r.school===s&&r.rank===3&&r.supply==='none'&&r.outcome==='win');
    const level=wins.length?Math.max(...wins.map(r=>Number(r.target.split('_').at(-1)))):0;
    const highest=rows.filter(r=>r.school===s&&r.rank===3&&r.supply==='none'&&r.target===`training_target_${level}`);
    return {school:s,highestTarget:level,highestTargetWins:highest.filter(r=>r.outcome==='win').length,highestTargetTrials:highest.length,...(highest.length?stats(highest):{})};
  });
}
fs.mkdirSync(out,{recursive:true});
const cohorts={}, pairedGroups=[], trialPairs=[], runs=[], retained={};
for(const [cohort,dir,expected] of files) {
  const beforeFile=path.join(priorRoot,dir,'battles.csv'),afterFile=path.join(runRoot,dir,'battles.csv');
  const after=read(afterFile),current=new Map(after.map(r=>[key(r),r]));
  check(after.length===expected,`${cohort} persisted count ${after.length} != ${expected}`);
  for(const r of after) {
    check(r.netHullLoss===r.initialHull-r.hull&&r.netCrewLoss===r.initialCrewHealth-r.crewHealth,'Final net loss mismatch');
    check(r.netHullLoss===r.grossHullLossIncludingBlood-r.effectiveHullRecovered,'Hull conservation mismatch');
    check(r.netCrewLoss===r.boardingCarryover+r.grossCrewLossExcludingCarryoverIncludingBlood-r.effectiveCrewRecovered,'Crew conservation mismatch');
    check(r.hull>=0&&r.hull<=r.maxHull&&r.crewHealth>=0&&r.crewHealth<=r.maxCrewHealth,'Invalid final health');
    check(Math.abs(r.victoryHealthScore-(r.outcome==='win'?50*(r.hull/r.maxHull+r.crewHealth/r.maxCrewHealth):0))<1e-9,'Victory score mismatch');
  }
  const before=read(beforeFile).map(r=>{check(current.has(key(r)),'Missing new pair '+key(r));return enrichOld(r,current.get(key(r)));});
  retained[cohort]={before,after};
  cohorts[cohort]={before:stats(before),after:stats(after),paired:paired(before,after)};
  const oldGroups=group(before,groupKey);
  for(const [k,g] of group(after,groupKey)) {
    const r=g[0];pairedGroups.push({cohort,deck:r.deck,school:r.school,size:r.size,coreCopies:r.coreCopies,rank:r.rank,tactic:r.tactic,supply:r.supply,target:r.target,...paired(oldGroups.get(k),g)});
  }
  const oldIndex=new Map(before.map(r=>[key(r),r]));
  for(const a of after) {
    const b=oldIndex.get(key(a)),both=b.outcome==='win'&&a.outcome==='win';
    trialPairs.push({cohort,deck:a.deck,school:a.school,size:a.size,coreCopies:a.coreCopies,rank:a.rank,tactic:a.tactic,supply:a.supply,target:a.target,seed:a.seed,
      beforeOutcome:b.outcome,afterOutcome:a.outcome,beforeVictoryHealthScore:b.victoryHealthScore,afterVictoryHealthScore:a.victoryHealthScore,
      beforeHull:b.hull,afterHull:a.hull,beforeCrewHealth:b.crewHealth,afterCrewHealth:a.crewHealth,
      ...Object.fromEntries(metrics.flatMap(m=>[[`before_${m}`,b[m]],[`after_${m}`,a[m]],[`commonWinDelta_${m}`,both?a[m]-b[m]:null]]))});
  }
  const traces=csv(path.join(runRoot,dir,'trace-cases.csv'));
  check(traces.every(r=>r.status==='replay-matched'),'Unmatched replay');
  runs.push({cohort,directory:path.relative(root,path.join(runRoot,dir)).replaceAll('\\','/'),persistedTrials:after.length,oldHealthReconstructionsVerified:before.length,
    replayMatchedTraces:traces.length,beforeCsvSha256:hash(beforeFile),afterCsvSha256:hash(afterFile),runMetadata:json(path.join(runRoot,dir,'run-metadata.json'))});
}
writeCsv('paired-group-summary.csv',pairedGroups);writeCsv('paired-trial-comparison.csv',trialPairs);
writeCsv('lost-wins.csv',trialPairs.filter(r=>r.beforeOutcome==='win'&&r.afterOutcome!=='win'));
writeCsv('gained-wins.csv',trialPairs.filter(r=>r.beforeOutcome!=='win'&&r.afterOutcome==='win'));
const baseline=retained.baseline.after;
const comparison=[];
for(const size of [10,15])for(const rank of [0,1,2,3])for(const tactic of ['cannon','boarding'])for(const target of [1,2,3,4,5])for(const supply of ['full','none']) {
  const select=(rows,s)=>rows.filter(r=>r.school===s&&r.size===size&&r.coreCopies===(s==='ordinary'?0:1)&&r.rank===rank&&r.tactic===tactic&&r.target===`training_target_${target}`&&r.supply===supply);
  const ordinary=stats(select(baseline,'ordinary'));
  for(const school of schools) {
    const b=select(retained.baseline.before,school),a=select(baseline,school);
    check(a.length===4,`Unmatched comparison sample ${school}/${size}/${rank}/${tactic}/${target}/${supply}`);
    const st=stats(a);
    comparison.push({size,coreCopies:school==='ordinary'?0:1,school,rank,tactic,target:`training_target_${target}`,supply,...st,
      ordinaryVictoryHealthScore:ordinary.victoryHealthScore,relativeVictoryHealthScore:ordinary.victoryHealthScore>0?100*st.victoryHealthScore/ordinary.victoryHealthScore:null,
      denominatorStable:ordinary.victoryHealthScore>=20&&ordinary.wins>=2,
      beforeWins:stats(b).wins,beforeVictoryHealthScore:stats(b).victoryHealthScore,...paired(b,a)});
  }
}
writeCsv('matched-health-comparison.csv',comparison);
const featured=comparison.filter(r=>r.size===10&&r.rank===3&&r.supply==='full'&&['training_target_3','training_target_4'].includes(r.target));
writeCsv('rank3-size10-target3-4.csv',featured);
const strata=[];
for(const [cohort,{after}] of Object.entries(retained))for(const [k,rows] of group(after,r=>[r.school,r.size,r.coreCopies,r.rank,r.tactic,r.supply].join('/'))) {
  const r=rows[0];strata.push({cohort,school:r.school,size:r.size,coreCopies:r.coreCopies,rank:r.rank,tactic:r.tactic,supply:r.supply,...stats(rows)});
}
writeCsv('size-rank-tactic-strata.csv',strata);
const sourceRows=retained.sources.after;
const sourceControlKey=r=>[r.school,r.ordinaryCrew,r.rank,r.tactic,r.supply,r.target,r.seed].join('/');
const sourceControls=new Map(sourceRows.filter(r=>!r.sourceCardId).map(r=>[sourceControlKey(r),r]));
writeCsv('source-health-sensitivity.csv',sourceRows.filter(r=>r.sourceCardId).map(r=>{
  const c=sourceControls.get(sourceControlKey(r));check(c,'Missing source control');const both=r.outcome==='win'&&c.outcome==='win';
  return {sourceCardId:r.sourceCardId,sourceKind:r.sourceKind,school:r.school,ordinaryCrew:r.ordinaryCrew,rank:r.rank,tactic:r.tactic,supply:r.supply,target:r.target,seed:r.seed,
    controlOutcome:c.outcome,outcome:r.outcome,controlVictoryHealthScore:c.victoryHealthScore,victoryHealthScore:r.victoryHealthScore,victoryHealthScoreDelta:r.victoryHealthScore-c.victoryHealthScore,
    ...Object.fromEntries(metrics.map(m=>['commonWinDelta_'+m,both?r[m]-c[m]:null]))};
}));
// Each school gets the same 2 sizes * 4 ranks * 2 tactics * 5 targets * 4 seeds.
// Ordinary size20 is illegal and never enters this descriptive ranking.
const matchedOverview=schools.map(s=>{
  const select=rows=>rows.filter(r=>r.school===s&&[10,15].includes(r.size)&&r.coreCopies===(s==='ordinary'?0:1)&&r.supply==='full');
  const b=select(retained.baseline.before),a=select(baseline);return {school:s,...stats(a),before:stats(b),paired:paired(b,a)};
});
const ordinaryOverview=matchedOverview.find(r=>r.school==='ordinary');
for(const r of matchedOverview)r.relativeVictoryHealthScore=100*r.victoryHealthScore/ordinaryOverview.victoryHealthScore;
writeCsv('matched-full-supply-overview.csv',matchedOverview.map(({before,paired,...r})=>r));
const matchedRankTacticOverview=[];
for(const rank of [0,1,2,3])for(const tactic of ['cannon','boarding']) {
  const select=(rows,s)=>rows.filter(r=>r.school===s&&[10,15].includes(r.size)&&r.coreCopies===(s==='ordinary'?0:1)&&r.supply==='full'&&r.rank===rank&&r.tactic===tactic);
  const ordinary=stats(select(baseline,'ordinary'));
  for(const school of schools) {
    const b=select(retained.baseline.before,school),a=select(baseline,school),st=stats(a);
    matchedRankTacticOverview.push({school,rank,tactic,...st,relativeVictoryHealthScore:ordinary.victoryHealthScore>0?100*st.victoryHealthScore/ordinary.victoryHealthScore:null,before:stats(b),paired:paired(b,a)});
  }
}
writeCsv('matched-rank-tactic-overview.csv',matchedRankTacticOverview.map(({before,paired,...r})=>r));
const coverageMap=new Map();
for(const [,dir] of files)for(const c of csv(path.join(runRoot,dir,'card-coverage.csv'))) {
  if(!coverageMap.has(c.cardId))coverageMap.set(c.cardId,{cardId:c.cardId,school:c.school||'ordinary',isSource:c.isSource==='True',seen:0,plays:0});
  const item=coverageMap.get(c.cardId);item.seen+=Number(c.seen);item.plays+=Number(c.plays);
}
const activeIds=new Set(files.flatMap(([,dir])=>csv(path.join(runRoot,dir,'deck-manifest.csv')).flatMap(r=>[...r.cards.split(';'),r.sourceCardId].filter(Boolean))));
const rawCoverage=[...coverageMap.values()];
const coverage=rawCoverage.filter(c=>activeIds.has(c.cardId));
writeCsv('raw-library-card-coverage.csv',rawCoverage);writeCsv('combined-card-coverage.csv',coverage);
const coverageBySchool=schools.map(s=>{const c=coverage.filter(c=>c.school===s);return {school:s,total:c.length,played:c.filter(c=>c.plays>0).length,basic:c.filter(c=>!c.isSource).length,basicPlayed:c.filter(c=>!c.isSource&&c.plays>0).length,sources:c.filter(c=>c.isSource).length,sourcesPlayed:c.filter(c=>c.isSource&&c.plays>0).length};});
const completion={status:'completed',scope:'Crew-independent fixed card effects; health-aware paired single-battle endurance assessment.',
  counting:'47,680 new persisted bouts; previous 47,680 paired controls reused and not counted as new trials.',
  costDefinition:'Unweighted sum of actual powder+cannonballs+wood+medicine inventory deductions; no health-to-item conversion.',
  healthDefinition:{netHullLoss:'initialHull - finalHull',netCrewLoss:'initialCrew - finalCrew; includes naval carryover exactly once',
    normalizedLoss:'each pool divided by its own maximum; raw hull HP and crew HP are not interchangeable',
    criticalVictory:'A victory with either final health pool <=25% of its own maximum',
    victoryHealthScore:'100 * mean(win ? (finalHull/maxHull + finalCrew/maxCrew)/2 : 0). Defeat and unresolved=0.',
    relativeScore:'ordinary=100 only under identical size/rank/tactic/target/supply/seed conditions; ordinary score zero => NA. denominatorStable requires ordinary score >=20 and >=2 wins/4.',
    blood:'Successful BloodRisk/BloodWard threshold recorded before same-action recovery; gross loss and effective healing separately conserve each pool.',
    carryover:'Naval hull loss can reduce boarding entry health. This is retained crew attrition, not a second independent attack; never add it to external damage.',
    oldControls:'Old signed action-boundary sums reconstruct initial/max HP and are checked against each new matched fixture; old blood payment/minimums cannot be reconstructed.'},
  continuousMode:{tested:false,reason:'StartTrainingBattle recreates full crew HP and boarding entry derives from maximum again; chaining it would replenish crew. This report measures single-battle end health for future endurance design, not a no-healing streak.'},
  limitations:['Fixed deterministic greedy policy, not exhaustive construction or optimal play.','Four baseline and two source seeds per exact configuration.','Winning material and attrition averages condition on wins; paired common-win comparisons avoid survivor mixing.','No-win cells are blank, never free victories; turn120 outcomes remain unresolved.','Aggregate all-deck school rankings are invalid because ordinary size20 is infeasible; matched overview uses only size10/15 and school core1.','Victory-health score is a transparent reference, not an objective total strength, and does not prove the exact +30% target.','Card grades, source variety, phases and equipment remain bounded by the fixed audit fixtures.'],
  cohorts,runs,totalNewPersistedTrials:47680,totalPersistedTrials:47680,matchedFullSupplyOverview:matchedOverview,matchedRankTacticOverview,
  featured,zeroSupplyHighestRank3:strongest(baseline),sourceZeroHighestRank3:strongest(retained.sources.after),
  coverage:{total:coverage.length,played:coverage.filter(c=>c.plays>0).length,unplayed:coverage.filter(c=>c.plays===0).map(c=>c.cardId),
    rawLibrarySlots:rawCoverage.length,excludedLegacyAliasesOrRetiredSlots:rawCoverage.filter(c=>!activeIds.has(c.cardId)).map(c=>c.cardId),denominator:'Distinct real deck/source IDs in the current audit manifests; excludes legacy aliases and retired library slots.'},coverageBySchool,
  invariants:{healthConservationRows:47680,oldInitialMaxReconstructions:47680,uniquePairedRows:47680,immutablePriorResults:true},artifacts:{paired:'paired-group-summary.csv',matched:'matched-health-comparison.csv',strata:'size-rank-tactic-strata.csv',featured:'rank3-size10-target3-4.csv'}};
for(const name of ['core-source-sha256.txt','benchmark-source-sha256.txt'])
  check(fs.readFileSync(path.join(runRoot,'BaselineAfter',name),'utf8')===fs.readFileSync(path.join(runRoot,'SourcesAfter',name),'utf8'),'Different cohort snapshot: '+name);
fs.writeFileSync(path.join(out,'analysis-source-sha256.txt'),hash(__filename)+'  tools/CardBalanceAudit/analyze-crew-independent.cjs\n');
fs.writeFileSync(path.join(out,'completion.json'),JSON.stringify(completion,null,2));
console.log(JSON.stringify({counts:runs.map(r=>({cohort:r.cohort,trials:r.persistedTrials,traces:r.replayMatchedTraces})),cohorts,matchedFullSupplyOverview:matchedOverview,featured,coverage:completion.coverage},null,2));
