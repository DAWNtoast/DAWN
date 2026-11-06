// Validate the published increment and its offline report. The temporary browser
// profile is confined to this task's output directory and removed in finally.
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fixed=path.resolve(__dirname,'../../outputs/battle-icon-tooltips-20261026');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const marker=read(path.join(fixed,'status-revision.json'));
const root=path.resolve(__dirname,'../..',marker.reportDirectory);
assert(root.startsWith(path.resolve(__dirname,'../../outputs')+path.sep),'Report must stay within outputs.');
const data=read(path.join(root,'audit-entries.json'));
const summary=read(path.join(root,'audit-summary.json'));
const input=read(path.join(root,'user-tooltip-overrides.json'));
const report={};
const one=(id,origin='statuses.json')=>{
  const rows=data.filter(e=>e.id===id&&e.origin===origin);assert.equal(rows.length,1,id);return rows[0];
};
assert.equal(data.length,summary.counts.totalRecords);
assert.equal(data.filter(e=>e.origin!=='keywords.json'&&e.availability==='reachable').length,summary.counts.normalIcons);
assert.equal(data.filter(e=>e.origin==='statuses.json'&&e.availability==='reachable').length,summary.counts.normalStatuses);
assert.deepEqual(summary.counts,input.plannedCounts);
assert.deepEqual(one('field_protection_blood').stages,['Naval']);
assert.equal(one('field_protection_blood').side,'player');
assert.equal(one('field_protection_blood').assetId,'immunity');
assert.equal(one('field_protection_blood').value,'1');
assert.deepEqual(one('field_protection_blood').sources.map(s=>s.id).sort(),['card_special_plating_fogiron_carapace','card_special_plating_living_hull']);
assert.equal(input.substantiveChanges,9);
assert.equal(hash(input.input),input.inputSha256);
for(const change of input.changes){
  if(change.id==='resource_curse'){
    const delta=read(path.join(root,'revision-delta.json'));
    assert(delta.userTooltipOverrides.some(x=>x.id==='resource_curse'&&x.userTooltip===change.userTooltip));
    continue;
  }
  assert.equal(one(change.destination).tooltip,change.userTooltip,change.id);
}
for(const id of ['resource_chain','resource_tide','resource_curse','crew_shortage','curse_guard'])
  assert(!data.some(e=>e.id===id),id+' should have been removed');
const resources=data.filter(e=>e.category==='资源 HUD');
assert.equal(resources.length,7);assert(resources.every(e=>!e.hasTooltip));
for(const id of ['pile_draw','pile_discard'])assert.equal(one(id,'controls.json').hasTooltip,false);
assert.equal(one('thunder_chain').assetId,'thunder_chain');
assert.equal(one('tide_power').assetId,'tide');
assert.equal(one('curse_marks').assetId,'curse_mark');
assert.equal(one('curse_marks').tooltip,'死神，来找你了！');
for(const entry of read(path.join(root,'field-tooltip-overrides.json')).entries)
  assert.equal(one(entry.id).tooltip,entry.tooltip);
assert.equal(data.filter(e=>e.id==='retained_guard'&&e.origin==='statuses.json').length,2);
assert.equal(data.filter(e=>e.id==='thunder_ready'&&e.origin==='statuses.json').length,2);
for(const ext of ['html','md'])assert.equal(hash(path.join(root,'战斗图标与悬停提示.'+ext)),hash(path.join(fixed,'战斗图标与悬停提示.'+ext)));
assert.equal(hash(path.join(root,'audit-entries.json')),hash(path.join(fixed,'all-icons.json')));
Object.assign(report,{dataAndUserText:'PASS',counts:summary.counts,userChangesPreserved:9,
  originalUserInputUnchanged:true,resourceIcons:7,resourceAndPileTooltips:false,
  newStateArt:{thunder_chain:'thunder_chain',tide_power:'tide',curse_marks:'curse_mark'},
  fixedOutputsMatch:true});
