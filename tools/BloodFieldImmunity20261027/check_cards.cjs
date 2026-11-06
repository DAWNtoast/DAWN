const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../../outputs/blood-field-immunity-20261027');
const target=path.resolve(__dirname,'../../outputs/card-list-by-school-20261023/全部卡牌清单.html');
(async()=>{
  const profile=fs.mkdtempSync(path.join(root,'card-browser-check-'));
  const report={};let context;
  try {
    const options={headless:true,viewport:{width:1350,height:1000},deviceScaleFactor:1};
    if(!fs.existsSync(chromium.executablePath())) options.executablePath=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(p=>fs.existsSync(p));
    context=await chromium.launchPersistentContext(profile,options);
    await context.setOffline(true);
    const page=await context.newPage(),errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(target).href);
    await page.locator('#card-search').fill('免疫血池负面效果影响');
    await page.waitForFunction(()=>document.getElementById('filter-summary').textContent.includes('2 /'));
    const visible=page.locator('tbody.card-block:visible');
    assert.equal(await visible.count(),2);
    for(const [id,name] of [['card_special_plating_fogiron_carapace','雾铁偿伤'],['card_special_plating_living_hull','活舟还生']]){
      const text=await page.locator('tbody[data-card-id="'+id+'"]').innerText();
      assert(text.includes(name));
      assert.equal(text.split('直到下个己方回合开始时，免疫血池负面效果影响').length-1,1);
    }
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    await page.locator('#school-blood').screenshot({path:path.join(root,'两张血契部件牌.png')});
    assert.equal(errors.length,0);
    report.result='PASS';report.visibleCards=2;report.offline=true;report.pageErrors=errors;
  } finally {
    if(context)await context.close();
    const resolved=fs.realpathSync(profile);
    assert(resolved.startsWith(fs.realpathSync(root)+path.sep)&&path.basename(resolved).startsWith('card-browser-check-'));
    function size(p){const s=fs.lstatSync(p);assert(!s.isSymbolicLink());return s.isDirectory()?fs.readdirSync(p).reduce((n,x)=>n+size(path.join(p,x)),0):s.size;}
    report.removedBrowserProfileBytes=size(resolved);
    fs.rmSync(resolved,{recursive:true,force:true});
    report.profileRemoved=!fs.existsSync(resolved);
    fs.writeFileSync(path.join(root,'card-browser-verification.json'),JSON.stringify(report,null,2));
  }
  console.log(JSON.stringify(report,null,2));
})().catch(e=>{console.error(e);process.exitCode=1;});
