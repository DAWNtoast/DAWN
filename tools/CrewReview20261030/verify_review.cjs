/* Executes the shipped persistence script with an in-memory DOM/storage boundary.
 * No user browser storage is read or modified. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'outputs/crew-review-20261030');
const readJson = name => JSON.parse(fs.readFileSync(path.join(out,name),'utf8').replace(/^\uFEFF/,''));
const source = fs.readFileSync(path.join(out,'crew-review.js'),'utf8');
const html = fs.readFileSync(path.join(out,'全部船员审核.html'),'utf8');
const data = readJson('crew-data.json');
const prefix = 'shattered-sail:crew-review:v1:';
const recoveryPrefix = 'shattered-sail:crew-review-recovery:v1:';
const tests = [];

class Target {
  constructor() { this.handlers = new Map(); this.value = ''; this.checked = false; this.textContent = ''; this.classes = new Set(); this.classList = {toggle:(key,value)=>value?this.classes.add(key):this.classes.delete(key),contains:key=>this.classes.has(key)}; }
  addEventListener(name,callback) { if(!this.handlers.has(name)) this.handlers.set(name,[]); this.handlers.get(name).push(callback); }
  async emit(name,fields={}) { for(const callback of this.handlers.get(name)||[]) await callback({type:name,currentTarget:this,target:this,...fields}); }
  click() { this.emit('click'); }
  remove() {}
}
function makeStorage(entries=[]) {
  const values = new Map(entries);
  return {values, get length(){return values.size;},key:index=>[...values.keys()][index]??null,getItem:key=>values.get(key)??null,setItem(key,value){values.set(key,String(value));},removeItem:key=>values.delete(key)};
}
function boot(storage=makeStorage(),options={}) {
  const cards = data.crew.map((crew,index)=>{
    const card = new Target();
    card.dataset = {crewId:crew.id,crewNumber:String(index+1).padStart(2,'0'),crewName:crew.displayName};
    card.parts = new Map(['.review-checkbox','.review-notes','.review-crew-print'].map(key=>[key,new Target()]));
    card.querySelector = key=>card.parts.get(key)||null;
    return card;
  });
  const elements = new Map(['review-filter','review-progress','review-storage-status','export-reviews','import-reviews','import-reviews-file'].map(id=>[id,new Target()]));
  const window = new Target(); window.dispatchEvent = event=>window.emit(event.type,event);
  if(options.denied) Object.defineProperty(window,'localStorage',{get(){throw new Error('Storage blocked');}}); else window.localStorage=storage;
  const document = {querySelectorAll:selector=>{assert.equal(selector,'.crew-block[data-crew-id]');return cards;},getElementById:id=>elements.get(id),body:{dataset:{catalogDate:'2026-10-30'},appendChild(){}},activeElement:null,createElement:()=>new Target()};
  let exportedBlob;
  const context = vm.createContext({window,document,Blob,URL:{createObjectURL:blob=>{exportedBlob=blob;return 'blob:qa';},revokeObjectURL(){}},Event:class{constructor(type){this.type=type;}},setTimeout:()=>0});
  vm.runInContext(source,context,{filename:'crew-review.js'});
  return {cards,window,document,elements,storage, async change(index,reviewed,suggestion){const card=cards[index];card.querySelector('.review-checkbox').checked=reviewed;card.querySelector('.review-notes').value=suggestion;await card.querySelector('.review-notes').emit('input');await card.querySelector('.review-checkbox').emit('change');},async export(){await elements.get('export-reviews').emit('click');return JSON.parse(await exportedBlob.text());},async import(value){const raw=typeof value==='string'?value:JSON.stringify(value);const input=elements.get('import-reviews-file');input.files=[{size:Buffer.byteLength(raw),text:async()=>raw}];await input.emit('change');},status(){return elements.get('review-storage-status').textContent;}};
}
function record(crew,changes={}) {return {id:crew.id,name:crew.displayName,number:'01',reviewed:false,suggestion:'',updatedAt:null,...changes};}
function payload(crew) {return {format:'shattered-sail-crew-review',version:1,crew};}
async function test(name,action) {await action();tests.push({name,passed:true});}

(async()=>{
  await test('36 unique stable IDs, complete per-person controls and 6×6 roster',()=>{
    assert.equal(data.crew.length,36); assert.equal(new Set(data.crew.map(c=>c.id)).size,36);
    assert(data.crew.every(c=>/^crew_[a-z0-9_]+$/.test(c.id)));
    const ports=new Set(data.crew.map(c=>c.portName));assert.equal(ports.size,6);
    for(const port of ports) assert.equal(new Set(data.crew.filter(c=>c.portName===port).map(c=>c.professionId)).size,6);
    assert.equal((html.match(/class="crew-block"/g)||[]).length,36);
    assert.equal((html.match(/class="review-checkbox"/g)||[]).length,36);
    assert.equal((html.match(/class="review-notes"/g)||[]).length,36);
    for(const c of data.crew) assert(html.includes(`data-crew-id="${c.id}"`));
    assert(!source.includes('card-review'));assert(!source.includes('data-card'));assert(!source.includes('CardReview'));assert(!source.includes('data.cards'));
  });
  const storage=makeStorage(), app=boot(storage), first=data.crew[0];
  await test('checkbox and Unicode multiline suggestion persist in crew namespace',async()=>{
    await app.change(0,true,'第一条：立绘左眼。\n第二条：酒馆形象与背景核对。');
    const saved=JSON.parse(storage.getItem(prefix+first.id));assert.equal(saved.reviewed,true);assert.equal(saved.suggestion,'第一条：立绘左眼。\n第二条：酒馆形象与背景核对。');assert(saved.updatedAt);
    assert([...storage.values.keys()].every(k=>k.startsWith(prefix)));
  });
  await test('reload restores checkbox, exact text, progress and filter state',()=>{
    const restored=boot(storage);assert.equal(restored.cards[0].querySelector('.review-checkbox').checked,true);assert.equal(restored.cards[0].querySelector('.review-notes').value,JSON.parse(storage.getItem(prefix+first.id)).suggestion);
    assert(restored.elements.get('review-progress').textContent.includes('1 / 36'));
    restored.elements.get('review-filter').value='pending';assert.equal(restored.window.CrewReview.matches(restored.cards[0]),false);assert.equal(restored.window.CrewReview.matches(restored.cards[1]),true);
    restored.elements.get('review-filter').value='notes';assert.equal(restored.window.CrewReview.matches(restored.cards[0]),true);assert.equal(restored.window.CrewReview.matches(restored.cards[1]),false);
  });
  await test('export contains all 36 people, names and accurate totals',async()=>{
    const report=await app.export();assert.equal(report.format,'shattered-sail-crew-review');assert.equal(report.version,1);assert.equal(report.crew.length,36);assert.equal(report.summary.total,36);assert.equal(report.summary.reviewed,1);assert.equal(report.summary.withSuggestions,1);assert.equal(report.crew[0].name,first.displayName);assert(!Object.hasOwn(report,'cards'));
  });
  await test('import round trip restores records; conflicting suggestions append without loss',async()=>{
    const other=boot();await other.import(await app.export());assert.equal(other.cards[0].querySelector('.review-checkbox').checked,true);
    await other.change(0,true,'本地意见');await other.import(payload([record(first,{reviewed:true,suggestion:'导入意见'})]));const text=other.cards[0].querySelector('.review-notes').value;assert(text.includes('本地意见'));assert(text.includes('导入意见'));assert(other.status().includes('追加保留'));
    await other.import(payload([record(first,{reviewed:true,suggestion:'导入意见'})]));assert.equal(other.cards[0].querySelector('.review-notes').value,text);
  });
  await test('invalid JSON, duplicate IDs and invalid later rows cannot partially mutate records',async()=>{
    for(const input of ['{bad',payload([record(first),record(first)]),payload([record(first,{suggestion:'不应写入'}),{...record(data.crew[1]),reviewed:'bad'}])]){
      const before=[...storage.values];await app.import(input);assert.deepEqual([...storage.values],before);assert(app.status().includes('导入失败'));
    }
  });
  await test('malformed old record bytes remain intact, new text uses recovery key',async()=>{
    const broken='user-original: {broken';const local=makeStorage([[prefix+first.id,broken]]), recovered=boot(local);
    await recovered.change(0,true,'新建议');assert.equal(local.getItem(prefix+first.id),broken);assert.equal(JSON.parse(local.getItem(recoveryPrefix+first.id)).suggestion,'新建议');assert.equal(boot(local).cards[0].querySelector('.review-notes').value,'新建议');
    const report=await recovered.export();assert(report.storageRecovery.some(r=>r.raw===broken));
  });
  await test('disabled storage shows warning and export still retains in-memory work',async()=>{
    const denied=boot(makeStorage(),{denied:true});await denied.change(0,true,'浏览器禁止保存时保留建议');assert(denied.status().includes('未能保存'));const report=await denied.export();assert.equal(report.crew[0].suggestion,'浏览器禁止保存时保留建议');assert.equal(report.summary.reviewed,1);
  });
  await test('write quota failure warns and preserves an exportable record',async()=>{
    const local=makeStorage();local.setItem=()=>{throw new Error('QuotaExceededError');};const failed=boot(local);await failed.change(1,false,'额度不足时的建议');assert(failed.status().includes('未能保存'));assert.equal((await failed.export()).crew.find(c=>c.id===data.crew[1].id).suggestion,'额度不足时的建议');
  });
  await test('cross-tab edit while focused merges on blur without discarding local text',async()=>{
    const local=makeStorage(), focused=boot(local);await focused.change(0,false,'本窗口');const notes=focused.cards[0].querySelector('.review-notes');focused.document.activeElement=notes;
    const incoming=JSON.stringify(record(first,{suggestion:'另一窗口',reviewed:true,updatedAt:new Date().toISOString()}));local.setItem(prefix+first.id,incoming);await focused.window.emit('storage',{key:prefix+first.id,newValue:incoming});assert.equal(notes.value,'本窗口');await notes.emit('blur');assert(notes.value.includes('本窗口'));assert(notes.value.includes('另一窗口'));assert.equal(focused.cards[0].querySelector('.review-checkbox').checked,true);
  });
  await test('checkbox can be unchecked and remains unchecked after reload',async()=>{
    const local=makeStorage(), changed=boot(local);await changed.change(0,true,'');await changed.change(0,false,'');assert.equal(boot(local).cards[0].querySelector('.review-checkbox').checked,false);
  });
  await test('all local HTML asset references resolve and images have alt text',()=>{
    for(const match of html.matchAll(/(?:src|data-src)="([^"]+)"/g)){
      if(/^(?:https?:|data:|blob:)/.test(match[1])) continue;
      assert(fs.existsSync(path.join(out,match[1])),`Missing asset ${match[1]}`);
    }
    for(const image of html.matchAll(/<img\b[^>]*>/g)) assert(/\balt="[^"]*"/.test(image[0]),'Image lacks alt text');
  });
  let captureStatus='pending';
  if(fs.existsSync(path.join(out,'capture-manifest.json'))){
    await test('144 current game pages and six tavern scenes are mapped to all 36 crew',()=>{
      const manifest=readJson('capture-manifest.json');assert.equal(manifest.crew.length,36);assert.equal(manifest.ports.length,6);
      for(const crew of data.crew){const entry=manifest.crew.find(row=>row.id===crew.id);assert(entry,crew.id);assert.equal(entry.name,crew.displayName);assert.equal(entry.port,crew.portName);assert.deepEqual(entry.pages.map(p=>p.key).sort(),['background','skills','specialCards','story']);
        for(const image of [...entry.pages,{path:entry.tavernScenePath}]){const bytes=fs.readFileSync(path.join(out,image.path));assert.equal(bytes.toString('hex',0,8),'89504e470d0a1a0a');assert.equal(bytes.readUInt32BE(16),1920);assert.equal(bytes.readUInt32BE(20),1080);if(image.sha256)assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'),image.sha256);assert(html.includes(image.path),`HTML missing ${image.path}`);}
      }
      assert.equal((html.match(/class="game-shot"/g)||[]).length,144);captureStatus='passed';
    });
  }else if(!process.argv.includes('--allow-pending')) throw new Error('Capture manifest is required for final validation');
  const report={checkedAt:new Date().toISOString(),passed:true,testCount:tests.length,captureStatus,tests};
  fs.writeFileSync(path.join(out,'web-validation.json'),JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify({passed:true,testCount:tests.length,captureStatus},null,2));
})().catch(error=>{console.error(error);process.exitCode=1;});
