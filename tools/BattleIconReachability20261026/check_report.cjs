const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../../outputs/battle-icon-reachability-20261026');
const fixed=path.resolve(__dirname,'../../outputs/battle-icon-tooltips-20261026');
(async()=>{
  const data=JSON.parse(fs.readFileSync(path.join(root,'audit-entries.json'),'utf8'));
  const normal=data.filter(e=>e.availability==='reachable'&&e.origin!=='keywords.json');
  const profile=fs.mkdtempSync(path.join(root,'browser-check-'));
  const report={}; let context;
  try {
    const opts={headless:true,viewport:{width:1500,height:1100},deviceScaleFactor:1};
    if(!fs.existsSync(chromium.executablePath()))opts.executablePath=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(p=>fs.existsSync(p));
    context=await chromium.launchPersistentContext(profile,opts);await context.setOffline(true);
    const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(path.join(fixed,'战斗图标与悬停提示.html')).href);
    await page.waitForFunction(()=>document.getElementById('count').textContent.includes('/'));
    const visible=()=>page.locator('tbody tr:visible').count();
    assert.equal(await page.locator('tbody tr').count(),data.length);
    report.defaultNormalIcons=await visible();assert.equal(report.defaultNormalIcons,normal.length);
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    await page.screenshot({path:path.join(root,'当前图标预览.png')});
    await page.locator('#kind').selectOption('keyword');
    report.normalKeywords=await visible();assert.equal(report.normalKeywords,21);
    await page.locator('#availability').selectOption('legacy_unreachable');
    report.inactiveKeywords=await visible();assert.equal(report.inactiveKeywords,2);
    await page.locator('#kind').selectOption('icon');
    report.inactiveIcons=await visible();assert.equal(report.inactiveIcons,15);
    await page.screenshot({path:path.join(root,'旧图标筛选预览.png')});
    await page.locator('#availability').selectOption('training_only');
    report.trainingIcons=await visible();assert.equal(report.trainingIcons,3);
    await page.locator('#availability').selectOption('reachable');
    await page.locator('#category').selectOption({label:'资源 HUD'});assert.equal(await visible(),10);
    await page.locator('#category').selectOption('');await page.locator('#tooltip').selectOption('no');
    report.normalWithoutTooltip=await visible();assert.equal(report.normalWithoutTooltip,normal.filter(e=>!e.hasTooltip).length);
    await page.locator('#tooltip').selectOption('');await page.locator('#search').fill('咒印');
    report.searchMatches=await visible();assert(report.searchMatches>0);
    assert(await page.locator('tbody tr:visible').evaluateAll(rows=>rows.every(r=>r.textContent.includes('咒印'))));
    await page.locator('#search').fill('');await page.setViewportSize({width:800,height:1000});
    report.narrowViewportOverflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);assert.equal(report.narrowViewportOverflow,false);
    report.pageErrors=errors;assert.equal(errors.length,0);report.offline=true;report.result='PASS';
  } finally {
    if(context)await context.close();
    const target=fs.realpathSync(profile);assert(target.startsWith(root+path.sep)&&path.basename(target).startsWith('browser-check-'));
    function size(p){const s=fs.lstatSync(p);assert(!s.isSymbolicLink());return s.isDirectory()?fs.readdirSync(p).reduce((n,x)=>n+size(path.join(p,x)),0):s.size;}
    report.temporaryBrowserProfileBytesRemoved=size(target);fs.rmSync(target,{recursive:true,force:true});
    report.temporaryBrowserProfileRemoved=!fs.existsSync(target);
    for(const out of [root,fixed])fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify(report,null,2));
  }
  console.log(JSON.stringify(report,null,2));
})().catch(e=>{console.error(e);process.exitCode=1;});
