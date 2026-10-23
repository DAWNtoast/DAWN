from pathlib import Path
import json, hashlib, html, datetime
from collections import Counter
from PIL import Image, ImageOps, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
ITEMS = json.loads((ROOT / 'manifest.json').read_text(encoding='utf-8-sig'))
REVIEW = ROOT / 'review'
REVIEW.mkdir(exist_ok=True)
FONT = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 19)
SMALL = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 14)
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def thumb(p, size):
    with Image.open(p) as im:
        return ImageOps.pad(im.convert('RGB'), size, color='#211d18', method=Image.Resampling.LANCZOS)

plans={}
for manifest_file in sorted((ROOT/'manifests').glob('*.json'), key=lambda p: (p.name.endswith('-story-preparation.json'), p.name)):
    for entry in json.loads(manifest_file.read_text('utf-8-sig')):
        plans[entry['id']]=entry
records=[]
seen={}
for item in ITEMS:
    final=ROOT / 'final' / (item['id']+'.png')
    prompt=ROOT / 'prompts' / (item['id']+'.txt')
    rec={k:item[k] for k in ('id','name','port','slug','target','reference','before')}
    rec.update(final=str(final), prompt=str(prompt), errors=[], scene=plans.get(item['id'],{}).get('scene',''), story_basis=plans.get(item['id'],{}).get('story_basis',''))
    rec['backupIntact']=sha(item['before'])==item['before_sha256']
    rec['metaIntact']=sha(item['target']+'.meta')==item['meta_sha256']
    with Image.open(item['tavern']) as tavern, Image.open(item['reference']) as ref:
        crop=tavern.crop(item['crop_box']).convert('RGB')
        rec['referenceMatchesTavern']=crop.size==ref.size and crop.tobytes()==ref.convert('RGB').tobytes()
    if not rec['backupIntact']: rec['errors'].append('Original backup changed')
    if not rec['metaIntact']: rec['errors'].append('Unity meta changed')
    if not rec['referenceMatchesTavern']: rec['errors'].append('Reference differs from tavern crop')
    if final.exists():
        with Image.open(final) as image:
            image.load()
            rec['size']=list(image.size)
            rec['format']=image.format
        rec['sha256']=sha(final)
        rec['targetSha256']=sha(item['target'])
        with Image.open(final) as f, Image.open(item['target']) as t:
            rec['targetMatchesFinal']=f.size==t.size and f.convert('RGBA').tobytes()==t.convert('RGBA').tobytes()
        rec['redrawn']=rec['sha256']!=item['before_sha256']
        rec['promptSaved']=prompt.exists() and prompt.stat().st_size>100
        if not rec['targetMatchesFinal']: rec['errors'].append('Target does not match final')
        if not rec['redrawn']: rec['errors'].append('Portrait still matches old art')
        if not rec['promptSaved']: rec['errors'].append('Missing prompt')
        if min(rec['size'])<1000: rec['errors'].append('Resolution below 1000 px')
        if abs(rec['size'][0]/rec['size'][1]-.8)>.015: rec['errors'].append('Aspect ratio is not 4:5')
        if rec['sha256'] in seen: rec['errors'].append('Duplicate portrait: '+seen[rec['sha256']])
        seen[rec['sha256']]=item['id']
        rec['status']='pass' if not rec['errors'] else 'failed'
    else:
        rec['status']='pending'
    records.append(rec)

