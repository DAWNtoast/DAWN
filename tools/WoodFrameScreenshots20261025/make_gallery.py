import json, math, html, shutil
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(r'D:\Game\outputs\wood-frame-screenshots-20261025')
frames={}
for report in sorted(root.glob('run*/report.json')):
    for f in json.loads(report.read_text(encoding='utf-8-sig'))['frames']:
        if not f.get('error') and (report.parent/f['file']).exists():
            f['source']=str((report.parent/f['file']).relative_to(root)).replace('\\','/')
            frames[f['index']]=f
frames=[frames[k] for k in sorted(frames)]
thumbs=root/'thumbnails'; thumbs.mkdir(exist_ok=True)
font=ImageFont.truetype(r'C:\Windows\Fonts\msyh.ttc',18)
small=ImageFont.truetype(r'C:\Windows\Fonts\msyh.ttc',16)
for f in frames:
    with Image.open(root/f['source']) as im:
        assert im.size==(1920,1080), (f['file'],im.size)
        im.thumbnail((480,270))
        im.convert('RGB').save(thumbs/(str(f['index']).zfill(2)+'.jpg'),quality=88)
for batch in range(math.ceil(len(frames)/12)):
    group=frames[batch*12:(batch+1)*12]
    sheet=Image.new('RGB',(1488,math.ceil(len(group)/3)*314+54),'#14212b')
    d=ImageDraw.Draw(sheet)
    d.text((16,12),f'木板底框界面截图 · {group[0]["index"]:02d}—{group[-1]["index"]:02d}',font=font,fill='#ead8af')
    for slot,f in enumerate(group):
        x=16+(slot%3)*492; y=50+(slot//3)*314
        with Image.open(thumbs/(str(f['index']).zfill(2)+'.jpg')) as im: sheet.paste(im,(x,y))
        d.text((x,y+277),f'{f["index"]:02d} {f["title"]}',font=small,fill='white')
    sheet.save(root/f'总览_{batch+1:02d}.jpg',quality=92)
data=json.dumps(frames,ensure_ascii=False).replace('</',r'<\/')
template='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>木板底框界面截图</title><style>
*{box-sizing:border-box}body{margin:0;background:#101922;color:#eee;font:16px system-ui,"Microsoft YaHei",sans-serif}header{padding:24px 4vw 12px}h1{font-size:26px;margin:0 0 10px}p{color:#b7c1c9}button,a{color:#f0d69e}button{border:1px solid #635541;background:#26303b;padding:10px 20px;border-radius:6px;cursor:pointer}button:disabled{opacity:.4;cursor:default}main{max-width:1700px;margin:auto;padding:0 4vw}#viewer{width:100%;display:block;background:black}#controls{position:sticky;top:0;z-index:2;display:flex;align-items:center;gap:16px;padding:12px 0;background:#101922;flex-wrap:wrap}#title{flex:1;min-width:250px;font-size:20px}#grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:18px;margin:30px 0}.tile{display:block;color:#eee;text-decoration:none;border:1px solid #36424e;border-radius:5px;overflow:hidden;background:#1d2832}.tile img{display:block;width:100%}.tile span{display:block;padding:10px;font-size:14px}.tile.active{border:2px solid #e0ba72}footer{padding:24px 0;color:#a8b1ba;font-size:14px}details{margin-top:14px;color:#adb8c3}summary{cursor:pointer}
</style><header><h1>木板底框界面截图</h1><p>按界面依次编号 · COUNT 张原图 · 1920 × 1080 · 点击目录或使用左右方向键切换</p></header><main><div id="controls"><button id="prev">← 上一张</button><div id="title"></div><button id="next">下一张 →</button><a id="original" target="_blank">打开原图</a></div><img id="viewer" alt="界面截图"><details><summary>截图说明</summary><p>采用当前项目代码渲染的独立界面预览；弹窗与游戏状态使用内存样本，未改动玩家进度。旧维修补给板已无现役入口，对应当前补给请看出航基础补给。GM页使用真实界面构建代码，仅在隔离工程开放编辑预览入口。</p></details><div id="grid"></div><footer>截图日期：2026-10-25。原始PNG、截图清单和核验记录保存在同一目录。</footer></main><script>const frames=DATA;let current=0;const grid=document.querySelector('#grid');frames.forEach((f,i)=>{let a=document.createElement('a');a.className='tile';a.href='#'+f.index;a.innerHTML='<img loading="lazy" src="thumbnails/'+String(f.index).padStart(2,'0')+'.jpg"><span>'+String(f.index).padStart(2,'0')+' · '+f.title+'</span>';a.onclick=e=>{e.preventDefault();show(i);window.scrollTo({top:0,behavior:'smooth'})};grid.append(a)});function show(i){current=Math.max(0,Math.min(frames.length-1,i));let f=frames[current];document.querySelector('#viewer').src=f.source;document.querySelector('#viewer').alt=f.title;document.querySelector('#title').textContent=String(f.index).padStart(2,'0')+' · '+f.title;document.querySelector('#original').href=f.source;document.querySelector('#prev').disabled=current===0;document.querySelector('#next').disabled=current===frames.length-1;grid.querySelectorAll('.tile').forEach((t,k)=>t.classList.toggle('active',k===current));history.replaceState(null,'','#'+f.index)}document.querySelector('#prev').onclick=()=>show(current-1);document.querySelector('#next').onclick=()=>show(current+1);document.addEventListener('keydown',e=>{if(e.key==='ArrowLeft')show(current-1);if(e.key==='ArrowRight')show(current+1)});show(Math.max(0,frames.findIndex(f=>f.index===Number(location.hash.slice(1)))));</script></html>'''
(root/'index.html').write_text(template.replace('COUNT',str(len(frames))).replace('DATA',data),encoding='utf-8')
(root/'截图清单.json').write_text(json.dumps(frames,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'count':len(frames),'missing':[i for i in range(1,51) if i not in {f['index'] for f in frames}],'gallery':str(root/'index.html')},ensure_ascii=False))
