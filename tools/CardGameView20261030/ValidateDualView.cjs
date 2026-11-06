// Independent QA: current files and fresh browser contexts only; never a user profile.
const fs=require('fs'), path=require('path'), assert=require('assert/strict'), crypto=require('crypto');
const {spawnSync}=require('child_process');
const {pathToFileURL,fileURLToPath}=require('url');
const {chromium}=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root='D:/Game/outputs/card-list-by-school-20261023';
const out='D:/Game/outputs/card-game-view-20261030/validation';
const python='C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe';
const read=file=>JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const resolveFile=relative=>relative.startsWith('file:')?fileURLToPath(relative):path.resolve(root,relative);
const key=row=>`${row.Id}|${row.Stage}|${row.Level}`;
const stageKey=row=>`${row.Id}|${row.Stage}`;
const htmlURL=pathToFileURL(path.join(root,'全部卡牌清单.html')).href;
const href=relative=>new URL(relative,htmlURL).href;
const report={status:'running',userBrowserProfileAccessed:false,checks:[]};
fs.mkdirSync(out,{recursive:true});

async function run(){
  let browser;
  try {
    const base=read(path.join(root,'card-list-data.json'));
    const upgrades=read(path.join(root,'card-upgrade-data.json'));
    const art=read(path.join(root,'card-art-data.json'));
    const game=read(path.join(root,'card-game-view-data.json'));
    assert.equal(base.Cards.length,313);
    assert.equal(art.Cards.length,340);
    const expected=new Map();
    for(const card of upgrades.Cards) for(const stage of card.Stages) for(const rank of stage.Levels){
      const row={Id:card.Id,Stage:stage.Stage,Level:rank.Level};
      assert.equal(expected.has(key(row)),false,key(row)); expected.set(key(row),rank);
    }
    assert.equal(expected.size,1017);
    assert.equal(game.Cards.length,expected.size);
    const gameByKey=new Map();
    for(const row of game.Cards){
      assert.ok(expected.has(key(row)),`Unexpected game render: ${key(row)}`);
      assert.equal(gameByKey.has(key(row)),false,`Duplicate game render: ${key(row)}`);
      gameByKey.set(key(row),row);
      assert.ok(row.Image&&row.Image.startsWith('game-view/'),key(row));
      assert.ok(row.DesignWidth>0&&row.DesignHeight>0,key(row));
      assert.ok(row.Width>=row.DesignWidth&&row.Height>=row.DesignHeight,key(row));
      const file=resolveFile(row.Image);
      assert.ok(fs.existsSync(file),file);
      assert.equal(hash(file),row.ImageSha256.toLowerCase(),file);
      assert.ok(row.FrameResourcePath&&row.ArtResourcePath,key(row));
    }
    const artByStage=new Map(), originalFiles=new Set();
    for(const row of art.Cards){
      assert.ok(row.OriginalImage,`Missing original source: ${stageKey(row)}`);
      assert.equal(artByStage.has(stageKey(row)),false,stageKey(row));
      artByStage.set(stageKey(row),row);
      const file=resolveFile(row.OriginalImage);
      const source=path.resolve(row.SourcePath);
      assert.ok(fs.existsSync(file),file);
      assert.equal(hash(file),row.SourceSha256.toLowerCase(),file);
      assert.equal(hash(source),row.SourceSha256.toLowerCase(),source);
      assert.notEqual(href(row.OriginalImage),href(row.Image),`Original must not use 640px thumbnail: ${stageKey(row)}`);
      assert.equal(row.SourceWidth,1254); assert.equal(row.SourceHeight,1254);
      originalFiles.add(source);
    }
    assert.equal(originalFiles.size,310);
    assert.equal(new Set(game.Cards.map(stageKey)).size,artByStage.size);
    for(const row of game.Cards) assert.ok(artByStage.has(stageKey(row)),stageKey(row));
    report.cards=base.Cards.length;report.stages=art.Cards.length;report.gameViews=game.Cards.length;
    report.originalImages=originalFiles.size;
    report.checks.push('All 340 original mappings match the 1254px source file hashes; all 1017 legal rank/stage views have unique complete mappings and matching hashes.');
    const inspection=String.raw`
import json,sys
from pathlib import Path
from PIL import Image,ImageStat,ImageDraw
from urllib.parse import urlparse,unquote
root=Path(sys.argv[1]); output=Path(sys.argv[2])
art=json.loads((root/'card-art-data.json').read_text(encoding='utf-8-sig'))
game=json.loads((root/'card-game-view-data.json').read_text(encoding='utf-8-sig'))
seen=set(); originals=0
for row in art['Cards']:
    source=Path(row['SourcePath'])
    if str(source) in seen: continue
    seen.add(str(source))
    with Image.open(source) as image:
        image.load()
        assert image.size==(row['SourceWidth'],row['SourceHeight']),str(source)
    originals+=1
seen=set(); screenshots=0; smallest_alpha_fraction=1.0
for row in game['Cards']:
    source=root/row['Image']
    if str(source) in seen: continue
    seen.add(str(source))
    with Image.open(source) as image:
        image.load()
        assert image.format=='PNG',str(source)
        assert image.size==(row['Width'],row['Height']),str(source)
        rgba=image.convert('RGBA'); alpha=rgba.getchannel('A')
        bounds=alpha.getbbox(); assert bounds is not None,'Empty screenshot '+str(source)
        pixels=(bounds[2]-bounds[0])*(bounds[3]-bounds[1]); fraction=pixels/(image.width*image.height)
        assert fraction>0.05,'Tiny rendered content '+str(source)
        smallest_alpha_fraction=min(smallest_alpha_fraction,fraction)
        stats=ImageStat.Stat(rgba.convert('RGB'))
        assert max(stats.stddev)>5,'Flat/blank screenshot '+str(source)
        assert image.width>=row['DesignWidth'] and image.height>=row['DesignHeight']
    screenshots+=1
result={'status':'passed','originalsDecoded':originals,'uniqueGamePNGsDecoded':screenshots,'minimumContentBoundsAreaFraction':smallest_alpha_fraction,'sourceImagesModified':False}
sample_ids=['card_special_crew_gray_gull_cook_pepe','card_boarding_basic_08','card_boarding_arcane_05','card_school_tide_n03','card_boarding_fire_14','card_school_curse_b01','card_school_thunder_n06','card_school_blood_b05']
samples=[]
for card_id in sample_ids:
    rows=[row for row in game['Cards'] if row['Id']==card_id]
    assert rows,card_id
    samples.append(max(rows,key=lambda row:(row['Level'],row['StageId']=='Boarding',row['StageId']=='Naval')))
sheet=Image.new('RGB',(1280,1120),'#e9eef0'); draw=ImageDraw.Draw(sheet)
for i,row in enumerate(samples):
    left=(i%4)*320; top=(i//4)*560
    with Image.open(root/row['Image']) as original:
        preview=original.convert('RGBA'); preview.thumbnail((300,500),Image.Resampling.LANCZOS)
        sheet.paste(preview,(left+(320-preview.width)//2,top+35),preview)
    draw.text((left+8,top+8),row['Id'].replace('card_','')[:43],fill='black')
    draw.text((left+8,top+22),str(row['StageId'])+' +'+str(row['Level']),fill='black')
sheet.save(output.parent/'game-card-contact-sheet.png')
result['contactSheet']='game-card-contact-sheet.png'
output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result))
`;
    const inspected=spawnSync(python,['-c',inspection,root,path.join(out,'image-validation.json')],{encoding:'utf8',maxBuffer:1024*1024});
    assert.equal(inspected.status,0,inspected.stderr||inspected.stdout);
    report.imageInspection=JSON.parse(inspected.stdout.trim());
    report.checks.push('Pillow decoded every source image and game PNG; dimensions match and no game image is empty or flat.');

    browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
    const context=await browser.newContext({viewport:{width:1600,height:1200},deviceScaleFactor:1,acceptDownloads:true});
    const page=await context.newPage(),errors=[];
    page.on('pageerror',error=>errors.push(error.message));
    await page.goto(htmlURL,{waitUntil:'load'});
    for(const selector of ['.card-block','.card-visuals','.original-art-image','.game-card-image','.preview-stage','.preview-level','.review-checkbox','.review-notes'])
      assert.equal(await page.locator(selector).count(),base.Cards.length,selector);
    const dom=await page.locator('.card-block').evaluateAll(cards=>cards.map(card=>({
      id:card.dataset.cardId,views:JSON.parse(card.querySelector('.card-visuals').dataset.views),
      original:card.querySelector('.original-art-image').src,originalLink:card.querySelector('.original-art-link').href,
      game:card.querySelector('.game-card-image').src,gameLink:card.querySelector('.game-card-link').href,
      stages:[...card.querySelector('.preview-stage').options].map(option=>option.value),
    })));
    const fromDom=new Set();
    for(const card of dom){
      assert.ok(card.views.length>0,card.id);
      const expectedStages=new Set();
      for(const view of card.views){
        const id=`${card.id}|${view.stage}|${view.level}`,render=gameByKey.get(id);
        assert.ok(render,id);assert.equal(fromDom.has(id),false,id);fromDom.add(id);expectedStages.add(view.stage);
        assert.equal(href(view.image),href(render.Image),id);
        assert.equal(view.width,render.Width,id);assert.equal(view.height,render.Height,id);
        assert.equal(view.designWidth,render.DesignWidth,id);assert.equal(view.designHeight,render.DesignHeight,id);
        assert.equal(href(view.originalImage),href(artByStage.get(`${card.id}|${view.stage}`).OriginalImage),id);
        assert.equal(view.originalWidth,1254,id);assert.equal(view.originalHeight,1254,id);
      }
      assert.deepEqual(card.stages.sort(),[...expectedStages].sort(),card.id);
      const first=card.views.find(view=>view.level===0);assert.ok(first,card.id);
      assert.equal(card.original,href(first.originalImage),card.id);assert.equal(card.originalLink,card.original,card.id);
      assert.equal(card.game,href(first.image),card.id);assert.equal(card.gameLink,card.game,card.id);
    }
    assert.equal(fromDom.size,expected.size);
    report.checks.push('Every card embeds all its legal stage/rank views, original image source and independent review controls.');
    const cardLocator=id=>page.locator(`article[data-card-id="${id}"]`);
    const selectView=async(id,stage,level)=>{
      const card=cardLocator(id),expected=gameByKey.get(`${id}|${stage}|${level}`);
      await page.locator('#card-search').fill(id);
      await card.locator('.preview-stage').selectOption(stage);
      await card.locator('.preview-level').selectOption(String(level));
      const actual=await card.locator('.game-card-image').evaluate(image=>({src:image.src,loaded:image.complete&&image.naturalWidth>0}));
      assert.equal(actual.src,href(expected.Image));
      await card.locator('.game-card-image').evaluate(image=>image.decode());
      const size=await card.locator('.game-card-image').evaluate(image=>({width:image.getBoundingClientRect().width,height:image.getBoundingClientRect().height,naturalWidth:image.naturalWidth,naturalHeight:image.naturalHeight}));
      assert.equal(size.naturalWidth,expected.Width);assert.equal(size.naturalHeight,expected.Height);
      assert.ok(Math.abs(size.width-expected.DesignWidth)<1,`${id} design width ${size.width}/${expected.DesignWidth}`);
      assert.ok(Math.abs(size.height-expected.DesignHeight)<1,`${id} design height ${size.height}/${expected.DesignHeight}`);
      assert.equal(await card.locator('.game-card-link').getAttribute('href'),await card.locator('.game-card-image').getAttribute('src'));
      const original=artByStage.get(`${id}|${stage}`);
      assert.equal(await card.locator('.original-art-image').evaluate(image=>image.src),href(original.OriginalImage));
      assert.equal(await card.locator('.original-art-link').evaluate(link=>link.href),href(original.OriginalImage));
      await card.locator('.original-art-image').evaluate(image=>image.decode());
      assert.equal(await card.locator('.original-art-image').evaluate(image=>image.naturalWidth),1254);
      return card;
    };
    await selectView('card_special_crew_gray_gull_cook_pepe','准备',0);
    await selectView('card_special_crew_gray_gull_cook_pepe','接舷',1);
    await selectView('card_special_crew_gray_gull_cook_pepe','海战',0);
    await selectView('card_boarding_basic_08','接舷',3);
    const preview=await selectView('card_special_crew_gray_gull_cook_pepe','接舷',1);
    await preview.locator('.card-visuals').scrollIntoViewIfNeeded();
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    await page.screenshot({path:path.join(out,'dual-view-desktop.png')});
    for(const selector of ['.original-art-link','.game-card-link']){
      const expectedImage=await preview.locator(selector).evaluate(link=>link.href);
      await preview.locator(selector).click();
      assert.equal(await page.locator('#card-image-dialog').isVisible(),true);
      await page.locator('#card-image-dialog-image').evaluate(image=>image.decode());
      assert.equal(await page.locator('#card-image-dialog-image').evaluate(image=>image.src),expectedImage);
      assert.equal(await page.locator('#image-view-original-link').evaluate(link=>link.href),expectedImage);
      await page.locator('#image-view-pixels').click();
      const actual=await page.locator('#card-image-dialog-image').evaluate(image=>({width:image.getBoundingClientRect().width,natural:image.naturalWidth}));
      assert.ok(Math.abs(actual.width-actual.natural)<1,`${selector} 1:1 size ${actual.width}/${actual.natural}`);
      await page.locator('#image-view-fit').click();
      await page.locator('#image-view-close').click();
      assert.equal(await page.locator('#card-image-dialog').isVisible(),false);
    }
    report.checks.push('Stage and upgrade controls load the correct original/game images; game renders display at design size; both viewers support fit and true 1:1 pixels.');
    await page.setViewportSize({width:390,height:844});
    await preview.locator('.card-visuals').scrollIntoViewIfNeeded();
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    await page.screenshot({path:path.join(out,'dual-view-mobile.png')});
    await page.setViewportSize({width:1600,height:1200});
    await page.locator('#clear-filters').click();
    const inputFile='D:/Game/outputs/card-review-apply-20261030/input-backup/卡牌审核记录-2026-10-30.json';
    const input=read(inputFile),inputHash=hash(inputFile);
    await page.locator('#import-reviews-file').setInputFiles(inputFile);
    await page.waitForFunction(()=>document.querySelectorAll('.review-checkbox:checked').length===94);
    await page.reload({waitUntil:'load'});
    const loaded=await page.locator('.card-block').evaluateAll(cards=>cards.map(card=>({id:card.dataset.cardId,reviewed:card.querySelector('.review-checkbox').checked,suggestion:card.querySelector('.review-notes').value})));
    const loadedById=new Map(loaded.map(card=>[card.id,card]));
    for(const old of input.cards.filter(card=>loadedById.has(card.id))){
      assert.equal(loadedById.get(old.id).reviewed,old.reviewed,old.id);
      assert.equal(loadedById.get(old.id).suggestion,old.suggestion,old.id);
    }
    const waiting=page.waitForEvent('download');await page.locator('#export-reviews').click();
    const download=await waiting,chunks=[];
    for await(const chunk of await download.createReadStream()) chunks.push(chunk);
    const exported=JSON.parse(Buffer.concat(chunks).toString('utf8').replace(/^\uFEFF/,''));await download.delete();
    assert.equal(exported.cards.length,324);
    assert.equal(exported.cards.filter(card=>card.reviewed).length,105);
    assert.equal(exported.cards.filter(card=>card.suggestion.trim()).length,26);
    for(const old of input.cards){
      const saved=exported.cards.find(card=>card.id===old.id);assert.ok(saved,old.id);
      for(const field of ['reviewed','suggestion','number']) assert.equal(saved[field],old[field],old.id+' '+field);
    }
    assert.equal(hash(inputFile),inputHash);
    assert.equal(loaded.filter(card=>card.suggestion.trim()).length,15);
    assert.deepEqual(errors,[]);
    report.review={recordsPreserved:324,checkedPreserved:105,suggestionsPreserved:26,currentChecked:94,currentSuggestions:15};
    report.checks.push('Desktop/mobile layouts have no horizontal overflow; all original review records survive import/reload/export unchanged, including retired cards; no browser script errors.');
    report.status='passed';
  } catch(error){report.status='failed';report.error=error.stack;throw error;}
  finally{
    if(browser)await browser.close();
    report.cleanup='Fresh browser contexts closed and temporary downloads removed; user browser storage and game source never accessed for writing.';
    fs.writeFileSync(path.join(out,'dual-view-validation.json'),JSON.stringify(report,null,2));
    console.log(JSON.stringify(report,null,2));
  }
}
run().catch(error=>{console.error(error);process.exitCode=1;});
