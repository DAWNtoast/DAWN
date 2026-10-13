"""Build a local, native-pixel review of the actual layered Unity captures.

Usage: python build-game-review.py <capture-directory> [review-directory]
Source screenshots stay unchanged; comparison crops use no resizing/sharpening.
"""
import hashlib
import html
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bc7_bytes(texture):
    return ((texture['width'] + 3) // 4) * ((texture['height'] + 3) // 4) * 16


def main():
    capture = Path(sys.argv[1]).resolve()
    report = json.loads((capture / 'report.json').read_text(encoding='utf-8-sig'))
    out = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else capture.parent / 'review'
    out.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 26)
    frames = {(f['width'], f['height'], f['profession'], f['variant']): f for f in report['frames']}
    people, details, source_hashes = [], [], {}
    for index in range(6):
        name = frames[(3840, 2160, index, 'original')]['crewName']
        people.append({'index': index, 'name': name})
        for width, height in ((3840, 2160), (1920, 1080)):
            a = frames[(width, height, index, 'original')]
            b = frames[(width, height, index, 'atlas')]
            paper = next(g for g in a['graphics'] if g['path'].endswith('Tavern Dossier Parchment'))
            rect = paper['screenRect']
            x, y, w, h = (rect[k] for k in ('x', 'y', 'width', 'height'))
            # The same left-column screen rectangle in A and B, including frame.
            # The original subject is baked into its paper painting, so its own
            # UI mesh does not exist. Never fit one portrait to the other.
            region = (max(0, round(x + w * .025)), max(0, round(height - y - h + h * .07)),
                      min(width, round(x + w * .34)), min(height, round(height - y - h + h * .96)))
            paths = [capture / f['file'] for f in (a, b)]
            hashes = [sha256(path) for path in paths]
            for f, path, digest in zip((a, b), paths, hashes):
                if digest != f['screenshotSha256']:
                    raise ValueError(f'Screenshot differs from capture report: {path}')
                source_hashes[f['file']] = digest
            with Image.open(paths[0]) as before, Image.open(paths[1]) as after:
                if before.size != (width, height) or after.size != (width, height):
                    raise ValueError('Capture PNG resolution mismatch')
                old, new = before.crop(region), after.crop(region)
                sheet = Image.new('RGB', (old.width * 2 + 32, old.height + 58), '#171511')
                sheet.paste(old, (0, 58))
                sheet.paste(new, (old.width + 32, 58))
                draw = ImageDraw.Draw(sheet)
                draw.text((12, 12), 'A · 原档案', font=font, fill='#efe2c9')
                draw.text((old.width + 44, 12), 'B · 新头像 + 独立框', font=font, fill='#efe2c9')
                filename = f'crew-{index}-native-detail-{width}x{height}.png'
                sheet.save(out / filename)
            details.append({'profession': index, 'width': width, 'height': height,
                            'regionTopLeftPixels': list(region), 'file': filename,
                            'outputPixels': list(sheet.size), 'layers': b.get('layers', {})})

    import os
    capture_href = Path(os.path.relpath(capture, out)).as_posix()
    atlas, border = report['atlas'], report['frameTexture']
    summary = {
        'status': report['status'], 'passedFrames': report['passedFrames'],
        'expectedFrames': report['expectedFrames'], 'passedPagePairs': report['passedPagePairs'],
        'expectedPagePairs': report['expectedPagePairs'], 'atlas': atlas, 'frameTexture': border,
        'bc7TextureDataBytesEstimate': {'atlas': bc7_bytes(atlas), 'sharedFrame': bc7_bytes(border),
                                      'combined': bc7_bytes(atlas) + bc7_bytes(border)},
        'memoryScope': 'BC7 数值仅按4×4压缩块估算单级纹理数据；runtimeBytes 是编辑器 Unity 资源内存，不是发行版显存实测。',
        'pixelScope': '所有局部对照均为相同屏幕区域的原像素裁切和无损PNG；网页整屏滑杆会适配宽度，已明确标注。',
        'sourceScreenshotsUnmodified': source_hashes, 'details': details,
    }
    (out / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    data = json.dumps({'people': people, 'capture': capture_href, 'details': details}, ensure_ascii=False).replace('</', '<\\/')
    frame_info = f"{border['width']}×{border['height']}"
    total_mib = (bc7_bytes(atlas) + bc7_bytes(border)) / 1048576
    page = r'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>断锚港 · 新头像与独立边框</title>
<style>*{box-sizing:border-box}body{margin:0;background:#171511;color:#efe2c9;font:16px/1.65 system-ui}main{max-width:1480px;margin:28px auto;padding:0 22px}h1{font-size:30px;margin-bottom:6px}h2{font-size:23px}p{color:#c6b99f}.controls{display:flex;flex-wrap:wrap;gap:14px;align-items:center;margin:20px 0}select,a{font:inherit;color:#e2bd76}select{border:1px solid #705b38;background:#2a241b;border-radius:6px;padding:8px 14px}.slider{position:relative;width:100%;aspect-ratio:16/9;overflow:hidden;background:#000}.slider img{position:absolute;width:100%;height:100%;object-fit:contain}.slider .new{clip-path:inset(0 0 0 50%)}.line{position:absolute;left:50%;height:100%;border-left:2px solid #fff9;pointer-events:none}.label{position:absolute;top:10px;background:#14110dd9;padding:6px 12px;border-radius:4px}.right{right:10px}.left{left:10px}input[type=range]{width:100%;accent-color:#d8b56f}.native{overflow:auto;border:1px solid #705b38;max-height:1450px;background:#0d0c09}.native img{display:block;max-width:none;width:auto;height:auto}.notice{border-left:3px solid #b99455;padding:10px 18px;background:#251f16}code{font-size:14px}small{color:#b5a891}</style>
<main><h1>断锚港 · 新头像与独立边框</h1><p>同一游戏 UI、同一角色和正文。A 为原档案，B 为本轮新制六人清晰头像与独立框纸的分层组合。</p>
<div class="notice">六名船员共用一张 4096×4096 人物图，以及一张 FRAME_SIZE 框纸；两层独立渲染。源图保持原生分辨率，使用 BC7 且关闭 mipmap。人物按真实游戏布局显示；报告分别记录完整矩形与遮罩后的可见区域。</div>
<div class="controls"><label>船员 <select id="crew"></select></label><label>分辨率 <select id="resolution"><option value="3840x2160">4K · 3840×2160</option><option value="1920x1080">1080p · 1920×1080</option></select></label><a id="oldLink" target="_blank">打开 A 原尺寸截图</a><a id="newLink" target="_blank">打开 B 原尺寸截图</a></div>
<h2>整屏布局对照</h2><p>此滑杆画面会适配页面宽度，用于检查布局和构图；清晰度请看下面的原像素局部或打开原图。</p>
<div class="slider"><img id="old"><img id="new" class="new"><div class="line" id="line"></div><span class="label left">A · 原档案</span><span class="label right">B · 新头像 + 独立框</span></div><input id="range" type="range" min="0" max="100" value="50" aria-label="原档案与分层组合分界">
<h2>原像素局部对照</h2><p>左右均截取同一屏幕区域，无缩放、锐化或重新压缩。下方保持图片原生尺寸，可横向和纵向滚动查看。<a id="detailLink" target="_blank">打开完整对照 PNG</a></p><div class="native"><img id="detail" alt="相同屏幕区域的原像素并排对照"></div><p id="geometry"></p>
<p>验证状态：STATUS。FRAMES/24 张截图、PAIRS/48 组页面切换检查。检查 A/B 切换前后角色、正文、金币、页码和 UI 节点保持一致，以及人物、边框使用独立纹理和正确着色器。<a id="reportLink">完整 Unity 验证记录</a> · <a href="summary.json">对照摘要</a></p>
<p>两张共享纹理的单级 BC7 数据合计约 TOTAL_MIB MiB（含共享框约 FRAME_MIB MiB），这是压缩纹理数据估算。报告中的 <code>runtimeBytes</code> 是编辑器 Unity 资源内存，不能作为发行版显存实测。</p></main>
<script>const data=DATA,crew=document.getElementById('crew'),resolution=document.getElementById('resolution');data.people.forEach(p=>crew.add(new Option(p.name,p.index)));const rect=r=>r?Math.round(r.width*100)/100+'×'+Math.round(r.height*100)/100:'未记录';function update(){const prefix=data.capture+'/crew-'+crew.value+'-',a=prefix+'original-'+resolution.value+'.png',b=prefix+'atlas-'+resolution.value+'.png';document.getElementById('old').src=a;document.getElementById('new').src=b;document.getElementById('oldLink').href=a;document.getElementById('newLink').href=b;const entry=data.details.find(d=>d.profession===Number(crew.value)&&d.width===Number(resolution.value.split('x')[0]));document.getElementById('detail').src=entry.file;document.getElementById('detailLink').href=entry.file;const l=entry.layers;document.getElementById('geometry').textContent='B 人物完整矩形 '+rect(l.portraitRawRect)+' px；遮罩后可见 '+rect(l.portraitVisibleRect)+' px；独立框可见 '+rect(l.frameVisibleRect)+' px。';document.getElementById('reportLink').href=data.capture+'/report.json';}crew.onchange=resolution.onchange=update;document.getElementById('range').oninput=e=>{document.getElementById('new').style.clipPath='inset(0 0 0 '+e.target.value+'%)';document.getElementById('line').style.left=e.target.value+'%';};update();</script></html>'''
    replacements = {'FRAME_SIZE': frame_info, 'FRAME_MIB': f'{bc7_bytes(border)/1048576:.2f}',
                    'TOTAL_MIB': f'{total_mib:.2f}', 'STATUS': html.escape(report['status']),
                    'FRAMES': str(report['passedFrames']), 'PAIRS': str(report['passedPagePairs'])}
    for token, value in replacements.items():
        page = page.replace(token, value)
    page = page.replace('DATA', data)
    (out / 'index.html').write_text(page, encoding='utf-8')
    print(out / 'index.html')


if __name__ == '__main__':
    main()
