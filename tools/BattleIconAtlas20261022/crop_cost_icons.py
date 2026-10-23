"""Crop existing authored card/atlas art; never synthesize or repaint a resource icon."""
import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

ROOT=Path('D:/Game')
OUT=ROOT/'outputs/battle-icon-atlas-20261022'
FRAMES=ROOT/'UnityClient/Assets/_Project/Resources/CardSchools/Frames/Integrated'
layouts={entry['id']:entry for entry in json.loads((FRAMES/'frame-layouts.json').read_text(encoding='utf-8-sig'))['entries']}
specs=[
    ('energy','能量费用','frame-neutral-both','energyPosition','支付本张卡的能量成本；具体费用随当前效果变化。'),
    ('powder','火药费用','frame-neutral-both','powderPosition','支付本张卡需要的火药；免耗效果生效时可能为 0。'),
    ('cannonball','炮弹费用','frame-neutral-both','cannonballPosition','支付本张炮击卡需要的炮弹。'),
    ('mana','法力费用','frame-arcane-ap-mana','specialPosition','部分奥术卡额外消耗法力。'),
    ('health','生命／船体费用','frame-blood-ap-health','specialPosition','支付自身生命或船体；具体支付对象取决于战斗阶段。'),
    ('curse','咒痕费用','frame-curse-ap-curse','specialPosition','消耗已积累的咒痕／咒印，具体数量见牌面。'),
    ('tide','潮势费用','frame-tide-ap-tide','specialPosition','消耗潮势以发动对应卡牌效果。'),
    ('chain','雷链费用','frame-thunder-ap-chain','specialPosition','消耗雷链／连锁；消耗全部的牌在战斗中显示实际支付量。'),
    ('wood','木板费用','frame-neutral-ap-wood','supplyPosition','修理船体或部分战术牌消耗木板；与体系资源费用分开计算。'),
]
OUT.joinpath('icons').mkdir(parents=True,exist_ok=True)
OUT.joinpath('data').mkdir(exist_ok=True)
items=[]
for key,name,frame,slot,effect in specs:
    source=FRAMES/(frame+'.png')
    image=Image.open(source).convert('RGBA')
    layout=layouts[frame]; rect=layout['rect']; position=layout[slot]
    x=(position['x']-rect['x'])/rect['width']*image.width
    y=(rect['y']+rect['height']-position['y'])/rect['height']*image.height
    # The emblem is integral to the frame: retain nearby rim pixels as authored.
    half=38/rect['width']*image.width
    bounds=[round(x-half),round(y-half),round(x+half),round(y+half)]
    cropped=image.crop(bounds)
    filename='icons/cost-'+key+'.png'
    cropped.save(OUT/filename)
    items.append({'key':'cost_'+key,'category':'cost','name':name,'kind':key,'renderer':'cost','effect':effect,'stage':'卡面','source':'D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CardSchoolArtCatalog.cs:137','sourceAsset':str(source).replace('\\','/'),'crop':bounds,'image':filename,'detail':'完整卡框中的实际徽记裁片。保留周围框边；不同流派的装饰边框可能不同。动态数字另由界面叠加。'})

# Medicine is a separately authored supply asset. Prefer an existing integrated frame when ready.
medicine=FRAMES/'frame-neutral-ap-medicine.png'
if medicine.exists() and medicine.stem in layouts:
    image=Image.open(medicine).convert('RGBA'); layout=layouts[medicine.stem]
    rect=layout['rect'];position=layout.get('supplyPosition',{'x':101,'y':108})
    x=(position['x']-rect['x'])/rect['width']*image.width
    y=(rect['y']+rect['height']-position['y'])/rect['height']*image.height
    half=38/rect['width']*image.width
    bounds=[round(x-half),round(y-half),round(x+half),round(y+half)]
    cropped=image.crop(bounds);source=medicine
    detail='完整药品卡框中的实际徽记裁片；动态数字另由界面叠加。'
    stage='接舷卡面'
else:
    source=ROOT/'UnityClient/Assets/_Project/Resources/ItemIcons/items_small_v1.png'
    image=Image.open(source).convert('RGBA')
    bounds=[round(292/1402*image.width),round(317/1122*image.height),round(543/1402*image.width),round(552/1122*image.height)]
    cropped=image.crop(bounds)
    current=(ROOT/'UnityClient/Assets/_Project/Presentation/Runtime/CardSchoolArtCatalog.cs').read_text(encoding='utf-8-sig')
    switched='supplyType == "wood" || supplyType == "medicine"' in current
    detail=('当前费用绑定已切换为整卡框内图案，但药品整卡框文件尚未出现；此处展示现有物品图集中的药品素材，供检查素材缺口。' if switched else '从游戏物品图集中按实际药品 UV 裁切；用于卡面独立补给费用。')
    stage='药品素材 · 卡框待就绪' if switched else '接舷卡面'
filename='icons/cost-medicine.png';cropped.save(OUT/filename)
items.append({'key':'cost_medicine','category':'cost','name':'药品费用','kind':'medicine','renderer':'cost','effect':'治疗及部分接舷牌消耗药品；具体数量按当前治疗条件计算。','stage':stage,'source':'D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/ItemIconCatalog.cs:36','sourceAsset':str(source).replace('\\','/'),'crop':bounds,'image':filename,'detail':detail})
(OUT/'data/cost-icons.json').write_text(json.dumps(items,ensure_ascii=False,indent=2),encoding='utf-8')
sheet=Image.new('RGB',(5*230,2*270),(242,240,233))
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
for index,item in enumerate(items):
    thumb=Image.open(OUT/item['image']).convert('RGBA');thumb.thumbnail((210,210),Image.Resampling.LANCZOS)
    x=(index%5)*230+(230-thumb.width)//2;y=(index//5)*270+10
    sheet.paste(thumb,(x,y),thumb)
    draw.text(((index%5)*230+10,(index//5)*270+230),item['name'],font=font,fill=(30,35,40))
sheet.save(OUT/'cost-contact-sheet.png')
print('Cropped '+str(len(items))+' existing cost icons; medicine: '+stage)