summary={'generatedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'total':len(records),'passed':sum(r['status']=='pass' for r in records),'pending':sum(r['status']=='pending' for r in records),'failed':sum(r['status']=='failed' for r in records),'tool':'built-in image_gen','records':records}
(REVIEW/'art-validation.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')

for slug in dict.fromkeys(r['slug'] for r in records):
    batch=[r for r in records if r['slug']==slug]
    sheet=Image.new('RGB',(900,6*390+45),'#211d18')
    draw=ImageDraw.Draw(sheet)
    for col,title in enumerate(['酒馆参考','原立绘','重绘立绘']):draw.text((col*300+18,10),title,font=FONT,fill='#f4d8aa')
    for row,r in enumerate(batch):
        y=45+row*390
        draw.text((18,y),r['name'],font=FONT,fill='#fff0da')
        for col,p in enumerate([r['reference'],r['before'],r['final']]):
            if Path(p).exists(): sheet.paste(thumb(p,(284,350)),(8+col*300,y+30))
    sheet.save(REVIEW/(slug+'_comparison.jpg'),quality=93)

overview=Image.new('RGB',(1620,2160),'#211d18')
draw=ImageDraw.Draw(overview)
for index,r in enumerate(records):
    x=(index%6)*270;y=(index//6)*360
    if Path(r['final']).exists():overview.paste(thumb(r['final'],(258,322)),(x+6,y+4))
    else:draw.text((x+85,y+145),'待重绘',font=FONT,fill='#aa9a82')
    draw.text((x+8,y+329),r['name'],font=SMALL,fill='#fff0da')
overview.save(REVIEW/'all-36-redrawn.jpg',quality=94)

cards=[]
for r in records:
    cols=[]
    for caption,key in [('酒馆参考','reference'),('原立绘','before'),('重绘立绘','final')]:
        p=Path(r[key]); rel='../'+p.relative_to(ROOT).as_posix()
        content=f'<a href="{rel}"><img loading="lazy" src="{rel}" alt="{html.escape(r["name"])} {caption}"></a>' if p.exists() else '<div class="pending">待完成</div>'
        cols.append(f'<figure>{content}<figcaption>{caption}</figcaption></figure>')
    scene_html='<p>'+html.escape(r['scene'])+'</p>' if r['scene'] else ''
    basis_html='<details><summary>故事依据</summary><p>'+html.escape(str(r['story_basis']))+'</p></details>' if r['story_basis'] else ''
    cards.append(f'<article data-port="{r["port"]}"><h2>{r["port"]} · {r["name"]}</h2>'+scene_html+basis_html+'<div class="compare">'+''.join(cols)+f'</div><p class="state">{r["status"]} · <a href="../prompts/{r["id"]}.txt">重绘提示词</a></p></article>')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>三十六位特殊船员 · 酒馆形象对照</title><style>body{margin:0;background:#171410;color:#f2e5d1;font:16px/1.6 system-ui}header,main{max-width:1120px;margin:auto;padding:24px}header{position:sticky;top:0;background:#171410f5;z-index:1;border-bottom:1px solid #55432b}h1{margin:0;font-size:26px}h2{font-size:20px}article{padding:10px 0 28px;border-bottom:1px solid #55432b}.compare{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}figure{margin:0}img,.pending{width:100%;aspect-ratio:4/5;object-fit:contain;background:#211d18}.pending{display:grid;place-items:center}figcaption{text-align:center;color:#ceb993}a{color:#edc077}select{padding:8px;background:#2b241a;color:#f2e5d1;border:1px solid #715c3b}.state{font-size:13px;color:#aa9a82}</style><header><h1>三十六位特殊船员 · 酒馆形象对照</h1><p>以酒馆实图锁定外貌，结合实际背景故事创作上半身叙事立绘。内置工具连续网络错误，本轮新增 0 张；洛恩为已有成品，已完成复核。其余角色的场景与提示词是待生成方案。点击图片查看原始尺寸。</p>SUMMARY<select id="port"><option value="">全部港口</option>OPTIONS</select> · <a href="all-36-redrawn.jpg">36 人进度总览</a> · <a href="art-validation.json">资源校验</a></header><main>CARDS</main><script>document.querySelector('#port').onchange=e=>document.querySelectorAll('article').forEach(a=>a.hidden=e.target.value&&a.dataset.port!==e.target.value)</script></html>'''
page=page.replace('SUMMARY',f'<p>已校验 {summary["passed"]} / 36 · 待完成 {summary["pending"]} · 失败 {summary["failed"]}</p>').replace('OPTIONS',''.join('<option>'+p+'</option>' for p in dict.fromkeys(r['port'] for r in records))).replace('CARDS',''.join(cards))
(REVIEW/'index.html').write_text(page,encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k!='records'},ensure_ascii=False))
for r in records:
    if r['errors']:print(r['id'],r['errors'])

