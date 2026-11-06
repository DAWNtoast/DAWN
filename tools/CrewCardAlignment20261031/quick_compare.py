import json,hashlib
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont

root=Path('D:/Game')
out=root/'outputs/crew-card-alignment-20261031/audit'
out.mkdir(parents=True,exist_ok=True)
data=json.loads((root/'outputs/crew-review-20261030/crew-data.json').read_text(encoding='utf-8-sig'))
cards=json.loads((root/'outputs/card-list-by-school-20261023/card-art-data.json').read_text(encoding='utf-8-sig'))
by_id={r['Id']:r for r in cards['Cards']}
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14)
for port in data['ports']:
    crew=[c for c in data['crew'] if c['portName']==port]
    sheet=Image.new('RGB',(1056,6*342+42),'#faf7ef'); draw=ImageDraw.Draw(sheet)
    for col,label in enumerate(['现役酒馆场景','现役详情肖像','当前船员卡图']):
        draw.text((col*352+12,8),label,font=font,fill='#201910')
    for row,c in enumerate(crew):
        y=42+row*342
        draw.text((12,y),c['displayName']+' · '+c['id'],font=small,fill='#201910')
        card=by_id[c['specialCards'][0]['id']]
        refs=[c['art']['tavern'],c['art']['portrait']]
        images=[]
        for ref in refs:
            source=root/ref['sourcePath']
            assert hashlib.sha256(source.read_bytes()).hexdigest()==ref['sourceSha256'],source
            im=Image.open(source).convert('RGB')
            if 'cropPixels' in ref: im=im.crop(ref['cropPixels'])
            images.append(im)
        source=Path(card['SourcePath'])
        assert hashlib.sha256(source.read_bytes()).hexdigest()==card['SourceSha256'],source
        images.append(Image.open(source).convert('RGB'))
        for col,im in enumerate(images):
            preview=ImageOps.contain(im,(336,308))
            sheet.paste(preview,(col*352+(352-preview.width)//2,y+27+(308-preview.height)//2))
    slug=crew[0]['id'].split('_navigator_')[0].replace('crew_','')
    target=out/('quick-'+slug+'.jpg')
    sheet.save(target,quality=94)
    print(target)
