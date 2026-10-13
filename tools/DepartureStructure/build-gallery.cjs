const fs = require('fs');
const path = require('path');
const root = 'D:/Game/Artifacts/DepartureStructure20261012';
const capture = process.argv[2] || 'ui-v1';
const report = JSON.parse(fs.readFileSync(path.join(root, 'validation', capture, 'report.json'), 'utf8'));
const states = {Ordinary:'普通航线', Chart:'已绘海图', InvalidChart:'无效海图', Treasure:'藏宝航线', Crew:'船员配置', BasicSupplies:'基础物资'};
const items = report.pages.map(p => ({...p, label:`${p.port} · ${states[p.state]} · ${p.width}×${p.height}`, url:`validation/${capture}/${p.file}`}));
const html = `<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>出航码头 · 结构优化</title>
<style>*{box-sizing:border-box}body{margin:0;background:#101b20;color:#efe8d6;font:16px/1.6 system-ui,"Microsoft Yahei",sans-serif}header{padding:28px 4vw 16px;border-bottom:1px solid #41514b}h1{margin:0 0 6px;font-size:28px}p{margin:6px 0;color:#c4c8bb}main{padding:20px 3vw}nav{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:16px}button{background:#263a3c;color:#ead8b3;border:1px solid #6e7157;border-radius:5px;padding:8px 12px;font:inherit;cursor:pointer}button[aria-pressed=true]{background:#d4b27b;color:#1b2a28}figure{margin:0}img{display:block;width:100%;height:auto;border:1px solid #5b674e;border-radius:5px}figcaption{padding:10px 0;color:#d5bd94}aside{margin-top:20px;border-top:1px solid #41514b;padding-top:18px}a{color:#e0c391}small{color:#9daaa6}</style>
<header><h1>出航码头 · 结构优化</h1><p>统一浅纸阅读区，收纳海图入口，放大海域插画与旗舰，整理整备操作。</p><small>以下均为 Unity 实际界面捕获。包含六港与已绘海图、藏宝、船员、补给状态。</small></header>
<main><nav id="ports" aria-label="预览状态"></nav><figure><a id="full" target="_blank"><img id="frame" alt="出航码头实际界面"></a><figcaption id="caption"></figcaption></figure><aside><a href="../DepartureArtRedraw20261012/index.html">查看上一阶段：六港美术与原布局</a> · <a href="validation/${capture}/report.json">界面检查记录</a></aside></main>
<script>const items=${JSON.stringify(items)};const nav=document.querySelector('#ports');items.forEach((p,i)=>{const b=document.createElement('button');b.textContent=p.label;b.onclick=()=>show(i);nav.append(b)});function show(i){const p=items[i];document.querySelector('#frame').src=p.url;document.querySelector('#frame').alt=p.label;document.querySelector('#full').href=p.url;document.querySelector('#caption').textContent=p.label;[...nav.children].forEach((b,n)=>b.setAttribute('aria-pressed',n===i));}show(0);</script></html>`;
fs.writeFileSync(path.join(root, 'index.html'), html);
console.log(`Gallery written: ${items.length} captures; report passed=${report.passed}`);