(async()=>{
  const profile=fs.mkdtempSync(path.join(root,'browser-check-'));let context;
  try{
    const opts={headless:true,viewport:{width:1500,height:1100},deviceScaleFactor:1};
    if(!fs.existsSync(chromium.executablePath()))opts.executablePath=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(p=>fs.existsSync(p));
    context=await chromium.launchPersistentContext(profile,opts);await context.setOffline(true);
    const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(path.join(fixed,'战斗图标与悬停提示.html')).href);
    const visible=()=>page.locator('tbody tr:visible').count();
    assert.equal(await page.locator('tbody tr').count(),summary.counts.totalRecords);assert.equal(await visible(),summary.counts.normalIcons);
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    await page.screenshot({path:path.join(root,'当前图标预览.png')});
    await page.locator('#category').selectOption({label:'资源 HUD'});assert.equal(await visible(),7);
    await page.locator('#tooltip').selectOption('yes');assert.equal(await visible(),0);
    await page.locator('#category').selectOption({label:'牌堆'});assert.equal(await visible(),0);
    await page.locator('#tooltip').selectOption('no');assert.equal(await visible(),2);
    await page.locator('#category').selectOption('');await page.locator('#tooltip').selectOption('');
    await page.locator('#search').fill('field_protection_blood');assert.equal(await visible(),1);
    assert((await page.locator('tbody tr:visible').textContent()).includes('直到下个我方回合开始时，不受到血池场地的负面效果'));
    await page.screenshot({path:path.join(root,'血池防护图标预览.png')});
    await page.locator('#search').fill('');
    for(const id of ['thunder_chain','tide_power','curse_marks']){
      await page.locator('#search').fill(id);
      assert.equal(await visible(),1);
      const row=page.locator('tbody tr:visible');
      assert.equal(await row.getAttribute('data-id'),id);
      const sprite=row.locator('.sprite');assert.equal(await sprite.count(),1);
      const index=['energy','action_point','mana','tide','thunder_chain','curse_mark'].indexOf(one(id).assetId);
      assert.equal(await sprite.evaluate(e=>getComputedStyle(e).backgroundPosition),`${-(index%8)*64}px 0px`);
    }
    await page.locator('#search').fill('');await page.locator('#category').selectOption({label:'通用状态'});
    await page.locator('tr[data-id="thunder_chain"]').scrollIntoViewIfNeeded();
    await page.screenshot({path:path.join(root,'状态栏图标预览.png')});
    await page.locator('#category').selectOption('');await page.locator('#kind').selectOption('keyword');assert.equal(await visible(),21);
    await page.locator('#availability').selectOption('legacy_unreachable');assert.equal(await visible(),2);
    await page.locator('#kind').selectOption('icon');assert.equal(await visible(),15);
    await page.locator('#availability').selectOption('training_only');assert.equal(await visible(),3);
    await page.locator('#availability').selectOption('reachable');
    await page.setViewportSize({width:800,height:1000});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    assert.equal(errors.length,0);
    Object.assign(report,{browser:'PASS',offline:true,filterCounts:'PASS',spriteCells:'PASS',
      wideAndNarrowViewportOverflow:false,pageErrors:errors,result:'PASS'});
  }finally{
    if(context)await context.close();
    const target=fs.realpathSync(profile);
    assert(target.startsWith(root+path.sep)&&path.basename(target).startsWith('browser-check-'));
    function size(p){const s=fs.lstatSync(p);assert(!s.isSymbolicLink());return s.isDirectory()?fs.readdirSync(p).reduce((n,x)=>n+size(path.join(p,x)),0):s.size;}
    report.temporaryBrowserProfileBytesRemoved=size(target);fs.rmSync(target,{recursive:true,force:true});
    report.temporaryBrowserProfileRemoved=!fs.existsSync(target);
    for(const dir of [root,fixed])fs.writeFileSync(path.join(dir,'verification.json'),JSON.stringify(report,null,2));
  }
  console.log(JSON.stringify(report,null,2));
})().catch(e=>{console.error(e);process.exitCode=1;});
