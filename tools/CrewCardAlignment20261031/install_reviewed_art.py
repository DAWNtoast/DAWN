"""Install six visually reviewed built-in image edits; preserve IDs and importer GUIDs."""
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont

ROOT = Path('D:/Game')
TASK = ROOT / 'outputs/crew-card-alignment-20261031'
MAPPING = json.loads((TASK/'audit/crew-card-mapping.json').read_text(encoding='utf-8-sig'))
REPAIRS = {
    'crew_broken_anchor_navigator_lorne': '恢复当前酒馆的正常深色瞳孔、面貌年龄、整齐灰白长辫与海军蓝外套，保留暴潮指挥场景。',
    'crew_white_shell_carpenter_atang': '恢复当前酒馆的高马尾和金属额链，保留修船动作及原侧臂纹身；不同侧纹身不作推断。',
    'crew_bronze_bell_carpenter_osser': '恢复黄铜机械左臂、后梳银发和整齐灰白胡须，移除错误环状脸疤，保留雷电设备操作场景。',
    'crew_salt_crown_carpenter_saf': '对齐当前脸型年龄、短银白发、金额链和骨架肩背饰，保留木槌补船动作。',
    'crew_salt_crown_doctor_naji': '恢复双侧高盘发及当前额颊白纹，保留救治、倒药与诊脉场景。',
    'crew_salt_crown_gunner_rokan': '恢复壮硕体型、无须方下巴、白金发型、金色几何纹样及蓝金背心，保留分装炮弹场景。',
}
ROOT_REASONS = {
    'kia': '黑色短发剃侧、方下颌、无须、结实体型和铆钉围裙一致；白衬衣及劳动污痕属于工作场景差异。',
    'hector': '光头、白胡须、年长脸型及瘢痕身份线索一致，治疗场景服装变化不影响识别。',
    'mira': '深肤、银白短卷发、宽鼻厚唇、宽肩与红围巾一致；舰炮操作姿势改变装备可见范围。',
    'tam': '丰满体型、棕红卷发、红发带、耳环、白上衣与围裙一致。',
    'udo': '光头、下唇大圆环、短须、深肤、强壮体格及黄铜潜水装备一致。',
    'ashir': '光头及长蓝辫、白色额纹、奶白蓝纹服装一致。',
    'maz': '深色卷发、金圈耳饰、面貌及红金奶白服装线索一致。',
    'isha': '白色短卷发、大眼圆脸、蓝宝石耳坠和浅色面纹一致；战斗围巾与绳网为场景装备。',
    'evelyn': '深色波浪长发、面貌、青绿围巾、耳环和绿色上衣一致。',
    'barlow': '强壮体型、棕色须发、红衣及皮围裙一致。',
    'moer': '眼镜、深色中分发、胡须及青色药瓶线索一致。',
    'leisa': '红色长发、方下颌、胡茬与壮实体型一致；束发及工作服变化可识别为同一角色。',
    'pepe': '丰满圆脸、暖棕肤、白头巾、深卷发、金圈耳饰及棕围裙一致；已核对原图。',
    'noya': '年轻深色双辫女性、面貌、绿色背心和白衬衣一致。',
}

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def dump(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')

secondary = json.loads((TASK/'audit/visual-audit-secondary.json').read_text(encoding='utf-8-sig'))
reasons = {row['crewId']: row['reason'] for row in secondary['results']}
plans = []
# All safeguards run before any production write.
for crew in MAPPING['crew']:
    for ref in [crew['tavernOverview'], crew['dossierPortrait']]:
        assert sha(ref['sourcePath']) == ref['sha256'], ('Reference changed', crew['id'])
    source = Path(crew['card']['sourcePath'])
    backup = TASK/'input-backup'/source.relative_to(ROOT)
    assert sha(backup) == crew['card']['sha256'], ('Original backup mismatch', source)
    if crew['id'] not in REPAIRS:
        assert sha(source) == sha(backup), ('Unrelated art changed', source)
        continue
    assert not crew['card']['otherActiveCardConsumers'], ('Shared art', crew['id'])
    candidate = TASK/'generated'/f"{crew['id']}.png"
    with Image.open(candidate) as im:
        im.load()
        assert im.format == 'PNG' and im.size == (1254, 1254), (candidate, im.size)
    assert sha(source) in [sha(backup), sha(candidate)], ('Unexpected source edit', source)
    meta = Path(str(source)+'.meta')
    assert sha(meta) == sha(Path(str(backup)+'.meta')), ('Importer changed', meta)
    plans.append((crew, source, candidate, sha(meta)))

assert len(plans) == 6
installed = []
for crew, source, candidate, meta_hash in plans:
    shutil.copyfile(candidate, source)
    assert sha(source) == sha(candidate)
    assert sha(str(source)+'.meta') == meta_hash
    installed.append({
        'crewId': crew['id'], 'name': crew['displayName'], 'cardId': crew['card']['id'],
        'sourcePath': str(source), 'candidatePath': str(candidate),
        'originalSha256': crew['card']['sha256'], 'installedSha256': sha(source),
        'dimensions': [1254, 1254], 'metaSha256': meta_hash,
        'reviewedByRoot': True, 'reason': REPAIRS[crew['id']],
    })

reviews = []
for crew in MAPPING['crew']:
    crew_id = crew['id']
    reason = REPAIRS.get(crew_id) or reasons.get(crew_id) or ROOT_REASONS[crew_id.rsplit('_', 1)[-1]]
    reviews.append({
        'crewId': crew_id, 'name': crew['displayName'], 'cardId': crew['card']['id'],
        'status': 'corrected' if crew_id in REPAIRS else 'aligned-kept',
        'reason': reason, 'sourcePath': crew['card']['sourcePath'],
        'beforeSha256': crew['card']['sha256'], 'afterSha256': sha(crew['card']['sourcePath']),
        'tavernSourceSha256': crew['tavernOverview']['sha256'],
        'portraitSourceSha256': crew['dossierPortrait']['sha256'],
    })
dump(TASK/'audit/install-manifest.json', {'tool': 'built-in image_gen', 'installed': installed, 'preservedImporterMeta': True})
dump(TASK/'audit/final-visual-audit.json', {
    'authority': 'Current production tavern scene and dossier portrait_v2; not historical appearance tags.',
    'summary': {'checked': 36, 'corrected': 6, 'alignedKept': 30},
    'results': reviews,
})

# Review-only contact sheet. Production originals remain byte-for-byte generated PNGs.
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 17)
sheet = Image.new('RGB', (1200, 6*435+60), '#f7f3e9')
draw = ImageDraw.Draw(sheet)
for col, title in enumerate(['当前酒馆详情形象', '修正前卡图', '修正后卡图']):
    draw.text((col*400+16, 16), title, font=font, fill='#29241f')
for row, (crew, source, candidate, _) in enumerate(plans):
    y = 60 + row*435
    draw.text((16, y), crew['displayName'], font=small, fill='#29241f')
    files = [Path(crew['dossierPortrait']['sourcePath']), TASK/'input-backup'/source.relative_to(ROOT), candidate]
    for col, file in enumerate(files):
        with Image.open(file) as im:
            preview = ImageOps.contain(im.convert('RGB'), (384, 394))
            sheet.paste(preview, (col*400+(400-preview.width)//2, y+33+(394-preview.height)//2))
sheet.save(TASK/'audit/修正前后对照.jpg', quality=94)
print(json.dumps({'checked': len(reviews), 'installed': len(installed), 'cardIds': [r['cardId'] for r in installed]}, ensure_ascii=False))
