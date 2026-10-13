'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..');
const out=path.join(root,'Artifacts/CardRelease20261013');
const build=path.join(root,'Builds/CardBalance20261013');
const project=path.join(root,'tmp/card-release-20261013');
const read=p=>fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'');
const json=p=>JSON.parse(read(p));
const assert=(v,m)=>{if(!v)throw Error(m);};
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex').toUpperCase();
function tests(name){
  const text=read(path.join(out,name+'.xml'));
  const cases=[...text.matchAll(/<test-case\b[^>]*>/g)].map(m=>{
    const attr=k=>m[0].match(new RegExp('\\b'+k+'="([^"]*)"'))?.[1];
    return {name:attr('fullname'),result:attr('result')};
  });
  assert(cases.length>0,'Empty test output: '+name);
  return cases;
}
const initial=tests('EditMode'),supplement=tests('EditModeRendererVerified');
const edit=new Map(initial.map(c=>[c.name,c.result]));
for(const c of supplement){assert(edit.has(c.name),'Supplement contains an uncounted test');edit.set(c.name,c.result);}
assert(edit.size===216&&[...edit.values()].every(r=>r==='Passed'),'Unresolved editor regression');
const play=new Map(tests('PlayModeRendererVerified').map(c=>[c.name,c.result]));
const playSupplement=tests('PlayModeFinal');
for(const c of playSupplement){assert(play.has(c.name),'Unknown supplementary interaction test');play.set(c.name,c.result);}
assert(play.size===10&&[...play.values()].every(r=>r==='Passed'),'Unresolved interaction regression');
const approved=path.join(root,'Artifacts/CardCrewIndependent20261012/EnduranceCoreSnapshot');
const coreNames=fs.readdirSync(approved).filter(n=>n.endsWith('.cs'));
assert(coreNames.length===53,'Unexpected approved Core size');
for(const name of coreNames){
  const expected=hash(path.join(approved,name));
  assert(expected===hash(path.join(project,'Assets/_Project/Core/Runtime',name)),'Build Core differs from approved: '+name);
  assert(expected===hash(path.join(root,'UnityClient/Assets/_Project/Core/Runtime',name)),'Main Core changed after approval: '+name);
}
const report=read(path.join(build,'Windows/build-report.txt'));
assert(/^Result: Succeeded$/m.test(report)&&/^Errors: 0$/m.test(report),'Build did not succeed');
assert(/^Version: 0\.3\.20261013$/m.test(report),'Wrong release version');
const smoke=read(path.join(out,'PlayerSmokeFinal/player-smoke-report.txt'));
assert(smoke.startsWith('PASS\n')||smoke.startsWith('PASS\r\n'),'Packaged player acceptance failed');
const archive=json(path.join(out,'package-verification.json'));
assert(archive.Result==='PASS'&&archive.FilesVerified>0,'ZIP verification did not pass');
const zip=path.join(build,'第七港口-Windows.zip');
assert(hash(zip)===archive.ZipSHA256,'ZIP changed after verification');
const sourceCheck=json(path.join(out,'balance-source-verification.json'));
assert(sourceCheck.status==='passed'&&sourceCheck.allCoreRuntimeHashesMatch,'Independent source verification failed');
const result={status:'released',completedUtc:new Date().toISOString(),version:'0.3.20261013',platform:'Windows x86_64 / Mono',
  executable:path.join(build,'Windows/第七港口.exe'),archive:zip,approvedCoreFiles:53,approvedCoreHashesMatch:true,
  approvedReview:'Artifacts/CardCrewIndependent20261012/index.html',
  editMode:{uniqueCases:216,passed:216,initialPassed:215,initialFailed:1,supplementPassed:27},
  playMode:{uniqueCases:10,passed:10,initialPassed:5,initialFailed:5,afterRendererFixPassed:8,finalSupplementPassed:playSupplement.length},
  releaseFixes:['DeckListNameShadeGraphic explicitly requires CanvasRenderer.','Read-only release acceptance now checks the existing bilingual title artwork instead of the replaced text label.','All 36 authored card-frame resources are checked explicitly, independently of the balanced cards currently using each variant.','Hand-position test permits at most 0.01 logical pixel of floating-point rounding. All card values unchanged.'],
  build:{result:'Succeeded',errors:0,warnings:Number(report.match(/^Warnings: (\d+)$/m)?.[1]),report:'Builds/CardBalance20261013/Windows/build-report.txt'},
  playerSmoke:{passed:true,report:'Artifacts/CardRelease20261013/PlayerSmokeFinal/player-smoke-report.txt',scope:'Actual packaged-player startup, resources, rendering and main-menu interactions; no save writes or full-game completion claim.'},
  package:archive,
  priorValidation:{corePassed:1007,coreTotal:1021,knownFailures:14,newFailures:0,continuousChallenges:2336,continuousBattles:8099},
  limitations:['The prior 14 campaign-simulation failures remain; this is not a claim that every project test passes.','Continuous challenge is an audit harness, not a newly exposed player menu.']};
fs.writeFileSync(path.join(out,'verification-summary.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify(result,null,2));
