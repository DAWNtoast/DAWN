"""Render the game's exported UI meshes, assemble a self-contained inspection atlas."""
from __future__ import annotations

import base64
import hashlib
import io
import json
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path('D:/Game')
OUT = ROOT / 'outputs/battle-icon-atlas-20261022'
TOOLS = ROOT / 'tools/BattleIconAtlas20261022'
INLINE = Path('C:/Users/Admin/.codex/visualizations/2026/10/22/01a16380-9892-7e32-83e4-7f4f1f33b83f/battle-icon-atlas.html')


def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def rasterize(mesh, size=192, samples=3):
    """Rasterize original triangles, including interpolated vertex colors and alpha."""
    width, height = float(mesh['width']), float(mesh['height'])
    pixels = size * samples
    scale = min(pixels / width, pixels / height)
    offset = np.array([(pixels-width*scale)/2, (pixels-height*scale)/2], dtype=np.float32)
    vertices = np.asarray(mesh['vertices'], dtype=np.float32)
    positions = vertices[:, :2] * scale + offset
    colors = np.clip(vertices[:, 2:6]/255, 0, 1)
    result = np.zeros((pixels, pixels, 4), dtype=np.float32)
    triangles = np.asarray(mesh['triangles'], dtype=np.int64).reshape(-1, 3)
    for indices in triangles:
        p = positions[indices].copy()
        c = colors[indices].copy()
        area = float((p[1,0]-p[0,0])*(p[2,1]-p[0,1])-(p[1,1]-p[0,1])*(p[2,0]-p[0,0]))
        if abs(area) < 1e-8:
            continue
        if area < 0:
            p[[1,2]] = p[[2,1]]
            c[[1,2]] = c[[2,1]]
            area = -area
        left = max(0, int(np.floor(p[:,0].min())))
        right = min(pixels, int(np.ceil(p[:,0].max())))
        top = max(0, int(np.floor(p[:,1].min())))
        bottom = min(pixels, int(np.ceil(p[:,1].max())))
        if right <= left or bottom <= top:
            continue
        yy, xx = np.mgrid[top:bottom, left:right].astype(np.float32)
        xx += .5
        yy += .5
        weights, inside = [], np.ones(xx.shape, dtype=bool)
        for a,b in ((1,2),(2,0),(0,1)):
            dx,dy = p[b]-p[a]
            edge = dx*(yy-p[a,1])-dy*(xx-p[a,0])
            include = bool(dy < 0 or (abs(float(dy)) < 1e-8 and dx > 0))
            inside &= (edge > 1e-6) | ((np.abs(edge) <= 1e-6) & include)
            weights.append(edge/area)
        if not inside.any():
            continue
        color = sum(weights[i][...,None]*c[i] for i in range(3))[inside]
        color = np.clip(color,0,1)
        alpha = color[:,3:4]
        region = result[top:bottom,left:right]
        old = region[inside]
        old[:,:3] = color[:,:3]*alpha + old[:,:3]*(1-alpha)
        old[:,3:4] = alpha + old[:,3:4]*(1-alpha)
        region[inside] = old
    alpha = result[:,:,3:4]
    result[:,:,:3] = np.divide(result[:,:,:3], alpha, out=np.zeros_like(result[:,:,:3]), where=alpha>1e-8)
    rgba = np.uint8(np.clip(np.round(result*255),0,255))
    return Image.fromarray(rgba).resize((size,size),Image.Resampling.LANCZOS)


