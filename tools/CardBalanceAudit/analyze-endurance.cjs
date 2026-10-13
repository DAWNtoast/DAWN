'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {csv,mean,quantile,group}=require('./summarize-dummy-audit.cjs');
const root=path.resolve(__dirname,'../..'),runRoot=path.join(root,'UnityClient/Artifacts/CardCrewIndependent20261012'),out=path.join(runRoot,'EnduranceAnalysis');
const schools=['ordinary','fire','arcane','curse','tide','thunder','blood'];
const textKeys=new Set(['deck','school','tactic','seed','streakSeed','sourceCardId','sourceKind','stopReason','stopTarget','target','outcome','supply','finalStage','recipe','phaseSizes']);
const boolKeys=new Set(['rightCensored','reachedFive','allFiveWins','criticalVictory']);
const read=p=>csv(p).map(r=>Object.fromEntries(Object.entries(r).map(([k,v])=>[k,textKeys.has(k)?v:boolKeys.has(k)?v.toLowerCase()==='true':Number(v)])));
const readJson=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const assert=(v,m)=>{if(!v)throw Error(m);};
const streakKey=r=>[r.deck,r.rank,r.tactic,r.seed].join('/');
const groupKey=r=>[r.deck,r.rank,r.tactic].join('/');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function writeCsv(name,rows){const headers=[...new Set(rows.flatMap(Object.keys))],q=v=>'"'+String(v??'').replaceAll('"','""')+'"';fs.writeFileSync(path.join(out,name),'\uFEFF'+headers.join(',')+'\n'+rows.map(r=>headers.map(h=>q(r[h])).join(',')).join('\n')+'\n');}
function summary(rows){
  const first=rows[0],cleared=rows.map(r=>r.cleared),stops=Object.fromEntries(['loss','turn-watchdog','action-watchdog','round-cap'].map(k=>[k,rows.filter(r=>r.stopReason===k).length]));
  const result={samples:rows.length,meanCleared:mean(cleared),minCleared:Math.min(...cleared),maxCleared:Math.max(...cleared),medianCleared:quantile(cleared,.5),
    totalAttempted:rows.reduce((s,r)=>s+r.attempted,0),meanAttempted:mean(rows.map(r=>r.attempted)),meanStopRound:mean(rows.map(r=>r.stopRound)),
    reachFive:rows.filter(r=>r.reachedFive).length,reachFiveRate:mean(rows.map(r=>r.reachedFive?1:0)),allFiveWins:rows.filter(r=>r.allFiveWins).length,allFiveWinRate:mean(rows.map(r=>r.allFiveWins?1:0)),
    stage5Wins:rows.reduce((s,r)=>s+r.stage5Wins,0),stage5RepeatWins:rows.reduce((s,r)=>s+r.stage5RepeatWins,0),meanStage5RepeatWins:mean(rows.map(r=>r.stage5RepeatWins)),
    stopLoss:stops.loss,stopTurnWatchdog:stops['turn-watchdog'],stopActionWatchdog:stops['action-watchdog'],rightCensored:stops['round-cap'],
    initialHull:first.initialHull,maxHull:first.maxHull,initialCrewHealth:first.initialCrewHealth,maxCrewHealth:first.maxCrewHealth,
    initialPowder:first.initialPowder,initialCannonballs:first.initialCannonballs,initialWood:first.initialWood,initialMedicine:first.initialMedicine};
  for(const m of ['finalHull','finalCrewHealth','hullRemainingFraction','crewRemainingFraction','netHullLoss','netCrewLoss','totalTurns','totalCards','powderUsed','cannonballsUsed','woodUsed','medicineUsed','totalSuppliesUsed','remainingPowder','remainingCannonballs','remainingWood','remainingMedicine','hullBloodPaid','crewBloodPaid','grossHullLoss','grossCrewLoss','effectiveHullRecovered','effectiveCrewRecovered','boardingCarryover','minHull','minCrewHealth','initialMorale','finalMorale'])result['mean_'+m]=mean(rows.map(r=>r[m]));
  for(let target=1;target<=5;target++){result['stopTarget'+target]=rows.filter(r=>r.stopTarget===`training_target_${target}`).length;result['clearedAtLeast'+target]=rows.filter(r=>r.cleared>=target).length;}
  return result;
}
fs.mkdirSync(out,{recursive:true});
const cohorts={},allGroups={},raw={};
for(const [cohort,dir,expected,seeds] of [['baseline','EnduranceBaseline',1216,4],['sources','EnduranceSources',1120,2]]){
  const d=path.join(runRoot,dir),streaks=read(path.join(d,'streaks.csv')),rounds=read(path.join(d,'rounds.csv'));
  assert(streaks.length===expected,`${cohort} challenge count mismatch`);assert(new Set(streaks.map(streakKey)).size===expected,'Duplicate streak');
  const roundGroups=group(rounds,r=>[r.deck,r.rank,r.tactic,r.streakSeed].join('/'));
  assert(roundGroups.size===expected,'Round group count mismatch');
  for(const s of streaks){
    const a=roundGroups.get(streakKey(s)).sort((a,b)=>a.roundNumber-b.roundNumber),f=a[0],l=a.at(-1);
    assert(a.length===s.attempted&&s.cleared===a.filter(r=>r.outcome==='win').length,'Attempt/win mismatch');
    assert(s.initialPowder===75&&s.initialCannonballs===120&&s.initialWood===54&&s.initialMedicine===30,'Wrong initial supplies');
    assert(s.initialHull===f.initialHull&&s.initialCrewHealth===f.initialCrewHealth&&s.finalHull===l.hull&&s.finalCrewHealth===l.crewHealth,'Streak health endpoints mismatch');
    assert(s.netHullLoss===s.grossHullLoss-s.effectiveHullRecovered&&s.netCrewLoss===s.boardingCarryover+s.grossCrewLoss-s.effectiveCrewRecovered,'Streak health conservation mismatch');
    for(const [initial,used,remaining] of [['initialPowder','powderUsed','remainingPowder'],['initialCannonballs','cannonballsUsed','remainingCannonballs'],['initialWood','woodUsed','remainingWood'],['initialMedicine','medicineUsed','remainingMedicine']]){
      assert(s[initial]-s[remaining]===s[used]&&s[used]===a.reduce((n,r)=>n+r[used],0),'Streak supply conservation mismatch');
      assert(s[remaining]===l[remaining],'Final supplies mismatch');
    }
    assert(s.stopRound===l.roundNumber&&s.stopTarget===l.target,'Stop endpoint mismatch');
    assert(s.rightCensored===(s.stopReason==='round-cap'),'Censor flag mismatch');
    if(s.rightCensored)assert(s.cleared===200&&s.attempted===200,'Incorrect cap');else assert(l.outcome===s.stopReason&&s.attempted===s.cleared+1,'Nonwin stop mismatch');
    a.forEach((r,i)=>{
      assert(r.roundNumber===i+1&&r.target===`training_target_${Math.min(i+1,5)}`&&r.seed===r.streakSeed+`::ENDURANCE::ROUND::${String(i+1).padStart(4,'0')}`,'Round sequence mismatch');
      assert(r.netHullLoss===r.initialHull-r.hull&&r.netCrewLoss===r.initialCrewHealth-r.crewHealth,'Round net endpoint mismatch');
      assert(r.netHullLoss===r.grossHullLossIncludingBlood-r.effectiveHullRecovered&&r.netCrewLoss===r.boardingCarryover+r.grossCrewLossExcludingCarryoverIncludingBlood-r.effectiveCrewRecovered,'Round health conservation mismatch');
      if(i){const p=a[i-1];assert(p.outcome==='win'&&r.initialHull===p.hull&&r.initialCrewHealth===p.crewHealth&&r.moraleBefore===p.moraleAfter&&r.initialPowder===p.remainingPowder&&r.initialCannonballs===p.remainingCannonballs&&r.initialWood===p.remainingWood&&r.initialMedicine===p.remainingMedicine,'Round boundary replenished');}
    });
  }
  const groups=[...group(streaks,groupKey).values()].map(rows=>{assert(rows.length===seeds,'Group sample count mismatch');const r=rows[0];return {cohort,deck:r.deck,school:r.school,size:r.size,coreCopies:r.coreCopies,rank:r.rank,tactic:r.tactic,sourceCardId:r.sourceCardId,sourceKind:r.sourceKind,ordinaryCrew:r.ordinaryCrew,...summary(rows)};});
  allGroups[cohort]=groups;raw[cohort]={streaks,rounds};writeCsv(cohort==='baseline'?'group-summary.csv':'source-group-summary.csv',groups);
  const replays=csv(path.join(d,'streak-replays.csv'));assert(replays.every(r=>r.status==='replay-matched'),'Replay failure');
  cohorts[cohort]={directory:path.relative(root,d).replaceAll('\\','/'),challenges:streaks.length,battles:rounds.length,groups:groups.length,seedsPerGroup:seeds,...summary(streaks),
    healthPoolNote:cohort==='sources'?'Crew controls use 21 or22 ordinary crew; maximum differs and is explicit per row.':'All groups use22 ordinary crew.',
    sourceChallenges:streaks.filter(r=>r.sourceCardId).length,controlChallenges:streaks.filter(r=>!r.sourceCardId).length,
    replayMatchedChallenges:replays.length,streakCsvSha256:hash(path.join(d,'streaks.csv')),roundCsvSha256:hash(path.join(d,'rounds.csv')),runMetadata:readJson(path.join(d,'run-metadata.json'))};
}
const bestSort=(a,b)=>b.meanCleared-a.meanCleared||b.minCleared-a.minCleared||b.mean_hullRemainingFraction+b.mean_crewRemainingFraction-a.mean_hullRemainingFraction-a.mean_crewRemainingFraction||a.mean_totalSuppliesUsed-b.mean_totalSuppliesUsed||a.deck.localeCompare(b.deck);
const bestFixedBaseline=[...group(allGroups.baseline,r=>[r.school,r.rank,r.tactic].join('/')).values()].map(a=>a.slice().sort(bestSort)[0]);
writeCsv('best-fixed-baseline.csv',bestFixedBaseline);
const rankSchoolOverview=[...group(raw.baseline.streaks,r=>[r.school,r.rank,r.tactic].join('/')).values()].map(a=>({school:a[0].school,rank:a[0].rank,tactic:a[0].tactic,...summary(a)}));
writeCsv('school-rank-tactic-summary.csv',rankSchoolOverview);
const sourceControls=new Map(raw.sources.streaks.filter(r=>!r.sourceCardId).map(r=>[[r.school,r.ordinaryCrew,r.rank,r.tactic,r.seed].join('/'),r]));
const sourcePairs=raw.sources.streaks.filter(r=>r.sourceCardId).map(r=>{const c=sourceControls.get([r.school,r.ordinaryCrew,r.rank,r.tactic,r.seed].join('/'));assert(c,'Missing source control');return {sourceCardId:r.sourceCardId,sourceKind:r.sourceKind,school:r.school,ordinaryCrew:r.ordinaryCrew,rank:r.rank,tactic:r.tactic,seed:r.seed,controlDeck:c.deck,controlCleared:c.cleared,cleared:r.cleared,clearedDelta:r.cleared-c.cleared,controlStopReason:c.stopReason,stopReason:r.stopReason,controlFinalHull:c.finalHull,finalHull:r.finalHull,controlFinalCrewHealth:c.finalCrewHealth,finalCrewHealth:r.finalCrewHealth,controlSuppliesUsed:c.totalSuppliesUsed,totalSuppliesUsed:r.totalSuppliesUsed};});
writeCsv('source-control-pairs.csv',sourcePairs);
const sourceSensitivity=[...group(sourcePairs,r=>[r.sourceCardId,r.rank,r.tactic].join('/')).values()].map(a=>({sourceCardId:a[0].sourceCardId,sourceKind:a[0].sourceKind,school:a[0].school,ordinaryCrew:a[0].ordinaryCrew,rank:a[0].rank,tactic:a[0].tactic,samples:a.length,meanControlCleared:mean(a.map(r=>r.controlCleared)),meanCleared:mean(a.map(r=>r.cleared)),meanClearedDelta:mean(a.map(r=>r.clearedDelta)),improvedSeeds:a.filter(r=>r.clearedDelta>0).length,worsenedSeeds:a.filter(r=>r.clearedDelta<0).length}));
writeCsv('source-sensitivity.csv',sourceSensitivity);
const sizeStrata=[...group(raw.baseline.streaks,r=>[r.school,r.size,r.coreCopies,r.rank,r.tactic].join('/')).values()].map(a=>({school:a[0].school,size:a[0].size,coreCopies:a[0].coreCopies,rank:a[0].rank,tactic:a[0].tactic,...summary(a)}));
const completion={status:'completed',scope:'Real Core continuous challenge: I, II, III, IV, V, then V repeatedly until defeat/unresolved or a200-bout right-censor limit.',
  rules:{supplies:{powder:75,cannonballs:120,wood:54,medicine:30},betweenBattles:'No rewards, refill or free healing. Exact previous hull, crew health, supplies and morale carry. Only card-play recovery is allowed.',
    reset:'Real Core battle entry resets energy, hands/piles, temporary modifiers and school resources.',
    seeds:'Four baseline/two source base seeds; unique deterministic ::ENDURANCE::ROUND::NNNN key drives each round shuffle and critical stream.',
    boarding:'Each round starts with previous crew health; only this round naval hull attrition adds boarding-entry injury, once. Crew reaching0 at entry is a loss.',
    challengeCount:'cleared counts victories only; attempted includes the final failed/unresolved battle. ReachFive means entered round5; allFiveWins means won it. stage5RepeatWins counts victories after round5.',
    limits:{turnsPerBattle:120,actionsPerCaptainTurn:64,roundsPerChallenge:200},
    censorship:'round-cap means at least200 victories, never a defeat or a proven infinite streak; turn/action watchdog remains unresolved.',
    portFixture:'Tripled provisions occupy215.9 volume including20 water/20 food, above patrol capacity150. User-authorized fixed training starts through the real Port API, without sailing or changing ship/capacity rules.',
    morale:'Carried naturally through the same session, recorded every round; it can affect actual critical chances.'},
  cohorts,totalChallenges:cohorts.baseline.challenges+cohorts.sources.challenges,totalPersistedBattles:cohorts.baseline.battles+cohorts.sources.battles,
  groups:allGroups.baseline,sourceGroups:allGroups.sources,bestFixedBaseline,rankSchoolOverview,sourceSensitivity,
  bestSelection:'Choose one fixed deck per school/rank/tactic using mean cleared across all seeds, then minimum cleared, final combined health fractions, lower materials and ID. It is an in-sample descriptive choice, not an independent holdout or cross-seed best-case collage.',
  limitations:['Fixed heuristic and finite recipe menu; no claim of optimal deck/player performance.','Four/two seeds are a bounded sample.','Ordinary size20 cannot satisfy legal base-card ownership; its infeasible recipe is retained in manifests, not padded.','Do not compare unweighted school-wide means with different feasible recipe counts; use fixed rank/tactic/size rows or best-fixed sets.','End-of-run health and supplies may include the losing attempt; this is intentional and separate from cleared count.','Source additions are one-card sensitivities with matched21/22-crew controls, not combinations of multiple sources.'],
  artifacts:{baselineGroups:'group-summary.csv',sourcesGroups:'source-group-summary.csv',bestFixed:'best-fixed-baseline.csv',sourceControls:'source-control-pairs.csv',sourceSensitivity:'source-sensitivity.csv'},
  invariants:{uniqueChallenges:2336,roundHealthConservation:true,roundBoundaryHealthSuppliesMoralePreserved:true,streakCumulativeHealthSuppliesConserved:true,priorSingleBattleOutputsPreserved:true}};
for(const name of ['core-source-sha256.txt','benchmark-source-sha256.txt'])assert(fs.readFileSync(path.join(runRoot,'EnduranceBaseline',name),'utf8')===fs.readFileSync(path.join(runRoot,'EnduranceSources',name),'utf8'),'Cohort snapshot mismatch');
fs.writeFileSync(path.join(out,'analysis-source-sha256.txt'),hash(__filename)+'  tools/CardBalanceAudit/analyze-endurance.cjs\n');
fs.writeFileSync(path.join(out,'completion.json'),JSON.stringify(completion,null,2));
console.log(JSON.stringify({cohorts,totalChallenges:completion.totalChallenges,totalPersistedBattles:completion.totalPersistedBattles,bestFixedBaseline},null,2));
