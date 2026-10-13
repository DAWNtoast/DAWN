// Deterministic asset packing only; artwork is drawn by the built-in ImageGen tool.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = 'D:/Game/Artifacts/ShipArtRedraw20261012';
const resources = 'D:/Game/UnityClient/Assets/_Project/Resources';
const manifest = JSON.parse(fs.readFileSync(path.join(root,'manifest.json'),'utf8'));
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const xml = t => t.replace(/[&<>]/g, c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c]));
function configureMeta(file, maxSize, width, height) {
  let text = fs.readFileSync(file,'utf8');
  text = text.replace(/maxTextureSize: \d+/g,'maxTextureSize: '+maxSize)
    .replace(/filterMode: \d+/g,'filterMode: 1')
    .replace(/compressionQuality: \d+/g,'compressionQuality: 100');
  text = text.replace(/(buildTarget: Standalone\r?\n[\s\S]*?)(?=  - serializedVersion:|  spriteSheet:)/, block => block
    .replace(/textureFormat: -?\d+/,'textureFormat: 25')
    .replace(/textureCompression: \d+/,'textureCompression: 2')
    .replace(/overridden: \d+/,'overridden: 1'));
  // Legacy sheet has a single full-sheet Sprite subasset; retain its fileID.
  if (width && height) {
    text=text.replace(/(        width:) 1717/g,'$1 '+width).replace(/(        height:) 916/g,'$1 '+height);
  }
  fs.writeFileSync(file,text);
}
(async()=> {
  const report={tool:'built-in image_gen',cellSize:[1536,1024],padding:'6% of each cell; proportional resampling only',starterSha256:hash(manifest.starter),atlases:[],cells:[]};
  const contacts={};
  for(const group of ['roster','regional']) {
    const entries=manifest.entries.filter(e=>e.group===group);
    const cols=entries[0].cols, width=cols*1536, height=2048;
    const layers=[]; contacts[group]=[];
    for(const e of entries) {
      const source=path.join(root,'generated',e.name+'.png');
      const meta=await sharp(source).metadata();
      if (!meta.hasAlpha || meta.width!==1536 || meta.height!==1024) throw new Error('Invalid generated source '+source);
      const resized=await sharp(source).resize({width:1352,height:900,fit:'inside'}).png().toBuffer();
      const resizedMeta=await sharp(resized).metadata();
      const left=Math.floor((1536-resizedMeta.width)/2), top=Math.floor((1024-resizedMeta.height)/2);
      const cell=await sharp({create:{width:1536,height:1024,channels:4,background:{r:0,g:0,b:0,alpha:0}}})
        .composite([{input:resized,left,top}]).png().toBuffer();
      await sharp(cell).toFile(path.join(root,'preview',e.name+'-packed.png'));
      const raw=await sharp(cell).raw().toBuffer();
      const bottoms=[];
      for(let x=691;x<=845;x++) {
        for(let y=1023;y>=512;y--) if(raw[(y*1536+x)*4+3]>=128){bottoms.push(y);break;}
      }
      if(bottoms.length<100) throw new Error('Could not measure central keel: '+e.name);
      bottoms.sort((a,b)=>a-b);
      const bottom=bottoms[Math.floor(bottoms.length/2)];
      const contact=Math.round(((1023-bottom)/1024+.015)*10000)/10000;
      contacts[group].push(contact);
      layers.push({input:cell,left:(e.cell%cols)*1536,top:Math.floor(e.cell/cols)*1024});
      report.cells.push({name:e.name,source,sourceSha256:hash(source),contact,centralKeelFromTop:bottom,description:e.name==='regional_7'?'Four-mast slender flush-deck clipper; distinct artwork for ship11':e.description});
    }
    const resource=path.join(resources,entries[0].source);
    await sharp({create:{width,height,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite(layers).png().toFile(resource);
    configureMeta(resource+'.meta',group==='roster'?4096:8192,width,height);
    report.atlases.push({path:resource,width,height,sha256:hash(resource),import:'Standalone BC7 HQ, bilinear, authored alpha'});
  }
  const lookout=path.join(resources,'NavigationLookout/lookout_pirate_v1.png');
  if (!fs.existsSync(path.join(root,'originals','lookout_pirate_v1.png'))) {
    fs.copyFileSync(lookout,path.join(root,'originals','lookout_pirate_v1.png'));
    fs.copyFileSync(lookout+'.meta',path.join(root,'originals','lookout_pirate_v1.png.meta'));
  }
  fs.copyFileSync(path.join(root,'generated','roster_1.png'),lookout);
  configureMeta(lookout+'.meta',2048);
  report.lookout={path:lookout,sharedDesign:'roster_1 pirate flagship'};
  const layoutFile='D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/ShipWaterlineLayout.cs';
  let layout=fs.readFileSync(layoutFile,'utf8');
  for(const [group,name] of [['roster','RosterContacts'],['regional','RegionalContacts']]) {
    layout=layout.replace(new RegExp('(private static readonly float\\[\\] '+name+' = )\\{[^}]+\\};'),
      '$1{ '+contacts[group].map(n=>n.toFixed(4)+'f').join(', ')+' };');
  }
  fs.writeFileSync(layoutFile,layout);
  const names=['轻帆船 · 灰鸥','双桅商船 · 金潮','巡防舰 · 铜钟','遗迹猎船 · 月蚀','打捞船 · 拾潮','快帆邮船 · 白羽','补给船 · 丰穗','重炮舰 · 雷铸','远洋货驳 · 盐山','武装商船 · 誓金','雾行船 · 夜灯','飞剪商船 · 逐风'];
  const cells=[null,'roster_1','roster_2','roster_3','regional_2','regional_0','regional_1','regional_5','regional_6','regional_3','regional_4','regional_7'];
  const thumbs=[];
  for(let i=0;i<12;i++) {
    const source=i===0?manifest.starter:path.join(root,'generated',cells[i]+'.png');
    const picture=await sharp(source).resize({width:680,height:454,fit:'inside'}).png().toBuffer();
    const title=Buffer.from('<svg width="720" height="46"><text x="22" y="32" font-family="Microsoft YaHei, sans-serif" font-size="24" fill="#edcea0">'+xml(names[i])+'</text></svg>');
    thumbs.push({input:picture,left:(i%3)*720+20,top:Math.floor(i/3)*510+46});
    thumbs.push({input:title,left:(i%3)*720,top:Math.floor(i/3)*510});
  }
  await sharp({create:{width:2160,height:2040,channels:3,background:'#14242e'}}).composite(thumbs).png().toFile(path.join(root,'preview','all-player-ships.png'));
  fs.writeFileSync(path.join(root,'integration-report.json'),JSON.stringify(report,null,2));
  fs.writeFileSync(path.join(root,'contacts.json'),JSON.stringify(contacts,null,2));
  console.log(JSON.stringify({atlases:report.atlases,contacts,lookout:report.lookout},null,2));
})();
