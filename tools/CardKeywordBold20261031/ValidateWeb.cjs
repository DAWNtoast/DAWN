// Task-scoped integration check. Uses a fresh browser context, never the user's profile.
const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root='D:/Game/outputs/card-list-by-school-20261023';
const task='D:/Game/outputs/card-keyword-bold-20261031';
const out=path.join(task,'validation');
const backup=path.join(task,'input-backup/outputs/card-list-by-school-20261023');
const read=file=>JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
const htmlPath=path.join(root,'全部卡牌清单.html');
const report={status:'running',userBrowserProfileAccessed:false,checks:[]};
fs.mkdirSync(out,{recursive:true});
async function run(){
 let browser;
 try{
  const base=read(path.join(root,'card-list-data.json'));
  const upgrades=read(path.join(root,'card-upgrade-data.json'));
  const beforeBase=read(path.join(backup,'card-list-data.json'));
  const beforeUpgrades=read(path.join(backup,'card-upgrade-data.json'));
  const plainRows=data=>data.Cards.flatMap(c=>(c.Stages||c.StageVariants).flatMap(s=>(s.Levels||[s]).map(r=>({
   id:c.Id,stage:s.Stage||s.Stages.join('|'),level:r.Level||0,name:r.Name,energy:r.Energy,cost:r.OtherCost,effect:r.Effect,keywords:r.Keywords
  }))));
  assert.deepEqual(plainRows(base),plainRows(beforeBase),'Base definitions changed');
  assert.deepEqual(plainRows(upgrades),plainRows(beforeUpgrades),'Upgrade definitions changed');
  const allRows=base.Cards.flatMap(c=>c.StageVariants).concat(upgrades.Cards.flatMap(c=>c.Stages.flatMap(s=>s.Levels)));
  for(const row of allRows){
   assert.equal(typeof row.EffectRichText,'string','Missing runtime rich text');
   assert.equal(row.EffectRichText.replace(/<\/?b>/g,''),row.Effect,'Markup changed plain text');
   assert.ok(!/<b>[^<]*范围[^<]*<\/b>/.test(row.EffectRichText),'Range marked bold');
  }
  report.dataRowsChecked=allRows.length;
  report.checks.push('Base/upgrade values, effects and keywords match the input backup; all rich text preserves the exact plain wording.');
  const reviewJs=fs.readFileSync('D:/Game/tools/CardListBySchool20261023/CardReview.js','utf8');
  const html=fs.readFileSync(htmlPath,'utf8');
  const beforeHtml=fs.readFileSync(path.join(backup,'全部卡牌清单.html'),'utf8');
  assert.ok(html.includes(reviewJs)&&beforeHtml.includes(reviewJs),'Review script changed');
  const expected=new Map();
  for(const card of upgrades.Cards)for(const stage of card.Stages)for(const rank of stage.Levels)
   expected.set(`${card.Id}|${stage.Stage}|${rank.Level}`,rank);
  browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  const context=await browser.newContext({viewport:{width:1600,height:1200},deviceScaleFactor:1});
  const page=await context.newPage(),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(htmlPath).href,{waitUntil:'load'});
  const actual=await page.locator('.card-block').evaluateAll(cards=>cards.map(card=>({
   id:card.dataset.cardId,
   rows:[...card.querySelectorAll('.stage-block')].flatMap(stage=>[...stage.querySelectorAll('.rank')].map(rank=>{
    const effect=rank.querySelector('.effect');
    return {stages:stage.dataset.stages.split('|'),level:Number(rank.dataset.level),text:effect.textContent,
     normalWeight:getComputedStyle(effect).fontWeight,
     emphasized:[...effect.querySelectorAll('strong')].map(e=>({text:e.textContent,weight:getComputedStyle(e).fontWeight})),
     keywordWeights:[...rank.querySelectorAll('.rank-keywords a')].map(e=>getComputedStyle(e.querySelector('strong')||e).fontWeight)};
   }))
  })));
  let ranksChecked=0,keywordOccurrences=0,rangeLines=0;
  for(const card of actual)for(const rank of card.rows){
   for(const stage of rank.stages){
    const key=`${card.id}|${stage}|${rank.level}`,row=expected.get(key);
    assert.ok(row,key);assert.equal(rank.text,row.Effect,key);
    const expectedBold=[...row.EffectRichText.matchAll(/<b>(.*?)<\/b>/g)].map(m=>m[1]);
    assert.deepEqual(rank.emphasized.map(e=>e.text),expectedBold,key);
    ranksChecked++;
   }
   assert.ok(Number(rank.normalWeight)<600,'Effect body unexpectedly bold');
   for(const keyword of rank.emphasized){assert.ok(Number(keyword.weight)>=700,keyword.text);assert.ok(!keyword.text.includes('范围'),keyword.text);keywordOccurrences++;}
   for(const weight of rank.keywordWeights)assert.ok(Number(weight)>=700,'Keyword list not bold');
   if(rank.text.includes('范围'))rangeLines++;
  }
  assert.equal(ranksChecked,expected.size);assert.ok(keywordOccurrences>0);assert.ok(rangeLines>0);
  report.rankStagesChecked=ranksChecked;report.boldKeywordOccurrences=keywordOccurrences;report.normalRangeRows=rangeLines;
  report.checks.push('Every displayed stage/rank uses canonical bold keyword spans; ordinary effect and range text retain normal weight; keyword list labels are bold.');
  const reviewDom=await page.evaluate(before=>{
   const signature=doc=>[...doc.querySelectorAll('.card-block')].map(card=>({id:card.id,cardId:card.dataset.cardId,number:card.dataset.cardNumber,name:card.dataset.cardName,
    checkbox:card.querySelector('.review-checkbox').id,notes:card.querySelector('.review-notes').id}));
   return {before:signature(new DOMParser().parseFromString(before,'text/html')),after:signature(document)};
  },beforeHtml);
  assert.deepEqual(reviewDom.after,reviewDom.before,'Review DOM identity changed');
  const first=page.locator('.card-block').first();
  await first.locator('.review-checkbox').check();
  await first.locator('.review-notes').fill('字体验证：保留审核记录');
  await first.locator('.review-notes').blur();
  await page.reload({waitUntil:'load'});
  assert.ok(await first.locator('.review-checkbox').isChecked());
  assert.equal(await first.locator('.review-notes').inputValue(),'字体验证：保留审核记录');
  report.checks.push('Review controls and embedded storage script are unchanged; a fresh-context checkbox and note survive reload.');
  const sample=upgrades.Cards.find(card=>card.Stages.some(stage=>stage.Levels.some(row=>row.Effect.includes('范围')&&row.EffectRichText.includes('<b>'))));
  assert.ok(sample,'No range/keyword example');
  await page.locator('#card-search').fill(sample.Id);
  const sampleCard=page.locator(`.card-block[data-card-id="${sample.Id}"]`);
  await sampleCard.locator('.card-body').scrollIntoViewIfNeeded();
  await page.screenshot({path:path.join(out,'keyword-web-desktop.png')});
  await page.setViewportSize({width:390,height:844});
  await sampleCard.locator('.card-body').scrollIntoViewIfNeeded();
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,'Mobile horizontal overflow');
  await page.screenshot({path:path.join(out,'keyword-web-mobile.png')});
  assert.deepEqual(errors,[],'Browser errors');
  report.sampleCard=sample.Id;report.status='passed';
  await context.close();
 }catch(error){report.status='failed';report.error=error.stack;throw error;}
 finally{if(browser)await browser.close();fs.writeFileSync(path.join(out,'keyword-web-validation.json'),JSON.stringify(report,null,2));}
 console.log(JSON.stringify(report,null,2));
}
run().catch(error=>{console.error(error.stack);process.exitCode=1;});
