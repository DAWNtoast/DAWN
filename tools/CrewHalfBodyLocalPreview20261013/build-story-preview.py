"""No-network storyboard: existing cutouts + story-related scene crops + shared frame."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT=Path('D:/Game'); RES=ROOT/'UnityClient/Assets/_Project/Resources'
OUT=ROOT/'Artifacts/CrewStoryLocalPreview20261013';OUT.mkdir(parents=True,exist_ok=True)
font=lambda n:ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n)
cast=Image.open(RES/'BoardingBattle/Characters/boarding_crew_broken_anchor_v2.png').convert('RGBA')
crew=json.loads((ROOT/'Artifacts/CrewLayers20261013/manifest.json').read_text(encoding='utf-8'))['crew']
defs=[
 ('CardArt/Western/card_special_crew_broken_anchor_navigator_lorne.png',(930,0,1254,670),(58,43,373,508),'暴潮航路 · 雾后仍有船未归', '暴潮、漂浮残骸与雾中无名船；岸边测潮痕迹'),
 ('PortFacilities/Regional/Maintenance/maintenance_broken_anchor_side_v2.png',(220,0,1014,941),(474,51,772,465),'沉船船坞 · 会愈合的龙骨', '老船坞、湿木龙骨、铜铆钉和心跳般颤动的刨花'),
 ('CardArt/Western/card_special_crew_gray_gull_doctor_moer.png',(0,0,360,426),(908,51,1183,480),'黑沙医舱 · 深海留下的记忆', '昏暗船医舱、药瓶与黑沙分层的手术箱、病人话语记录'),
 ('CardArt/Western/card_special_crew_broken_anchor_gunner_mira.png',(550,350,1254,1183),(52,652,418,1040),'潮门巨链 · 沉船钟的低鸣', '潮门巨链、旧炮位、捞来的沉船钟和六段装药'),
 ('CardSchools/Art/card_school_neutral_crew_bronze_bell_cook_basin.png',(850,690,1254,1200),(480,654,826,1040),'残火灶台 · 失踪船只的最后一餐', '船上烟熏厨房、铜锅蒸汽、熏鱼、锅盖背面模糊旧名单'),
 ('CardArt/Western/card_special_crew_broken_anchor_diver_udo.png',(760,80,1254,665),(878,662,1228,1060),'港底锚墓 · 潜水钟里的另一个自己', '水下锈锚、沉船、潮门巨链与远处潜水钟、微光铜牌'),
]

size=(1780,705);window=(126,116,538,604);ww,wh=window[2]-window[0],window[3]-window[1]
frame=Image.open(ROOT/'Artifacts/CrewLayers20261013/frame-4k-v1.jpg').convert('RGB').resize(size,Image.Resampling.LANCZOS)
rgb=np.asarray(frame).astype(np.float32)/255;key=np.array([0,128/255,1],dtype=np.float32)
excess=np.maximum(0,rgb[:,:,2]-np.maximum(rgb[:,:,0],rgb[:,:,1]))
a=np.clip((np.clip(1-np.maximum(0,excess-.05)/((1-128/255)-.05),0,1)-.12)/.88,0,1)
fg=np.clip((rgb-key[None,None,:]*(1-a[:,:,None]))/np.maximum(a[:,:,None],.0001),0,1)
edge=np.clip(((1-a)-.001)/.079,0,1);edge=edge*edge*(3-2*edge)
fg[:,:,2]=fg[:,:,2]*(1-edge)+np.minimum(fg[:,:,2],(fg[:,:,0]+fg[:,:,1])*.5)*edge
frame=Image.fromarray(np.dstack((np.uint8(np.rint(fg*255)),np.uint8(np.rint(a*255)))))

pw,ph,gap,header=555,692,22,135
sheet=Image.new('RGB',(pw*3+gap*4,header+ph*2+gap*3),'#14191c');d=ImageDraw.Draw(sheet)
d.text((gap,18),'断锚港 · 六人故事场景预览',font=font(34),fill='#ece4d6')
d.text((gap,69),'本地拼合 / 0 次 API · 上半身人物 + 各自故事场景 + 共用外框',font=font(22),fill='#c5bbab')
d.text((gap,105),'场景借用现有美术示意方向；最终故事细节与光照尚未重新绘制。',font=font(18),fill='#99aaa9')
atlas=Image.new('RGB',(ww*3,wh*2));records=[]
for i,(c,spec) in enumerate(zip(crew,defs)):
    bgname,bgbox,personbox,caption,target=spec
    bg=ImageOps.fit(Image.open(RES/bgname).convert('RGB').crop(bgbox),(ww,wh),method=Image.Resampling.LANCZOS)
    bg.save(OUT/(c['id']+'-background-placeholder.png'))
    person=cast.crop(personbox)
    alpha=np.asarray(person.getchannel('A')).copy();alpha[alpha<8]=0;person.putalpha(Image.fromarray(alpha))
    scale=min((ww-14)/person.width,(wh-30)/person.height)
    person=person.resize((round(person.width*scale),round(person.height*scale)),Image.Resampling.LANCZOS)
    scene=bg.convert('RGBA')
    scene.alpha_composite(person,((ww-person.width)//2,wh-person.height+3))
    scene.save(OUT/(c['id']+'-scene-preview.png'))
    atlas.paste(scene.convert('RGB'),((i%3)*ww,(i//3)*wh))
    page=Image.new('RGBA',size,(0,0,0,0));page.paste(scene,window[:2]);page=Image.alpha_composite(page,frame)
    full=Image.new('RGB',size,'#182126');full.paste(page,(0,0),page)
    fd=ImageDraw.Draw(full)
    fd.text((675,122),c['name'],font=font(42),fill='#392718')
    fd.text((675,215),caption,font=font(26),fill='#6b4025')
    for y,txt in [(303,'人物保留上半身与职业道具'),(357,'肖像内部改为故事场景，不使用羊皮纸底'),(490,'本地构图示意 · 尚未生成新背景')]:
        fd.text((675,y),txt,font=font(26 if y<400 else 22),fill='#513d29')
    full.save(OUT/(c['id']+'-page-preview.png'))
    panel=Image.new('RGB',(pw,ph),'#202628');panel.paste(full.crop((50,44,605,658)),(0,0))
    pd=ImageDraw.Draw(panel);pd.text((18,619),c['name'],font=font(26),fill='#eee4d3')
    pd.text((18,657),caption,font=font(18),fill='#adbcb8')
    sheet.paste(panel,(gap+(i%3)*(pw+gap),header+gap+(i//3)*(ph+gap)))
    records.append(dict(id=c['id'],name=c['name'],storyTarget=target,backgroundSource=str(RES/bgname),backgroundCrop=list(bgbox),
                        foregroundSource='BoardingBattle/Characters/boarding_crew_broken_anchor_v2.png',foregroundCrop=list(personbox)))
sheet.save(OUT/'six-crew-story-preview.png');atlas.save(OUT/'six-crew-story-scenes-preview.png')
(OUT/'manifest.json').write_text(json.dumps(dict(apiCalls=0,gameAssetsChanged=False,localPreviewOnly=True,
    narrativeSource='UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs:139-144',
    note='Existing scene fragments approximate narrative settings. No newly generated backgrounds or finished matching lighting.',crew=records),ensure_ascii=False,indent=2),encoding='utf-8')
print(OUT/'six-crew-story-preview.png')
