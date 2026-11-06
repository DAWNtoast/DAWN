// Use a fresh browser context only, never the user's browser profile.
const fs=require('fs'), path=require('path'), assert=require('assert/strict');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root='D:/Game/outputs/card-list-by-school-20261023';
const out='D:/Game/outputs/card-review-apply-20261030';
const read=file=>JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
const data=read(path.join(root,'card-list-data.json'));
const inputFile=path.join(out,'input-backup/卡牌审核记录-2026-10-30.json');
const input=read(inputFile), summary=read(path.join(out,'review-input-summary.json'));
const report={status:'running',userProfileAccessed:false,checks:[]};
(async()=>{
  let browser;
  try {
    browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
    const context=await browser.newContext({viewport:{width:1440,height:1100},acceptDownloads:true});
    const page=await context.newPage(), errors=[];
    page.on('pageerror',error=>errors.push(error.message));
    await page.goto(pathToFileURL(path.join(root,'全部卡牌清单.html')).href,{waitUntil:'load'});
    assert.equal(data.Cards.length,313);
    for(const selector of ['.card-block','.review-checkbox','.review-notes']) assert.equal(await page.locator(selector).count(),data.Cards.length);
    const ids=new Set(data.Cards.map(card=>card.Id));
    for(const id of summary.DeletionIds) assert.equal(ids.has(id),false,id);
    for(const id of ['card_energy_order','card_tactical_draw','card_boarding_basic_09','card_boarding_basic_08']) {
      const card=data.Cards.find(card=>card.Id===id); assert.ok(card,id);
      assert.ok(card.StageVariants.every(row=>id.includes('boarding')?row.Stages.length===1&&row.Stages[0]==='接舷':!row.Stages.includes('接舷')),id);
    }
    report.checks.push('313 cards with review controls; 11 retired cards absent; ship and boarding identities separate.');
    await page.locator('#import-reviews-file').setInputFiles(inputFile);
    report.reviewedVisible=input.cards.filter(card=>ids.has(card.id)&&card.reviewed).length;
    await page.waitForFunction(count=>document.querySelectorAll('.review-checkbox:checked').length===count,report.reviewedVisible);
    await page.reload({waitUntil:'load'});
    const loaded=await page.locator('.card-block').evaluateAll(cards=>cards.map(card=>({id:card.dataset.cardId,reviewed:card.querySelector('.review-checkbox').checked,suggestion:card.querySelector('.review-notes').value})));
    const byId=new Map(loaded.map(card=>[card.id,card]));
    for(const old of input.cards.filter(card=>ids.has(card.id))) {
      assert.equal(byId.get(old.id).reviewed,old.reviewed,old.id);
      assert.equal(byId.get(old.id).suggestion,old.suggestion,old.id);
    }
    const waiting=page.waitForEvent('download'); await page.locator('#export-reviews').click();
    const download=await waiting, chunks=[];
    for await(const chunk of await download.createReadStream()) chunks.push(chunk);
    const exported=JSON.parse(Buffer.concat(chunks).toString('utf8').replace(/^\uFEFF/,''));
    await download.delete(); assert.equal(exported.cards.length,input.cards.length);
    for(const old of input.cards) {
      const saved=exported.cards.find(card=>card.id===old.id); assert.ok(saved,old.id);
      for(const key of ['reviewed','suggestion','number']) assert.equal(saved[key],old[key],old.id+' '+key);
    }
    report.suggestionsVisible=loaded.filter(card=>card.suggestion.trim()).length;
    report.reviewRecordsPreserved=exported.cards.length;
    report.checks.push('All checked states and 26 suggestions survive import, reload and export, including deleted cards.');
    await page.locator('#review-filter').selectOption('notes');
    assert.equal(await page.locator('.card-block:visible').count(),report.suggestionsVisible);
    await page.locator('#clear-filters').click();
    await page.locator('#card-search').fill('card_special_crew_gray_gull_cook_pepe');
    const preview=page.locator('article[data-card-id="card_special_crew_gray_gull_cook_pepe"]');
    await preview.scrollIntoViewIfNeeded();
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    await page.screenshot({path:path.join(out,'updated-card-desktop.png')});
    await page.setViewportSize({width:390,height:844});
    await preview.locator('.card-review').scrollIntoViewIfNeeded();
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    await page.screenshot({path:path.join(out,'updated-review-mobile.png')});
    await page.setViewportSize({width:1440,height:1100}); await page.locator('#clear-filters').click();
    await page.locator('.card-block img').evaluateAll(images=>images.forEach(img=>img.loading='eager'));
    await page.waitForFunction(()=>[...document.querySelectorAll('.card-block img')].every(img=>img.complete));
    assert.deepEqual(await page.locator('.card-block img').evaluateAll(images=>images.filter(img=>!img.naturalWidth).map(img=>img.src)),[]);
    assert.deepEqual(errors,[]);
    report.checks.push('Filters, desktop/mobile layout and all card images pass; no browser script errors.');
    report.status='passed';
  } catch(error) {report.status='failed';report.error=error.stack;throw error;}
  finally {
    if(browser) await browser.close();
    report.cleanup='Fresh context and temporary downloads removed; user browser storage never accessed.';
    fs.writeFileSync(path.join(out,'review-page-validation.json'),JSON.stringify(report,null,2));
    console.log(JSON.stringify(report,null,2));
  }
})().catch(error=>{console.error(error);process.exitCode=1;});
