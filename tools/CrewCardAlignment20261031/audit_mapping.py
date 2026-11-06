"""Read-only production art audit; generated crops are review previews only."""
from pathlib import Path
import json, hashlib, datetime
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/crew-card-alignment-20261031/audit'
SHEETS = OUT / 'contact-sheets'
SHEETS.mkdir(parents=True, exist_ok=True)
PRIOR = ROOT / 'outputs/crew-review-20261030/crew-data.json'
ART = ROOT / 'outputs/card-list-by-school-20261023/card-art-data.json'
CARDS = ROOT / 'outputs/card-list-by-school-20261023/card-list-data.json'
prior = json.loads(PRIOR.read_text(encoding='utf-8-sig'))
art = json.loads(ART.read_text(encoding='utf-8-sig'))
cards = json.loads(CARDS.read_text(encoding='utf-8-sig'))
hashes = {}

def sha(p):
    p = Path(p)
    if p not in hashes:
        hashes[p] = hashlib.sha256(p.read_bytes()).hexdigest()
    return hashes[p]

def source_record(item):
    path = ROOT / item['sourcePath']
    with Image.open(path) as im:
        size = list(im.size)
    actual = sha(path)
    assert actual.lower() == item['sourceSha256'].lower(), 'Prior source changed: ' + str(path)
    return {'sourcePath': path.as_posix(), 'sha256': actual,
            'dimensions': size, 'priorAuditSourceHashMatches': True,
            **{k:item[k] for k in ['cropPixels','normalizedBounds'] if k in item}}

rows = []
for crew in prior['crew']:
    cid = 'card_special_' + crew['id']
    stagearts = [x for x in art['Cards'] if x['Id'] == cid]
    assert stagearts, cid
    assert len({x['SourcePath'] for x in stagearts}) == 1, cid
    a = stagearts[0]
    source = Path(a['SourcePath'])
    assert sha(source).lower() == a['SourceSha256'].lower(), 'Manifest source changed: '+str(source)
    with Image.open(source) as im:
        dimensions = list(im.size)
    r = {k:crew[k] for k in ['id','displayName','portName','professionIndex','professionId','professionName','appearanceTags','portraitAssetName']}
    r['card'] = {'id':cid,'name':a['Name'],'requiredCrew':crew['id'],
                 'resourcePath':a['ResourcePath'],'sourcePath':source.as_posix(),
                 'sha256':sha(source),'dimensions':dimensions,
                 'manifestSourceHashMatches':True,
                 'stages':[x['StageId'] for x in stagearts]}
    r['tavernOverview'] = source_record(crew['art']['tavern'])
    r['dossierPortrait'] = source_record(crew['art']['portrait'])
    r['dossierPortrait']['uv'] = [0,0,1,1]
    r['regionalPortraitAuxiliary'] = source_record(crew['art']['regionalPortrait'])
    idx = crew['professionIndex']
    r['regionalPortraitAuxiliary']['uvUnityBottomLeft'] = [idx%3/3,1-(idx//3+1)/2,1/3,1/2]
    rows.append(r)

assert len(rows) == len({x['id'] for x in rows}) == 36
fonts = Path('C:/Windows/Fonts/msyh.ttc')
font = ImageFont.truetype(str(fonts),20)
small = ImageFont.truetype(str(fonts),14)
titlefont = ImageFont.truetype(str(fonts),28)
cell, label, gap, head = 300, 210, 12, 95
width = label + 4*(cell+gap)+gap
height = head + 6*(cell+gap)+gap
columns = ['酒馆总览（当前）','档案肖像 v2（当前）','区域图集 v2（辅助）','卡图（当前）']

def fit_on(sheet, source, box, crop=None):
    with Image.open(source) as im:
        im = im.convert('RGB')
        if crop:
            im = im.crop(tuple(crop))
        im = ImageOps.contain(im,(box[2]-box[0],box[3]-box[1]),Image.Resampling.LANCZOS)
        sheet.paste(im,(box[0]+((box[2]-box[0])-im.width)//2,box[1]+((box[3]-box[1])-im.height)//2))

sheet_paths = []
for port in prior['ports']:
    members = sorted([r for r in rows if r['portName']==port],key=lambda r:r['professionIndex'])
    assert len(members)==6
    slug = members[0]['id'].removeprefix('crew_').split('_navigator_')[0]
    sheet = Image.new('RGB',(width,height),'#e7edef')
    d = ImageDraw.Draw(sheet)
    d.text((16,12),port+' · 船员身份对照',fill='#16303a',font=titlefont)
    for j,col in enumerate(columns):
        d.text((label+j*(cell+gap),60),col,fill='#25444d',font=font)
    for i,r in enumerate(members):
        y = head+i*(cell+gap)
        d.text((12,y+12),r['displayName'],fill='#16303a',font=font)
        d.text((12,y+44),r['professionName'],fill='#496470',font=small)
        suffix = r['id'].removeprefix('crew_'+slug+'_')
        d.text((12,y+72),suffix,fill='#496470',font=small)
        d.text((12,y+112),r['card']['name'],fill='#16303a',font=font)
        for j,key in enumerate(['tavernOverview','dossierPortrait','regionalPortraitAuxiliary','card']):
            box=(label+j*(cell+gap),y,label+j*(cell+gap)+cell,y+cell)
            d.rectangle(box,fill='#fafafa',outline='#bacad0')
            item=r[key]
            fit_on(sheet,item['sourcePath'],box,item.get('cropPixels'))
    target = SHEETS/(slug+'.png')
    sheet.save(target)
    sheet_paths.append(target.as_posix())
    print(target.as_posix(),flush=True)

report = {
  'schemaVersion':1,
  'generatedAt':datetime.datetime.now().isoformat(timespec='seconds'),
  'readOnlyProductionAudit':True,
  'canonicalTavernAppearance':{
    'overview':'Characters are painted into PortFacilities/Regional/Tavern/tavern_<port>_v1; TavernCrewLayoutCatalog supplies per-character source bounds. Transparent buttons are interaction only.',
    'detail':'PortArt/CrewDossiers/<crewId>_portrait_v2 is first successful dossier presentation for all 36 present portraits. Entire image UV; top-aligned proportional fit into framed clipped viewport.',
    'auxiliary':'CrewRegional/<port>_portraits_v2 atlas is used for other portraits / fallback, not the current tavern overview.'
  },
  'inputs': [{'path':p.as_posix(),'sha256':sha(p)} for p in [PRIOR,ART,CARDS]],
  'contactSheets':sheet_paths,
  'crew':rows,
  'validation':{'crewCount':36,'allCurrentSourceHashesMatchPriorAuditAndCardManifest':True,
                'perPortCount':{p:sum(r['portName']==p for r in rows) for p in prior['ports']}}
}
(OUT/'crew-card-mapping.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Mapping:',(OUT/'crew-card-mapping.json').as_posix())
