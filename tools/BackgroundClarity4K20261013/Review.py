"""Read-only visual-comparison evidence; never overwrites source or enhanced art."""
import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageFilter
import numpy as np
OUT=Path('D:/Game/Artifacts/BackgroundClarity4K20261013')
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
rows=[]
for item in manifest['items']:
 if not Path(item['finalOutput']).exists():continue
 with Image.open(item['backup']) as im:a=im.convert('RGB')
 with Image.open(item['finalOutput']) as im:b=im.convert('RGB')
 a_small=a.resize((512,288),Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(2))
 b_small=b.resize((512,288),Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(2))
 av,bv=np.asarray(a_small,dtype=float),np.asarray(b_small,dtype=float)
 item['coarseRgbCorrelation']=round(float(np.corrcoef(av.ravel(),bv.ravel())[0,1]),6)
 item['meanChannelDifference']=round(float(np.abs(av-bv).mean()/255),6)
 # These metrics detect large composition/color drift; they do not prove pixel identity.
 rows.append({'name':item['name'],'group':item['group'],'source':item['backup'],'output':item['finalOutput'],
              'coarseRgbCorrelation':item['coarseRgbCorrelation'],'meanChannelDifference':item['meanChannelDifference']})
for group in manifest['approvedGroups']:
 items=[x for x in rows if x['group']==group]
 if not items:continue
 sheet=Image.new('RGB',(1200,len(items)*375),(25,33,43));draw=ImageDraw.Draw(sheet)
 for i,item in enumerate(items):
  y=i*375;draw.text((12,y+8),item['name']+'  BEFORE / AFTER   corr='+str(item['coarseRgbCorrelation']),font=font,fill='white')
  for j,key in enumerate(['source','output']):
   with Image.open(item[key]) as im:thumb=im.convert('RGB').resize((590,332),Image.Resampling.LANCZOS)
   sheet.paste(thumb,(j*600+5,y+36))
 sheet.save(OUT/'comparisons'/(group+'-contact.jpg'),quality=93)
review={'scope':'Coarse RGB correlation at 512x288 after radius2 blur, intended only to flag layout/palette drift. Visual inspection required for faces, detail and UI alignment.',
        'count':len(rows),'items':rows,'minimumCorrelation':min(x['coarseRgbCorrelation'] for x in rows)}
(OUT/'visual-metrics.json').write_text(json.dumps(review,indent=2),encoding='utf-8')
# Native screen-pixel sample: original rendered at UHD with bilinear sampling vs enhanced.
item=manifest['items'][0]
if Path(item['finalOutput']).exists():
 with Image.open(item['backup']) as im:a=im.convert('RGB').resize((3840,2160),Image.Resampling.BILINEAR)
 with Image.open(item['finalOutput']) as im:b=im.convert('RGB')
 box=(100,150,1060,830)
 detail=Image.new('RGB',(1920,730),(25,33,43));draw=ImageDraw.Draw(detail)
 draw.text((15,14),'BEFORE: original 1672 x 941 displayed at 4K',font=font,fill='white')
 draw.text((975,14),'AFTER: Nano Banana restoration, 3840 x 2160',font=font,fill='white')
 detail.paste(a.crop(box),(0,50));detail.paste(b.crop(box),(960,50))
 detail.save(OUT/'comparisons'/'port-detail-before-after.png')
print(json.dumps({'reviewedFiles':len(rows),'minimumCoarseCorrelation':review['minimumCorrelation']}))
