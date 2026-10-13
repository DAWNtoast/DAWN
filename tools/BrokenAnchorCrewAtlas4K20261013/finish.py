import hashlib
import html
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT = Path('D:/Game/Artifacts/BrokenAnchorCrewAtlas4K20261013')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    metadata_path = OUT/'broken-anchor-six-crew-4k-v1.png.metadata.json'
    metadata = json.loads(metadata_path.read_text(encoding='utf-8-sig'))
    if not metadata['success']:
        raise RuntimeError('No successful image; this script never retries generation.')
    image_path = Path(metadata['output']['path'])
    manifest_path = OUT/'manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    (OUT/'portraits').mkdir(exist_ok=True)
    with Image.open(image_path) as image:
        w, h = image.size
        assert w >= 3800 and h >= 3800, 'Returned atlas is not square 4K class.'
        assert abs(w-h) <= 1
        xs = [round(w*i/3) for i in range(4)]
        ys = [round(h*i/2) for i in range(3)]
        for crew in manifest['crew']:
            col, row = crew['column'], crew['row']
            rect = (xs[col], ys[row], xs[col+1], ys[row+1])
            crop_path = OUT/'portraits'/(crew['id']+'_portrait_atlas_v1.png')
            cropped = image.crop(rect)
            cropped.save(crop_path, optimize=True)
            crew['outputCropXYXY'] = rect
            crew['output'] = str(crop_path)
            crew['outputSize'] = cropped.size
            crew['outputSha256'] = digest(crop_path)
        # A compact browsing preview only; the original atlas and portrait crops stay native.
        preview = image.copy()
        preview.thumbnail((1536, 1536), Image.Resampling.LANCZOS)
        preview.save(OUT/'preview.jpg', quality=94, subsampling=0)

    # Useful inspection sheet: originals at native crop resolution beside a generated
    # portrait reduced to the same height. This is a preview, not a runtime asset.
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 25)
    small_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
    comparisons = Image.new('RGB', (1584, 970), '#201c17')
    draw = ImageDraw.Draw(comparisons)
    for crew in manifest['crew']:
        col, row = crew['column'], crew['row']
        x, y = col*528, row*485
        draw.text((x+18, y+12), crew['name'], font=font, fill='#f4e8cc')
        draw.text((x+18, y+49), '原档案裁切', font=small_font, fill='#beaf94')
        draw.text((x+274, y+49), '六人图裁切', font=small_font, fill='#beaf94')
        with Image.open(crew['reference']) as before:
            before.thumbnail((244, 560), Image.Resampling.LANCZOS)
            comparisons.paste(before, (x+14, y+84))
        with Image.open(crew['output']) as after:
            after.thumbnail((244, 560), Image.Resampling.LANCZOS)
            comparisons.paste(after, (x+270, y+84))
    comparisons.save(OUT/'reference-comparison.jpg', quality=95, subsampling=0)
    manifest.update(dict(status='generated-awaiting-visual-review',
                         apiModel=metadata['model'], apiCallCount=1,
                         automaticRetries=False, atlas=str(image_path), atlasSize=[w,h],
                         atlasSha256=digest(image_path), originalApiBytesPreserved=True,
                         individualCropsResized=False, integratedIntoGame=False,
                         prompt=str(OUT/'prompt.txt'), metadata=str(metadata_path)))
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    cards = '\n'.join('<article><h2>'+html.escape(c['name'])+'</h2><img src="portraits/'+Path(c['output']).name+'"><p>'+str(c['outputSize'][0])+' × '+str(c['outputSize'][1])+' 原尺寸裁切</p></article>' for c in manifest['crew'])
    page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>断锚港 · 六人肖像样图</title>
<style>body{margin:32px auto;max-width:1440px;padding:0 24px;background:#171511;color:#ede1cb;font:16px system-ui}h1{font-size:30px}p{color:#bdaf97}img{display:block;max-width:100%;height:auto}a{color:#d9b977}section{display:grid;grid-template-columns:repeat(3,1fr);gap:24px}article{background:#241f18;padding:16px}h2{font-size:20px}.atlas{max-width:960px;margin:24px auto}@media(max-width:700px){section{grid-template-columns:1fr}}</style>
<h1>断锚港 · 六人肖像样图</h1><p>一张 4K 合图，三列两行。Nano Banana API 调用一次；六张单人图为本地原尺寸裁切。尚未替换游戏资源。</p>
<p>样图检查：六名人物顺序和主要道具对应，细节清晰；上排仍有旧档案罗盘和底边残留，下排左下也有罗盘碎片与弧形缺口；部分肩部贴近裁切边缘。当前适合审核人物与清晰度，作为独立无框素材仍需清理。</p>
<p><a href="ATLAS">查看原始 4K 合图</a> · <a href="reference-comparison.jpg">参考形象对照</a> · <a href="manifest.json">素材记录</a> · <a href="prompt.txt">生成提示词</a></p>
<img class="atlas" src="preview.jpg"><section>CARDS</section></html>'''
    (OUT/'index.html').write_text(page.replace('ATLAS', image_path.name).replace('CARDS', cards), encoding='utf-8')
    print(json.dumps(dict(atlas=str(image_path), dimensions=[w,h],
                          portraits=[c['outputSize'] for c in manifest['crew']],
                          sha256=manifest['atlasSha256']), indent=2))


if __name__ == '__main__':
    main()
