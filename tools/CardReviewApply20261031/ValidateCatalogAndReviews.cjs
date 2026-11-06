// Independent task validation. Browser work uses an ephemeral context, never the user's profile.
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root='D:/Game/outputs/card-list-by-school-20261023';
const task='D:/Game/outputs/card-review-apply-20261031';
const backup=path.join(task,'input-backup/outputs/card-list-by-school-20261023');
const input=path.join(task,'input-backup/卡牌审核记录-2026-10-31.json');
const originalInput='C:/Users/Admin/Downloads/卡牌审核记录-2026-10-31.json';
const output=path.join(task,'validation');
const read=file=>JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const key=(id,stage,level)=>`${id}|${stage}|${level}`;
const roundRows=data=>data.Cards.flatMap(card=>card.Stages.flatMap(stage=>stage.Levels.map(row=>({id:card.Id,stage:stage.Stage,level:row.Level,row}))));
fs.mkdirSync(output,{recursive:true});
const report={status:'running',userBrowserProfileAccessed:false,checks:[]};
async function main(){
 let browser;
 try{
  assert.equal(hash(input),hash(originalInput),'User input backup differs');
  const beforeBase=read(path.join(backup,'card-list-data.json')),base=read(path.join(root,'card-list-data.json'));
  const beforeUpgrades=read(path.join(backup,'card-upgrade-data.json')),upgrades=read(path.join(root,'card-upgrade-data.json'));
  const audit=read(path.join(task,'audit/formula-upgrade-audit.json'));
  const roundedKeys=new Set(audit.RoundedFormulas.map(row=>key(row.Id,row.Stage,row.Level)));
  assert.equal(roundedKeys.size,44);assert.equal(new Set(audit.RoundedFormulas.map(row=>row.Id)).size,19);
  const afterRows=roundRows(upgrades),beforeRows=roundRows(beforeUpgrades);
  const expected=new Map(afterRows.map(row=>[key(row.id,row.stage,row.level),row.row]));
  assert.equal(base.Cards.length,313);assert.equal(expected.size,1017);
  const diffs=[];
  for(const [name,before,after] of [['base',beforeBase,base],['upgrades',beforeUpgrades,upgrades]]){
   assert.deepEqual(after.Cards.map(c=>c.Id),before.Cards.map(c=>c.Id),'Card identities/order changed');
   const normalized=structuredClone(after.Cards);
   for(let i=0;i<after.Cards.length;i++){
    const card=after.Cards[i],old=before.Cards[i],clean=normalized[i];
    const stages=card.Stages||card.StageVariants,oldStages=old.Stages||old.StageVariants,cleanStages=clean.Stages||clean.StageVariants;
    assert.equal(stages.length,oldStages.length);
    for(let s=0;s<stages.length;s++){
     const stage=stages[s],oldStage=oldStages[s],cleanStage=cleanStages[s];
     const rows=stage.Levels||[stage],oldRows=oldStage.Levels||[oldStage],cleanRows=cleanStage.Levels||[cleanStage];
     assert.equal(rows.length,oldRows.length);
     for(let r=0;r<rows.length;r++){
      const row=rows[r],oldRow=oldRows[r],cleanRow=cleanRows[r],level=row.Level||0,stageName=stage.Stage||stage.Stages.join('|');
      for(const field of new Set([...Object.keys(row),...Object.keys(oldRow)])){
       if(JSON.stringify(row[field])===JSON.stringify(oldRow[field]))continue;
       const effectField=field==='Effect'||field==='EffectRichText';
       const formula=roundedKeys.has(key(card.Id,stageName,level));
       const fire07=card.Id==='card_boarding_fire_07'&&(level===1||level===2);
       const cost=card.Id==='card_boarding_fire_14'&&field==='OtherCost'&&(level===0||level===1);
       assert.ok(cost||(name==='upgrades'&&effectField&&(formula||fire07)),`Unexpected change ${name}/${card.Id}/${stageName}/${level}/${field}`);
       if(cost){assert.equal(oldRow[field],'生命值 4');assert.equal(row[field],'—');}
       diffs.push({catalog:name,id:card.Id,stage:stageName,level,field,classification:cost?'remove_fire14_life_cost':fire07?'explicit_fire07_formula':card.Id==='card_boarding_fire_11'?'explicit_fire11_formula':'expand_existing_formula',before:oldRow[field],after:row[field]});
       cleanRow[field]=oldRow[field];
      }
     }
    }
   }
   assert.deepEqual(normalized,before.Cards,`Non-row changes in ${name}`);
  }
  const changedFormulas=diffs.filter(d=>d.catalog==='upgrades'&&d.field==='Effect'&&roundedKeys.has(key(d.id,d.stage,d.level)));
  assert.equal(changedFormulas.length,44);
  for(const {id,stage,level,row} of afterRows){
   assert.ok(!/四舍五入|[)）]\s*[×*]\s*\d/.test(row.Effect),`Old formula presentation ${id}/${stage}/${level}`);
   assert.equal(row.EffectRichText.replace(/<\/?b>/g,''),row.Effect);
   const emphasized=[...row.EffectRichText.matchAll(/<b>(.*?)<\/b>/g)].map(m=>m[1]);
   assert.deepEqual([...new Set(emphasized)],row.Keywords.map(term=>term.Name));
   assert.ok(!emphasized.some(term=>/范围|距离|才可以使用/.test(term)));
  }
  const rowFor=(id,level)=>expected.get(key(id,'接舷',level));
  for(const [level,formula] of ['60+2×自身燃烧层数','75+3×自身燃烧层数','90+5×自身燃烧层数'].entries())assert.ok(rowFor('card_boarding_fire_11',level).Effect.includes(formula));
  for(const [level,formula] of ['3+2X','4+3X','5+4X'].entries())assert.ok(rowFor('card_boarding_fire_07',level).Effect.includes(formula));
  for(const [level,percentage] of ['50%','63%','84%'].entries())assert.ok(rowFor('card_boarding_arcane_13',level).Effect.includes(percentage));
  assert.ok(rowFor('card_boarding_tide_08',0).Effect.includes('60+1.5X'),'Base fractional coefficient changed');
  assert.equal(diffs.filter(d=>d.catalog==='base').length,1);
  assert.equal(diffs.filter(d=>d.catalog==='upgrades'&&d.field==='OtherCost').length,2);
  assert.equal(diffs.filter(d=>d.catalog==='upgrades'&&d.field==='Effect').length,46);
  report.inputSha256=hash(input);report.publishedCards=base.Cards.length;report.stageRanks=afterRows.length;
  report.changedCards=[...new Set(diffs.map(d=>d.id))];report.classifiedFieldDifferences=diffs.length;
  report.changedFormulaRanks=44;report.explicitFire07Ranks=2;report.removedLifeCostRanks=2;
  report.checks.push('All data differences classified: 44 existing rounded formula ranks, two explicit fire07 ranks, and fire14 life-cost removal; all other fields unchanged.');
  report.checks.push('All 1017 effects avoid whole-expression upgrade multipliers/rounding text and preserve canonical keyword emphasis; explicit fire formulas and echo 50/63/84% match.');
  fs.writeFileSync(path.join(output,'catalog-classified-differences.json'),JSON.stringify(diffs,null,2));
  if(process.argv.includes('--data-only')){report.status='passed';return;}
  const htmlPath=path.join(root,'全部卡牌清单.html'),html=fs.readFileSync(htmlPath,'utf8');
  const beforeHtml=fs.readFileSync(path.join(backup,'全部卡牌清单.html'),'utf8');
  const reviewScript=fs.readFileSync('D:/Game/tools/CardListBySchool20261023/CardReview.js','utf8');
  assert.ok(html.includes(reviewScript)&&beforeHtml.includes(reviewScript),'Review script changed');
  browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  const context=await browser.newContext({viewport:{width:1600,height:1200},deviceScaleFactor:1,acceptDownloads:true});
  const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(htmlPath).href,{waitUntil:'load'});
  const dom=await page.locator('.card-block').evaluateAll(cards=>cards.map(card=>({id:card.dataset.cardId,rows:[...card.querySelectorAll('.stage-block')].flatMap(stage=>[...stage.querySelectorAll('.rank')].map(rank=>{
   const effect=rank.querySelector('.effect');return {stages:stage.dataset.stages.split('|'),level:Number(rank.dataset.level),effect:effect.textContent,cost:rank.querySelector('.play-cost').textContent,normalWeight:getComputedStyle(effect).fontWeight,
    bold:[...effect.querySelectorAll('strong')].map(e=>({text:e.textContent,weight:getComputedStyle(e).fontWeight}))};
  }))})));
  let checked=0,bold=0;
  for(const card of dom)for(const row of card.rows){
   assert.ok(Number(row.normalWeight)<600);
   for(const stage of row.stages){const expectedRow=expected.get(key(card.id,stage,row.level));assert.ok(expectedRow);assert.equal(row.effect,expectedRow.Effect);assert.deepEqual(row.bold.map(e=>e.text),[...expectedRow.EffectRichText.matchAll(/<b>(.*?)<\/b>/g)].map(m=>m[1]));checked++;}
   for(const term of row.bold){assert.ok(Number(term.weight)>=700);bold++;}
   if(card.id==='card_boarding_fire_14')assert.ok(!row.cost.includes('生命值'));
  }
  assert.equal(checked,1017);report.browserStageRanks=checked;report.browserBoldOccurrences=bold;
  const identity=await page.evaluate(before=>{
   const snapshot=doc=>[...doc.querySelectorAll('.card-block')].map(card=>({id:card.id,cardId:card.dataset.cardId,number:card.dataset.cardNumber,name:card.dataset.cardName,checkbox:card.querySelector('.review-checkbox').id,notes:card.querySelector('.review-notes').id}));
   return {before:snapshot(new DOMParser().parseFromString(before,'text/html')),after:snapshot(document)};
  },beforeHtml);
  assert.deepEqual(identity.after,identity.before,'Review identities changed');
  const reviews=read(input),reviewMap=new Map(reviews.cards.map(c=>[c.id,c]));
  await page.locator('#import-reviews-file').setInputFiles(input);
  await page.waitForFunction(()=>document.querySelector('#review-storage-status').textContent.includes('已导入'));
  async function verifyInputs(){
   const values=await page.locator('.card-block').evaluateAll(cards=>cards.map(c=>({id:c.dataset.cardId,reviewed:c.querySelector('.review-checkbox').checked,suggestion:c.querySelector('.review-notes').value})));
   for(const value of values){const original=reviewMap.get(value.id);assert.ok(original,value.id);assert.equal(value.reviewed,original.reviewed,value.id);assert.equal(value.suggestion,original.suggestion,value.id);}
  }
  await verifyInputs();await page.reload({waitUntil:'load'});await verifyInputs();
  const downloadPromise=page.waitForEvent('download');await page.locator('#export-reviews').click();const download=await downloadPromise;
  const stream=await download.createReadStream();const parts=[];for await(const part of stream)parts.push(part);const exported=JSON.parse(Buffer.concat(parts).toString('utf8'));await download.delete();
  const normalize=cards=>cards.map(({updatedAt,...content})=>content).sort((a,b)=>a.id.localeCompare(b.id));
  assert.deepEqual(normalize(exported.cards),normalize(reviews.cards),'Import/refresh/export altered review content');
  // Existing import merges stamp the import time. Preserve and report this behavior separately.
  const updatedTimestamps=exported.cards.filter(card=>card.updatedAt!==reviewMap.get(card.id).updatedAt);
  for(const card of updatedTimestamps)assert.ok(Number.isFinite(Date.parse(card.updatedAt)),`Invalid import timestamp ${card.id}`);
  const liveIds=new Set(base.Cards.map(c=>c.Id));const removed=reviews.cards.filter(c=>!liveIds.has(c.id));
  assert.equal(removed.length,11);for(const record of removed)assert.ok(exported.cards.some(c=>c.id===record.id&&c.suggestion===record.suggestion));
  const activeReviews=reviews.cards.filter(c=>liveIds.has(c.id));
  assert.equal(exported.summary.reviewed,activeReviews.filter(c=>c.reviewed).length);
  assert.equal(exported.summary.withSuggestions,activeReviews.filter(c=>c.suggestion.trim()).length);
  report.reviewRoundTrip={records:exported.cards.length,reviewed:exported.cards.filter(c=>c.reviewed).length,withSuggestions:exported.cards.filter(c=>c.suggestion.trim()).length,retiredRecords:removed.length,visibleSummary:exported.summary,allReviewContentPreserved:true,updatedAtChangedByExistingImportMerge:updatedTimestamps.length};
  report.checks.push('Fresh-browser import, refresh and export preserve id/number/name/reviewed/suggestion for every record, including all 11 retired cards; existing import behavior updates timestamps, and review DOM/storage script are unchanged.');
  const game=read(path.join(root,'card-game-view-data.json')),gameRows=new Map(game.Cards.map(r=>[key(r.Id,r.Stage,r.Level),r]));
  assert.equal(gameRows.size,1017);
  for(const row of game.Cards){assert.ok(expected.has(key(row.Id,row.Stage,row.Level)));assert.equal(hash(path.join(root,row.Image)),row.ImageSha256.toLowerCase(),row.Image);}
  report.gameImageHashesVerified=gameRows.size;report.gameInputFingerprint=game.InputFingerprint;report.gameGeneratedUtc=game.GeneratedUtc;
  const samples=[['card_boarding_fire_11',1],['card_boarding_fire_07',2],['card_boarding_fire_14',0],['card_boarding_arcane_13',2],['card_boarding_curse_08',3]];
  for(const [id,level] of samples){
   await page.locator('#card-search').fill(id);const card=page.locator(`.card-block[data-card-id="${id}"]`);
   await card.locator('.preview-stage').selectOption('接舷');await card.locator('.preview-level').selectOption(String(level));
   const image=card.locator('.game-card-image');await image.evaluate(e=>e.decode());
   assert.equal(await image.evaluate(e=>e.src),pathToFileURL(path.join(root,gameRows.get(key(id,'接舷',level)).Image)).href);
   if(id.startsWith('card_boarding_fire_'))await image.screenshot({path:path.join(output,`${id}-level${level}-browser.png`)});
  }
  await page.locator('#card-search').fill('card_boarding_fire_11');
  const sample=page.locator('.card-block[data-card-id="card_boarding_fire_11"]');
  await sample.locator('.card-body').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(output,'formula-review-desktop.png')});
  await page.setViewportSize({width:390,height:844});await sample.locator('.card-body').scrollIntoViewIfNeeded();
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);await page.screenshot({path:path.join(output,'formula-review-mobile.png')});
  assert.deepEqual(errors,[]);report.checks.push('Updated game-view selectors load correct stage/rank PNGs; desktop/mobile show new formulas and unchanged review UI without JS errors or horizontal overflow.');
  await context.close();report.status='passed';
 }catch(error){report.status='failed';report.error=error.stack;throw error;}
 finally{if(browser)await browser.close();fs.writeFileSync(path.join(output,process.argv.includes('--data-only')?'catalog-independent-validation.json':'catalog-browser-validation.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));}
}
main().catch(e=>{console.error(e.stack);process.exitCode=1;});
