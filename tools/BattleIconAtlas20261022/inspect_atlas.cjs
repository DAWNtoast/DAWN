const fs=require('fs');
const path=require('path');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const out='D:/Game/outputs/battle-icon-atlas-20261022';
const errors=[];
(async()=>{
  const candidates=[
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Google/Chrome/Application/chrome.exe'
  ];
  const executablePath=candidates.find(file=>fs.existsSync(file));
  const browser=await chromium.launch({headless:true,...(executablePath?{executablePath}:{})});
  const page=await browser.newPage({viewport:{width:780,height:1080},deviceScaleFactor:1});
  page.on('pageerror',error=>errors.push(String(error)));
  await page.goto(pathToFileURL(path.join(out,'atlas-preview.html')).href);
  const frame=page.frameLocator('iframe');
  await frame.locator('#atlas-items .atlas-item').first().waitFor();
  await frame.locator('.atlas-icon').first().evaluate(canvas=>new Promise((resolve,reject)=>{
    let tries=0;
    const check=()=>{
      if(canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data.some(value=>value>0)) return resolve();
      if(tries++>100) return reject(new Error('blank icon canvas'));
      setTimeout(check,50);
    };check();
  }));
  const groups=await frame.locator('button[data-category]').evaluateAll(buttons=>buttons.map(b=>b.dataset.category));
  const counts={};
  for(const category of groups){
    await frame.locator(`button[data-category="${category}"]`).click();
    counts[category]=await frame.locator('.atlas-item').count();
    const blanks=await frame.locator('.atlas-icon').evaluateAll(canvases=>canvases.filter(canvas=>!canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data.some(v=>v>0)).length);
    if(blanks) throw new Error(`${category}: ${blanks} blank icons`);
    await page.evaluate(()=>scrollTo(0,0));
    await page.screenshot({path:path.join(out,`preview-${category}.png`)});
  }
  await frame.locator('button[data-category="status_naval"]').click();
  await frame.locator('button[data-inspect]').first().click();
  if(await frame.locator('.atlas-zoom:not([hidden])').count()!==1) throw new Error('Zoom did not open');
  await page.evaluate(()=>scrollTo(0,0));
  await page.screenshot({path:path.join(out,'preview-zoom.png')});
  await frame.locator('button[data-inspect]').first().click();
  if(await frame.locator('.atlas-zoom:not([hidden])').count()!==0) throw new Error('Zoom did not close');
  const widths=[];
  for(const width of [360,780]){
    await page.setViewportSize({width,height:1080});
    for(const category of groups){
      await frame.locator(`button[data-category="${category}"]`).click();
      const dimension=await frame.locator('#battle-icon-atlas').evaluate(root=>({root:root.getBoundingClientRect().width,viewport:document.documentElement.clientWidth,scroll:document.documentElement.scrollWidth}));
      if(dimension.scroll>dimension.viewport+1) {
        await page.screenshot({path:path.join(out,'overflow.png')});
        const offenders=await frame.locator('#battle-icon-atlas').evaluate(root=>Array.from(root.querySelectorAll('*')).filter(e=>e.getBoundingClientRect().right>document.documentElement.clientWidth+1).slice(0,12).map(e=>({tag:e.tagName,id:e.id,class:e.className,right:e.getBoundingClientRect().right,width:e.getBoundingClientRect().width,display:getComputedStyle(e).display})));
        throw new Error(`horizontal overflow ${category} ${width}: ${JSON.stringify({dimension,offenders})}`);
      }
      widths.push({width,category,...dimension});
    }
  }
  await page.setViewportSize({width:360,height:1080});
  await frame.locator('button[data-category="status_boarding"]').click();
  await page.evaluate(()=>scrollTo(0,0));
  await page.screenshot({path:path.join(out,'preview-mobile.png')});
  await page.setViewportSize({width:780,height:1080});
  await page.emulateMedia({colorScheme:'dark'});
  await frame.locator('button[data-category="resource"]').click();
  await page.evaluate(()=>scrollTo(0,0));
  await page.screenshot({path:path.join(out,'preview-dark.png')});
  if(errors.length) throw new Error(errors.join('\n'));
  const report={counts,total:Object.values(counts).reduce((a,b)=>a+b,0),categories:groups.length,canvasChecks:'all visible icons nonempty',zoom:'open and close passed',widths,javascriptErrors:errors};
  fs.writeFileSync(path.join(out,'inspection.json'),JSON.stringify(report,null,2));
  console.log(JSON.stringify({total:report.total,categories:report.categories,counts,canvasChecks:report.canvasChecks,zoom:report.zoom,javascriptErrors:errors}));
  await browser.close();
})().catch(error=>{console.error(error);process.exit(1);});
