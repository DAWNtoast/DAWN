import html
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

capture = Path(sys.argv[1]).resolve()
report = json.loads((capture / 'report.json').read_text(encoding='utf-8-sig'))
out = capture.parent / 'review'
out.mkdir(exist_ok=True)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 28)
frames = {(f['width'], f['profession'], f['variant']): f for f in report['frames']}
crew = []
for index in range(6):
    pair = {}
    for mode in ('original', 'atlas'):
        frame = frames[(3840, index, mode)]
        pair[mode] = frame
    with Image.open(capture/pair['original']['file']) as before, Image.open(capture/pair['atlas']['file']) as after:
        # Identical screen region, identical native pixels. Do not enlarge the
        # smaller fitted portrait to manufacture a same-size quality comparison.
        paper = next(g for g in pair['original']['graphics'] if g['path'].endswith('Tavern Dossier Parchment'))
        r = paper['screenRect']
        x, y, w, h = r['x'], r['y'], r['width'], r['height']
        region = (round(x+w*.035), round(2160-y-h+h*.08),
                  round(x+w*.325), round(2160-y-h+h*.94))
        a, b = before.crop(region), after.crop(region)
        sheet = Image.new('RGB', (a.width*2+32, a.height+62), '#171511')
        sheet.paste(a,(0,62)); sheet.paste(b,(a.width+32,62))
        d=ImageDraw.Draw(sheet)
        d.text((18,14),pair['original']['crewName']+' · 原档案',font=font,fill='#efe2c9')
        d.text((a.width+50,14),'六人图 · 同一屏幕区域',font=font,fill='#efe2c9')
        sheet.save(out/f'crew-{index}-native-detail.jpg',quality=96,subsampling=0)
        if index==0:
            before.thumbnail((1920,1080),Image.Resampling.LANCZOS)
            after.thumbnail((1920,1080),Image.Resampling.LANCZOS)
            before.save(out/'original-preview.jpg',quality=94,subsampling=0)
            after.save(out/'atlas-preview.jpg',quality=94,subsampling=0)
    crew.append({'index':index,'name':pair['original']['crewName']})

data = json.dumps(crew,ensure_ascii=False).replace('</','<\\/')
page = r'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>断锚港 · 游戏内六人图对比</title>
<style>*{box-sizing:border-box}body{margin:0;background:#171511;color:#efe2c9;font:16px/1.65 system-ui}main{max-width:1480px;margin:28px auto;padding:0 22px}h1{font-size:30px;margin-bottom:6px}p{color:#c6b99f}.controls{display:flex;flex-wrap:wrap;gap:14px;align-items:center;margin:20px 0}select,button,a.btn{font:inherit;border:1px solid #705b38;background:#2a241b;color:#efe2c9;border-radius:6px;padding:8px 14px}a{color:#e2bd76}figure{margin:16px 0}.slider{position:relative;width:100%;aspect-ratio:16/9;overflow:hidden;background:#000}.slider img{position:absolute;width:100%;height:100%;object-fit:contain}.slider .new{clip-path:inset(0 0 0 50%)}.line{position:absolute;left:50%;height:100%;border-left:2px solid #fff9;pointer-events:none}.label{position:absolute;top:10px;background:#14110dd9;padding:6px 12px;border-radius:4px}.label.right{right:10px}.label.left{left:10px}input[type=range]{width:100%;accent-color:#d8b56f}.detail{width:100%;height:auto}.notice{border-left:3px solid #b99455;padding:10px 18px;background:#251f16}small{color:#b5a891}</style>
<main><h1>断锚港 · 游戏内六人图对比</h1><p>同一生产 UI、同一角色和正文；原档案与六人图方案原地切换。拖动滑杆查看画面差异。</p>
<div class="notice">新方案复用现有空白纸页并保持人物比例。人物在框内的显示尺寸和原画不同；对照保留真实显示尺寸。六人图仍有罗盘碎片、底边和背景融合问题，因此本页验证的是方案可行性，尚不等于最终美术验收通过。</div>
<div class="controls"><label>船员 <select id="crew"></select></label><label>分辨率 <select id="resolution"><option value="3840x2160">4K · 3840×2160</option><option value="1920x1080">1080p · 1920×1080</option></select></label><a id="oldLink" target="_blank">打开原档案原尺寸截图</a><a id="newLink" target="_blank">打开六人图原尺寸截图</a></div>
<figure><div class="slider"><img id="old"><img id="new" class="new"><div class="line" id="line"></div><span class="label left">原档案</span><span class="label right">六人图</span></div><input id="range" type="range" min="0" max="100" value="50" aria-label="原档案与六人图分界"></figure>
<h2>4K 原尺寸局部</h2><p>截取两张画面的相同屏幕区域，未放大较小的头像。打开图片可按 100% 像素查看。</p><a id="detailLink" target="_blank"><img class="detail" id="detail"></a>
<p>检查结果：FRAMES/24 张截图，PAIRS/48 组翻页与切换检查。切换前后保持人物、正文、页码、招募状态和金币。<a href="../capture/report.json">完整验证记录</a></p><p>本轮未调用出图 API。4K 合图使用 BC7、关闭 mipmap，六名人物共用一张纹理；A/B 试验保留旧资源，因此不将本次运行内存解释为最终发行内存。</p></main>
<script>const people=DATA;const crew=document.getElementById('crew'),resolution=document.getElementById('resolution');people.forEach(p=>crew.add(new Option(p.name,p.index)));function update(){const prefix='../capture/crew-'+crew.value+'-';const a=prefix+'original-'+resolution.value+'.png',b=prefix+'atlas-'+resolution.value+'.png';document.getElementById('old').src=a;document.getElementById('new').src=b;document.getElementById('oldLink').href=a;document.getElementById('newLink').href=b;const detail='crew-'+crew.value+'-native-detail.jpg';document.getElementById('detail').src=detail;document.getElementById('detailLink').href=detail;}crew.onchange=resolution.onchange=update;document.getElementById('range').oninput=e=>{document.getElementById('new').style.clipPath='inset(0 0 0 '+e.target.value+'%)';document.getElementById('line').style.left=e.target.value+'%';};update();</script></html>'''
page=page.replace('DATA',data).replace('FRAMES',str(report['passedFrames'])).replace('PAIRS',str(report['passedPagePairs']))
(out/'index.html').write_text(page,encoding='utf-8')
print(out/'index.html')