def fit_image(source, size=192, pad=10):
    image=source.convert('RGBA')
    image.thumbnail((size-pad*2,size-pad*2),Image.Resampling.LANCZOS)
    output=Image.new('RGBA',(size,size))
    output.alpha_composite(image,((size-image.width)//2,(size-image.height)//2))
    return output


def card_stack(front):
    box=front.getbbox()
    image=front.crop(box)
    image.thumbnail((130,172),Image.Resampling.LANCZOS)
    result=Image.new('RGBA',(192,192))
    faded=np.asarray(image).copy()
    faded[:,:,:3]=np.uint8(faded[:,:,:3]*np.array([.72,.76,.8]))
    back=Image.fromarray(faded)
    x,y=(192-image.width)//2+4,(192-image.height)//2+4
    result.alpha_composite(back,(x-8,y-8))
    result.alpha_composite(back,(x-4,y-4))
    result.alpha_composite(image,(x,y))
    return result


def image_key(item):
    if 'statusId' in item:
        return 'status:'+item['statusId']+':'+item['kind']+(':'+'debuff' if item['isDebuff'] else ':buff')
    renderer=item['renderer']
    if renderer=='cardback':
        return 'cardback:Stack'
    if renderer=='cost':
        return 'cost:'+item['key']
    return renderer+':'+item['kind']


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'icons').mkdir(exist_ok=True)
    raw=load(OUT/'mesh-icons.json')
    meshes={item['key']:item for item in raw['icons']}
    hashes=load(TOOLS/'MeshExport/source-hashes.json')
    for entry in hashes:
        assert hashlib.sha256(Path(entry['Source']).read_bytes()).hexdigest().upper()==entry['SHA256'], 'Production source changed after mesh export: '+entry['Source']
    if '--sample' in sys.argv:
        samples=['command:Energy','command:ActionPoint','command:Mana','command:Powder','status:guard:Guard:buff','status:burn:Fire:debuff','status:fire_retaliation:Retaliation:buff','status:arcane_echo:Arcane:buff','field:Fire','field:Arcane','field:Tide','field:Blood','header:Deck','header:Settings','cardback:Default']
        sheet=Image.new('RGBA',(5*192,3*192),(244,241,233,255))
        for index,key in enumerate(samples):
            image=rasterize(meshes[key])
            sheet.alpha_composite(image,((index%5)*192,(index//5)*192))
        sheet.convert('RGB').save(OUT/'mesh-sample.png')
        print('Saved mesh-sample.png from '+str(len(samples))+' original meshes')
        return
    statuses=load(OUT/'data/statuses.json')
    others=load(OUT/'data/other-icons.json')+load(OUT/'data/cost-icons.json')
    for item in others:
        if item['key']=='field-arcane':
            item['effect']='己方船在自身行动期间触发己方折射阵时，获得 1 临时法力；本回合下次奥术攻击最多 4 点伤害穿过格挡，使用后或本方回合末清除。'
        elif item['key']=='field-blood':
            item['effect']='己方船在自身行动期间触发己方血誓阵时，获得 6 格挡；本回合下次主动攻击增加 4 点伤害，使用后或本方回合末清除。'
    items=statuses+others
    assert len({i['key'] for i in items})==len(items),'duplicate catalog key'
    assert len({i['name'] for i in statuses})==61,'status name coverage changed'
    unique, images={},[]
    evidence=[]
    for item in items:
        key=image_key(item)
        if key not in unique:
            if key=='cardback:Stack':
                image=card_stack(rasterize(meshes['cardback:Default']))
            elif key.startswith('cost:'):
                path=Path(item['image'])
                if not path.is_absolute(): path=OUT/path
                assert path.is_file(),str(path)
                image=fit_image(Image.open(path))
            else:
                if key not in meshes:
                    raise KeyError('Missing actual mesh: '+key)
                image=rasterize(meshes[key])
            assert image.getbbox(),'empty icon: '+key
            unique[key]=len(images)
            images.append(image)
            filename=key.replace(':','_')+'.png'
            image.save(OUT/'icons'/filename,optimize=True)
            evidence.append({'key':key,'file':'icons/'+filename,'sha256':hashlib.sha256((OUT/'icons'/filename).read_bytes()).hexdigest()})
        item['spriteIndex']=unique[key]
    # One sprite sheet retains high-quality inspection previews while avoiding repeated data URIs.
    columns=10
    rows=(len(images)+columns-1)//columns
    template=(TOOLS/'atlas-template.html').read_text(encoding='utf-8')
    for cell in (160,144,128):
        sheet=Image.new('RGBA',(columns*cell,rows*cell))
        for index,image in enumerate(images):
            sheet.alpha_composite(image.resize((cell,cell),Image.Resampling.LANCZOS),((index%columns)*cell,(index//columns)*cell))
        memory=io.BytesIO()
        sheet.save(memory,format='WEBP',lossless=True,method=6)
        sprite=memory.getvalue()
        data={'cell':cell,'columns':columns,'items':items,'sprite':'data:image/webp;base64,'+base64.b64encode(sprite).decode('ascii')}
        serialized=json.dumps(data,ensure_ascii=False,separators=(',',':')).replace('</','<\\/')
        fragment=template.replace('__ATLAS_DATA__',serialized)
        if len(fragment.encode('utf-8'))<990_000: break
    assert len(fragment.encode('utf-8'))<1_000_000,'inline artifact exceeds size limit'
    assert '__ATLAS_DATA__' not in fragment
    INLINE.parent.mkdir(parents=True,exist_ok=True)
    INLINE.write_text(fragment,encoding='utf-8')
    (OUT/'atlas-sprite.webp').write_bytes(sprite)
    (OUT/'catalog.json').write_text(json.dumps(items,ensure_ascii=False,indent=2),encoding='utf-8')
    manifest={'created':'2026-10-22','entries':len(items),'uniqueImages':len(images),'statusNames':len({i['name'] for i in statuses}),'spriteCell':cell,'fragmentBytes':len(fragment.encode('utf-8')),'fragment':str(INLINE),'meshMetadata':raw.get('meta',{}),'sourceHashes':hashes,'images':evidence}
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:manifest[k] for k in ('entries','uniqueImages','statusNames','spriteCell','fragmentBytes','fragment')},ensure_ascii=False))
    renderer=Path('C:/Users/Admin/.codex/plugins/cache/openai-bundled/visualize/1.0.55/skills/visualize/scripts/render.py')
    subprocess.run([sys.executable,str(renderer),str(INLINE),str(OUT/'atlas-preview.html'),'--force'],check=True)
    if '--inspect' in sys.argv:
        node='C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'
        subprocess.run([node,str(TOOLS/'inspect_atlas.cjs')],check=True)


if __name__=='__main__':
    main()
