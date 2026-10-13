"""Local-only composition preview. No image API, no changes to game assets."""
from pathlib import Path
import json
import hashlib
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path('D:/Game')
OUT = ROOT/'Artifacts/CrewHalfBodyLocalPreview20261013'
OUT.mkdir(parents=True, exist_ok=True)
META = json.loads((ROOT/'Artifacts/CrewLayers20261013/manifest.json').read_text(encoding='utf-8'))
SOURCE = ROOT/'UnityClient/Assets/_Project/Resources/PortArt/CrewDossiers'
FRAME = ROOT/'Artifacts/CrewLayers20261013/frame-4k-v1.jpg'
FONT = 'C:/Windows/Fonts/msyh.ttc'

# Match the actual UI's 1780 x 705 paper, at one logical pixel per pixel.
size = (1780, 705)
window = (126, 116, 538, 604)
frame = Image.open(FRAME).convert('RGB').resize(size, Image.Resampling.LANCZOS)
rgb = np.asarray(frame).astype(np.float32)/255
key = np.array([0,128/255,1], dtype=np.float32)
excess = np.maximum(0, rgb[:,:,2] - np.maximum(rgb[:,:,0],rgb[:,:,1]))
coverage = np.clip(1-np.maximum(0,excess-.05)/((1-128/255)-.05),0,1)
coverage = np.clip((coverage-.12)/.88,0,1)
foreground = np.clip((rgb-key[None,None,:]*(1-coverage[:,:,None]))/np.maximum(coverage[:,:,None],.0001),0,1)
edge = np.clip(((1-coverage)-.001)/(.08-.001),0,1)
edge = edge*edge*(3-2*edge)
foreground[:,:,2] = foreground[:,:,2]*(1-edge)+np.minimum(foreground[:,:,2],(foreground[:,:,0]+foreground[:,:,1])*.5)*edge
rgba = np.dstack((np.uint8(np.rint(foreground*255)),np.uint8(np.rint(coverage*255))))
frame_layer = Image.fromarray(rgba)

background = Image.open(ROOT/'Artifacts/CrewLayers20261013/verification/capture2/capture/crew-0-atlas-3840x2160.png').convert('RGB')
# A plain dark workbench surrounds the local mockup, not part of the portrait.
panel_crop = (50, 44, 605, 658)
panel_w, panel_h = 555, 658
gap = 22
header_h = 132
contact = Image.new('RGB',(panel_w*3+gap*4,header_h+panel_h*2+gap*3),'#191611')
draw = ImageDraw.Draw(contact)
draw.text((gap,18),'断锚港 · 六人上半身构图预览',font=ImageFont.truetype(FONT,34),fill='#eee0c5')
draw.text((gap,69),'本地拼合 / 0 次 API · 原版人物与背景 + 共用空框',font=ImageFont.truetype(FONT,22),fill='#c2af8d')
draw.text((gap,103),'先看半身比例、手部与道具；原图局部饰件暂保留，不代表最终高清成图。',font=ImageFont.truetype(FONT,18),fill='#a9987c')
atlas = Image.new('RGB',(446*3,547*2))
records=[]
for index, crew in enumerate(META['crew']):
    source = SOURCE/(crew['id']+'_dossier_v1.png')
    original = Image.open(source).convert('RGB')
    # Keep the whole original bust and hands, rather than zooming into the face.
    crop = (110,119,556,666)
    portrait = original.crop(crop)
    atlas.paste(portrait,((index%3)*446,(index//3)*547))
    paper = Image.new('RGBA',size,(0,0,0,0))
    # The source aspect is close to the final aperture: only ~3% vertical loss.
    portrait_fit = ImageOps.fit(portrait,(window[2]-window[0],window[3]-window[1]),
                               method=Image.Resampling.LANCZOS,centering=(.5,.45))
    paper.paste(portrait_fit,window[:2])
    paper = Image.alpha_composite(paper,frame_layer)
    page = Image.new('RGB',size,'#201b14');page.paste(paper,(0,0),paper)
    pd = ImageDraw.Draw(page)
    pd.text((690,125),crew['name'],font=ImageFont.truetype(FONT,42),fill='#392718')
    for y,text in [(225,'上半身、手部与职业道具保留'),(282,'人物连同背景作为完整肖像放在框下'),(339,'文字保持独立，空框继续共用')]:
        pd.text((675,y),text,font=ImageFont.truetype(FONT,28),fill='#513d29')
    pd.text((675,500),'本地构图预览 · 尚未调用出图 API',font=ImageFont.truetype(FONT,24),fill='#7f5335')
    page_path=OUT/(crew['id']+'-page-preview.png')
    page.save(page_path)
    panel = Image.new('RGB',(panel_w,panel_h),'#201b14')
    panel.paste(page.crop(panel_crop),(0,0))
    pdraw=ImageDraw.Draw(panel)
    pdraw.text((22,620),crew['name'],font=ImageFont.truetype(FONT,26),fill='#eadbbb')
    contact.paste(panel,(gap+(index%3)*(panel_w+gap),header_h+gap+(index//3)*(panel_h+gap)))
    records.append(dict(id=crew['id'],name=crew['name'],source=str(source),cropXYXY=list(crop),
                        sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),pagePreview=str(page_path)))

contact.save(OUT/'six-crew-framed-preview.png')
atlas.save(OUT/'six-crew-halfbody-background-preview.png')
(OUT/'manifest.json').write_text(json.dumps(dict(apiCalls=0,gameAssetsChanged=False,
    purpose='Local composition preview only; reuses original half-body paintings and their existing patterned backgrounds beneath the shared empty frame.',
    sourceFrame=str(FRAME),displayFramePixels=list(size),portraitWindowXYXY=list(window),
    limitations=['Not AI-generated final art and not a high-resolution upgrade.',
                 'Original painted background and some lower-corner ornaments remain as temporary placeholders.',
                 'No new individual environment backgrounds have been generated.'],crew=records),ensure_ascii=False,indent=2),encoding='utf-8')
print(OUT/'six-crew-framed-preview.png')
