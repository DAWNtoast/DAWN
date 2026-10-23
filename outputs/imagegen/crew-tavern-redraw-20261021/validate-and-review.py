from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont
import json, hashlib, html, datetime, argparse

ROOT = Path(__file__).resolve().parent
REVIEW = ROOT / 'review'
REVIEW.mkdir(exist_ok=True)
ITEMS = json.loads((ROOT / 'manifest.json').read_text('utf-8-sig'))
FONT = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 20)
SMALL = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 15)

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def thumb(p, size):
    with Image.open(p) as im:
        return ImageOps.pad(im.convert('RGB'), size, color='#211d18', method=Image.Resampling.LANCZOS)

parser = argparse.ArgumentParser()
parser.add_argument('--integrate', action='store_true')
args = parser.parse_args()
plans = {}
for path in (ROOT / 'manifests').glob('*.json'):
    data = json.loads(path.read_text('utf-8-sig'))
    if isinstance(data, dict): data = [data]
    for entry in data:
        plans.setdefault(entry['id'], {}).update({k:v for k,v in entry.items() if v is not None and v != ''})
records, seen = [], {}
for item in ITEMS:
    final = ROOT / 'final' / (item['id'] + '.png')
    prompt = ROOT / 'prompts' / (item['id'] + '.txt')
    rec = {k: item[k] for k in ('id', 'name', 'port', 'slug', 'target', 'reference', 'before')}
    plan = plans.get(item['id'], {})
    rec.update(final=str(final), prompt=str(prompt), errors=[], scene=plan.get('scene', ''), story_basis=plan.get('story_basis', ''))
    rec['backupIntact'] = sha(item['before']) == item['before_sha256']
    rec['metaIntact'] = sha(item['target'] + '.meta') == item['meta_sha256']
    with Image.open(item['tavern']) as tavern, Image.open(item['reference']) as reference:
        crop = tavern.crop(item['crop_box']).convert('RGB')
        rec['referenceMatchesTavern'] = crop.size == reference.size and crop.tobytes() == reference.convert('RGB').tobytes()
    for key in ('backupIntact', 'metaIntact', 'referenceMatchesTavern'):
        if not rec[key]: rec['errors'].append(key + ' failed')
    if final.exists():
        with Image.open(final) as im:
            im.load()
            rec['size'] = list(im.size)
            rec['format'] = im.format
        rec['sha256'] = sha(final)
        if rec['sha256'] == item['before_sha256']: rec['errors'].append('Unchanged old portrait')
        if min(rec['size']) < 1000: rec['errors'].append('Resolution below 1000px')
        if abs(rec['size'][0] / rec['size'][1] - .8) > .015: rec['errors'].append('Aspect ratio is not 4:5')
        if rec['sha256'] in seen: rec['errors'].append('Duplicate: ' + seen[rec['sha256']])
        seen[rec['sha256']] = item['id']
        if not prompt.exists() or prompt.stat().st_size < 100: rec['errors'].append('Missing actual prompt')
        rec['status'] = 'failed' if rec['errors'] else 'ready'
    else: rec['status'] = 'pending'
    records.append(rec)

if args.integrate:
    if len(records) != 36 or any(r['status'] != 'ready' for r in records):
        raise RuntimeError('All 36 checked images required before integration')
    import shutil
    for rec in records:
        shutil.copyfile(rec['final'], rec['target'])
        assert sha(rec['final']) == sha(rec['target'])

for rec in records:
    rec['targetMatchesFinal'] = Path(rec['final']).exists() and sha(rec['final']) == sha(rec['target'])
    if rec['status'] == 'ready' and rec['targetMatchesFinal']: rec['status'] = 'integrated'
summary = dict(generatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), total=len(records), generated=sum(r['status'] in ('ready', 'integrated') for r in records), integrated=sum(r['status']=='integrated' for r in records), pending=sum(r['status']=='pending' for r in records), failed=sum(r['status']=='failed' for r in records), tool='built-in image_gen', records=records)
(REVIEW / 'art-validation.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')

for slug in dict.fromkeys(r['slug'] for r in records):
    batch = [r for r in records if r['slug'] == slug]
    sheet = Image.new('RGB', (1200, 6 * 510 + 50), '#211d18')
    draw = ImageDraw.Draw(sheet)
    for col, title in enumerate(['酒馆身份参考', '重绘故事立绘', '游戏窗口裁切预览']): draw.text((col*400+16, 12), title, font=FONT, fill='#f4d8aa')
    for row, rec in enumerate(batch):
        y = 50 + row * 510
        draw.text((16, y), rec['name'], font=FONT, fill='#fff0da')
        sheet.paste(thumb(rec['reference'], (384, 470)), (8, y+32))
        if Path(rec['final']).exists():
            sheet.paste(thumb(rec['final'], (384, 470)), (408, y+32))
            with Image.open(rec['final']) as im:
                # Production window: width 411.3 / height 486.8; cover, top aligned.
                h = round(im.width * 486.8 / 411.3)
                cropped = im.crop((0, 0, im.width, min(im.height, h))).convert('RGB')
                sheet.paste(ImageOps.pad(cropped, (384, 470), color='#211d18'), (808, y+32))
    sheet.save(REVIEW / (slug + '_comparison.jpg'), quality=94)

overview = Image.new('RGB', (1800, 2400), '#211d18')
draw = ImageDraw.Draw(overview)
for i, rec in enumerate(records):
    x, y = (i%6)*300, (i//6)*400
    if Path(rec['final']).exists(): overview.paste(thumb(rec['final'], (288, 360)), (x+6, y+5))
    else: draw.text((x+95, y+160), '待重绘', font=FONT, fill='#aa9a82')
    draw.text((x+12, y+373), rec['name'], font=SMALL, fill='#fff0da')
overview.save(REVIEW / 'all-36-overview.jpg', quality=95)

cards = []
for rec in records:
    e = html.escape
    image = f'<img src="../final/{e(rec["id"])}.png">' if Path(rec['final']).exists() else '<p>待生成</p>'
    cards.append(f'<article><h2>{e(rec["name"])}</h2><p>{e(rec["port"])} · {e(rec["status"])}</p><div class="pair"><img src="../references/{e(rec["id"])}_tavern.png">{image}</div><p>{e(rec["scene"])}</p><details><summary>故事依据与提示词</summary><p>{e(rec["story_basis"])}</p><a href="../prompts/{e(rec["id"])}.txt">实际生成提示词</a></details></article>')
page = '<!doctype html><html lang="zh"><meta charset="utf-8"><title>特殊船员立绘 · 酒馆形象与故事</title><style>body{margin:28px;background:#171715;color:#eee5d7;font-family:system-ui}h1{font-size:26px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(500px,1fr));gap:24px}article{background:#24231e;padding:20px;border-radius:12px}h2{font-size:21px}.pair{display:flex;gap:12px}.pair img{width:48%;height:460px;object-fit:contain;background:#141411}p{line-height:1.65}a{color:#e6c187}summary{cursor:pointer}</style>'
page += f'<h1>三十六位特殊船员 · 酒馆身份与故事立绘</h1><p>2026-10-21 · 内置图像工具 · 已生成 {summary["generated"]}/36 · 已接入 {summary["integrated"]}/36。每组左侧为酒馆参考，右侧为新立绘。</p><main>' + ''.join(cards) + '</main></html>'
(REVIEW / 'index.html').write_text(page, encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k != 'records'}, ensure_ascii=False))
