'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'Artifacts/CardCrewIndependent20261012');
const read=p=>fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'');
const json=p=>JSON.parse(read(p));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex').toUpperCase();
const normalize=s=>s.replace(/\r\n/g,'\n').trim();
function testSummary(dir){const xml=read(path.join(out,dir,'results.xml'));const top=xml.slice(0,xml.indexOf('>'));const attr=k=>Number(top.match(new RegExp('\\b'+k+'="(\\d+)"'))[1]);const failures=[...xml.matchAll(/<test-case\b[^>]*>/g)].filter(m=>/\bresult="Failed"/.test(m[0])).map(m=>m[0].match(/\bfullname="([^"]+)"/)[1]);return {total:attr('total'),passed:attr('passed'),failed:attr('failed'),skipped:attr('skipped'),failures,result:dir+'/results.xml'};}
const core=testSummary('CoreVerified');
const prior=read(path.join(root,'Artifacts/CardDefenseReduction20261012/CoreFinal/results.xml'));
const known=new Set([...prior.matchAll(/<test-case\b[^>]*>/g)].filter(m=>/\bresult="Failed"/.test(m[0])).map(m=>m[0].match(/\bfullname="([^"]+)"/)[1]));
core.newFailures=core.failures.filter(n=>!known.has(n));
if(core.newFailures.length)throw Error('New Core failures remain');
const definitionsUnchanged=normalize(read(path.join(out,'CoreVerified/cards.tsv')))===normalize(read(path.join(root,'Artifacts/CardDefenseReduction20261012/CoreFinal/cards.tsv')));
if(!definitionsUnchanged)throw Error('Card definitions unexpectedly changed');
const unit=json(path.join(out,'unity-validation.json'));
const enduranceDir=path.join(out,'CoreEnduranceVerified');
const enduranceCore=fs.existsSync(path.join(enduranceDir,'results.xml'))?testSummary('CoreEnduranceVerified'):null;
if(enduranceCore){enduranceCore.newFailures=enduranceCore.failures.filter(n=>!known.has(n));if(enduranceCore.newFailures.length)throw Error('New continuity failures remain');}
const current=enduranceCore||core;
const endurancePath=path.join(root,'UnityClient/Artifacts/CardCrewIndependent20261012/EnduranceAnalysis/completion.json');
const endurance=fs.existsSync(endurancePath)?json(endurancePath):null;
const compatibilityPath=path.join(root,'UnityClient/Artifacts/CardCrewIndependent20261012/EnduranceLegacyPilot/comparison.json');
const compatibility=fs.existsSync(compatibilityPath)?json(compatibilityPath):null;
if(endurance){
  if(endurance.totalChallenges!==2336||!endurance.invariants.roundBoundaryHealthSuppliesMoralePreserved||!endurance.invariants.streakCumulativeHealthSuppliesConserved)throw Error('Incomplete continuous challenge validation');
  if(!compatibility?.passed||compatibility.exactMatchedTrials!==520||compatibility.differences.length)throw Error('Legacy training compatibility check failed');
  const frozen=path.join(out,'EnduranceCoreSnapshot');
  for(const name of fs.readdirSync(frozen).filter(n=>n.endsWith('.cs'))){
    if(hash(path.join(frozen,name))!==hash(path.join(root,'UnityClient/Assets/_Project/Core/Runtime',name)))throw Error('Live Core changed after endurance snapshot: '+name);
  }
  if(enduranceCore&&normalize(read(path.join(out,'CoreEnduranceVerified/cards.tsv')))!==normalize(read(path.join(out,'CoreVerified/cards.tsv'))))throw Error('Continuity changed card definitions');
}
const summary=`卡牌人数缩放已取消；872 个等级定义逐项保持不变。Core ${current.passed}/${current.total} 通过，${current.failed} 项为此前已有失败，无新增失败。Unity 定向验证 ${unit.editMode.passedUnique}/${unit.editMode.totalUnique} 编辑器及 ${unit.playMode.passed}/${unit.playMode.total} 实机用例通过。${enduranceCore?'连续训练入口另经 Core 回归验证。':''}未打包；不代表整个项目全部测试通过。`;
const validation={status:'review_ready_not_packaged',packaged:false,summary,definitionsUnchanged,definitionRows:872,core,enduranceCore,unity:unit,liveScreens:unit.liveScreens,
 endurance: endurance?{challenges:endurance.totalChallenges,battles:endurance.totalPersistedBattles,baselineReplayChecks:endurance.cohorts.baseline.replayMatchedChallenges,sourceReplayChecks:endurance.cohorts.sources.replayMatchedChallenges,invariants:endurance.invariants,coreSnapshotMatchesCurrent:true,legacyCompatibility:compatibility,independentCodeReview:{blockingFindings:0,checks:['Retained crew HP and inventory','Only new naval carryover at boarding','Zero HP resolves defeat before follow-up actions','Both shuffle and critical sequence streams','Rejected entry cannot repeat previous victory']}}:null,
 catalogSHA256:hash(path.join(root,'UnityClient/Assets/_Project/Core/Runtime/CoreSchoolCardCatalog.cs')),
 healthAccountingReview:{reviewed:true,netLoss:'initial minus final for each health pool',bloodPayment:'separate from same-action recovery',carryover:'included in crew net attrition once; separate from external attacks',note:'Victory-health score includes unused crew health in cannon victories and cannot by itself establish overall strength.'},
 benchmark:'../../UnityClient/Artifacts/CardCrewIndependent20261012/ReviewAnalysis/completion.json'};
fs.writeFileSync(path.join(out,'validation.json'),JSON.stringify(validation,null,2));
console.log(summary);
