"""Consolidate read-only resolution evidence into a user review list. No asset writes."""
import csv
import html
import json
from pathlib import Path

ROOT = Path('D:/Game')
OUT = ROOT / 'Artifacts/ResolutionUpgradeAudit20261013'
RES = ROOT / 'UnityClient/Assets/_Project/Resources'

def read(name):
    return json.loads((OUT / name).read_text(encoding='utf-8-sig'))

inventory = read('current-source-inventory.json')
by_file = {Path(x['file']).as_posix(): x for x in inventory}
background = read('background-review.json')
bg = {g['key']: g for g in background['groups']}
cinema = {g['id']: g for g in read('cinematic-review.json')['groups']}
parts = read('ui-component-showroom-sizing.json')
runtime = read('runtime-4k-verified.json')
groups = []

def bg_files(*keys):
    return [a['file'] for key in keys for a in bg[key]['assets']]

def cinema_files(key):
    return [(ROOT / x).as_posix() for x in cinema[key]['files']]

def add(id, name, section, priority, target, files, reason, display, note='', evidence=None):
    items = []
    for f in files:
        f = Path(f).as_posix()
        inv = by_file.get(f)
        if inv:
            current = [inv['width'], inv['height']]
            desktop = next((p['maxSize'] for p in inv['platforms']
                            if p['target'] == 'Standalone' and p['overridden']), inv['maxSize'])
        else:
            current, desktop = [3840, 2160], None
        items.append({'file': f, 'currentSize': current, 'currentDesktopMaxSize': desktop,
                      'proposedSize': target, 'proposedImportMaxSize': 2048 if max(target) <= 2048 else 4096})
    groups.append({'id': id, 'name': name, 'section': section, 'priority': priority,
                   'count': len(items), 'proposedSize': target, 'reason': reason,
                   'displayAt4K': display, 'note': note, 'approval': '待用户审核',
                   'evidence': evidence or [], 'files': items})

add('A01', '六港主场景', '4K级候选', '首批建议', [3840,2160], bg_files('port-hubs'),
    '现有1672×941被放大约2.30倍；是用户截图中最直接的清晰度缺口。', '3840×1944',
    '保留建筑位置、交互热点和构图；顶部实时文字独立绘制。', bg['port-hubs']['evidence'])
add('A02', '六港收藏室／仓库房间', '4K级候选', '首批建议', [3840,2160], bg_files('warehouse'),
    '同一套6张房间供收藏室与仓库复用；整幅背景约放大2.30倍。', '3840×2160',
    '按6张源图计数，不按多个使用页面重复计算。', bg['warehouse']['evidence'])
add('A03', '主菜单背景', '4K级候选', '首批建议', [3840,2160], bg_files('main-menu'),
    '现有1672×941用于全屏，约放大2.30倍。', '约3842×2160',
    '目标3840宽与实测2像素差异可忽略。', bg['main-menu']['evidence'])
add('A04', '炮战环境背景', '4K级候选', '高', [4096,2304], cinema_files('naval_backgrounds'),
    '6个环境现为1672×941，常规镜头约放大2.40倍；近景约3.88倍。', '常规原始画面4018×1944；近景再裁切',
    '4K级先改善常规镜头。若要求最紧近景仍1:1采样，约需6480像素宽源图，可另选6656×3744并将导入上限设8192；该额外方案需单独确认，不能承诺普通4K覆盖最紧近景。', cinema['naval_backgrounds']['evidence'])
add('A05', '接舷战斗背景', '4K级候选', '高', [4096,2304], cinema_files('boarding_backgrounds'),
    '6种环境×3个时段，共18张；现有1672×941约放大2.40倍。', '原始画面4018×1944，再被视口裁切',
    '保留甲板位置和人物脚底接触区域。', cinema['boarding_backgrounds']['evidence'])
add('A06', '六港酒馆房间', '4K级候选', '中', [3840,2160], bg_files('taverns'),
    '近全屏背景约放大2.30倍。', '3840×1944', evidence=bg['taverns']['evidence'])
facility_keys = ['shipyard','component-shop','treasure-room','port-investment','market']
add('A07', '船厂、部件商店、藏宝图室、投资、市场', '4K级候选', '中', [3840,2160], bg_files(*facility_keys),
    '5个当前实际使用的设施背景，约放大2.30倍。', '约3840×1944',
    '仅计现役版本；旧区域部件商店、旧维护房间不加入制作。', [e for k in facility_keys for e in bg[k]['evidence']])
add('A08', '其余五港出航背景', '4K级候选', '中', [3840,2160], bg_files('voyage-other-ports'),
    '白贝、铜钟、盐冠、暮潮、灰鸥五港仍使用1672×941背景。', '3840×2160',
    '断锚港已有4K，移至R01复用项。', bg['voyage-other-ports']['evidence'])
add('A09', '结算船舱背景', '4K级候选', '中', [3840,2160], bg_files('settlement'),
    '现有1672×941用于全屏，约放大2.30倍。', '3840×2160',
    '宝箱和航海日志是独立配图，现有尺寸足够，不随背景升级。', bg['settlement']['evidence'])
add('A10', '船员人物档案整页插画', '4K级候选', '高', [3966,1586], cinema_files('crew_dossier_paintings'),
    '36张1983×793宽幅画面被显示为3560×1410，约放大1.80倍。', '3560×1410',
    '人物、纸张和装饰已经合在整幅画中；按原比例升级整页，不另做36张4K立绘。', cinema['crew_dossier_paintings']['evidence'])
add('A11', '部件档案整张羊皮纸', '4K级候选', '中', [3840,2160], [RES/'PortArt/UI/tavern_crew_dossier_parchment_v1.png'],
    '1672×941整张图直接用于大面板，横向约放大2.13倍；带卷角、绳结和绘画。', '3560×1410',
    '这张是整幅贴图，不属于可排除的九宫格纸纹；保留原有透明通道、写字区域和UV。', ['CoreGameplayComponentDossier.cs'])
add('A12', '回收、设置和教学共用整张纸面', '4K级候选', '中', [3840,2560], [RES/'CardWorkshopV2/deck_builder_parchment_v2.png'],
    '1536×1024；回收界面只取中间60%高度，显示3280×1280，约放大2.14倍。', '回收3280×1280；教学3220×1768',
    '保持3:2源比例和现有裁切；共享页面仅计1张。', ['CoreGameplayComponentResale.cs','CoreGameplayEncounterPresentation.cs','CoreGameplayPortSettingsController.cs'])
add('A13', '教学事件整张装饰框', '4K级候选', '中', [3840,2402], [RES/'EncounterV2/encounter_frame_salvage_v1.png'],
    '1586×992整张框以RawImage显示，约放大2.09倍。', '3320×1860',
    '只计当前可达的salvage教学框；escort和survey旧框没有现役调用，不制作。', ['CoreGameplayEncounterPresentation.cs','CoreGameplayDemoController.cs'])

for tier, ident, title in [(2048,'B01','部件主展厅插画：2K档'),(3072,'B02','部件主展厅插画：3K档')]:
    selected = [RES/(p['resource']+'.png') for p in parts if p['suggestedMinimumTier']==tier]
    add(ident,title,'2K／3K候选','另批确认',[tier,tier],selected,
        '当前1024×1024源图在部件商店主展厅按透明边界裁切后放大；按每张有效轮廓计算所需尺寸。',
        '非仪器最大2200×1220包围框，保持比例适配',
        '保持透明边缘、枢轴、装配连接位置和轮廓；不升级同名512小图标。30张仪器图继续1024。依据完整150项几何表，代表部件另有真实4K验证。',
        ['ui-component-showroom-sizing.json','CoreGameplayComponentShopShowroom.cs','ComponentShopDisplayGeometry.cs'])
add('B03','卡组工坊共用整张纸面','2K／3K候选','中',[3072,2048],[RES/'CardWorkshopV2/deck_builder_collection_parchment_v3.png'],
    '1536×1024显示为2268×1884，约放大1.84倍；3K源图已足够。','2268×1884',
    '卡组编辑、升级、收藏等多处复用，只计1张。',['CoreGameplayCardWorkshopPreview.cs'])
add('B04','航海地图水面底纹','2K／3K候选','可选低优先',[3072,1729],[RES/'VoyageMapV2/water_chart_base_v2.png'],
    '1672×941显示约2408×1309，约放大1.44倍；可选3K，不必4K。','2408×1309',
    '程序生成的海岸距离场是着色器数据，不是需生成4K的图片。',['runtime-4k-verified.json','VoyageChartSurfaceGraphic.cs'])
add('B05','收藏陈列用纪念物图集A／B','2K／3K候选','中',[2804,2244],
    [RES/'ItemIcons/keepsakes_a_small_v1.png',RES/'ItemIcons/keepsakes_b_small_v1.png'],
    '每张20格，1402×1122原图目前又被导入缩至1024×819；大陈列单物品可达372像素。','单物品最大372×372',
    '代表罗盘当前有效175×167显示372×355，约2.12倍。只提高上限可恢复原图部分细节，但仍不足；建议按原比例2倍升级图集并设4096上限。不是每件物品做4K。',['runtime-4k-verified.json','ui-review.json'])
add('R01','断锚港出航：已有4K底图及三张地图','已有4K复用','首批建议',[3840,2160],
    [x['file'] for x in background['existing4KCandidates']],
    '4张已完成4K素材及母图已找到，尺寸与manifest校验一致；尚未接入当前游戏。','底图目标全屏；地图窗口尺寸待接入核对',
    '不重复制作。1张底图替换现役断锚港出航背景，另3张为分层地图；接入前需审核构图、热点、船位和遮挡。此项4个文件不计入93张新4K候选。',
    ['D:/Game/Artifacts/DepartureDockLayers4K20261013/manifest.json'])

keep = [
    ('216张卡牌插画','现有源1254×1254、保留1024导入；4K正常卡面插画约432×432，工坊约328×328。卡名等文字的清晰度应由字体渲染修正。'),
    ('36张整合卡框、150张部件小图标','当前显示尺寸内采样足够；部件大展示用另一套ModularShip/Layers，已列B01/B02。'),
    ('26张开场／藏宝／冲撞漫画','当前3344×1882，且已匹配历史AI高清处理输出；全屏仅约1.15倍，先保留，后续对比再决定原生4K重制。'),
    ('九宫格边框、顶部栏、平铺纸纹／木纹','边角保留像素、中央重复或伸展，不能按整个面板宽度判断4K需求。排除以九宫格或平铺方式显示的纹理。'),
    ('9张航行远景、结算宝箱和日志','远景显示约1296×1011，考虑裁切后仅约1.075倍；结算配图原图已足够。'),
    ('船只、接舷人物、30张仪器部件','普通接舷人物13张已足；特殊船员6张图集约1.16倍，船只最大出航轮廓个别约1.48倍，可留后续2K单格优化，不做每人每船4K。30张仪器保持1024。'),
    ('旧版、备用、已被覆盖背景与框','19张旧背景及无现役渲染调用的旧框不纳入新制作。'),
]

totals = {section: sum(g['count'] for g in groups if g['section']==section)
          for section in ['4K级候选','2K／3K候选','已有4K复用']}
assert totals == {'4K级候选':93,'2K／3K候选':124,'已有4K复用':4}, totals
all_files = [f['file'] for g in groups for f in g['files']]
assert len(all_files)==len(set(all_files)), 'Duplicate candidates'
assert all(Path(f).is_file() for f in all_files), 'Missing source file'
assert runtime['status']=='passed' and not runtime['errors']
changed = [x['resource'] for x in inventory
           if Path(x['file']).stat().st_size != x['bytes'] or Path(x['file']).stat().st_mtime_ns != x['modifiedNs']]
assert not changed, changed

report = {'date':'2026-10-13','approvalState':'待用户审核；未开始制作或接入','generatedAssetsThisAudit':0,
          'productFilesModifiedThisAudit':0,'sourceInventoryCount':len(inventory),'runtimeCases':len(runtime['frames']),
          'runtimeGraphicMeasurements':len(runtime['rows']),'measurementErrors':runtime['errors'],
          'scope':'当前项目源代码和资源，3840×2160、16:9、Canvas缩放2；52个界面／港口组合实测，未遍历变体由源码目录与布局推算。不是对所有存档、动画帧、宽高比的穷举测试。',
          'totals':totals,'firstBatchSuggestion':['A01','A02','A03','R01'],
          'firstBatchNewSourceCount':13,'firstBatchReuseCount':4,'groups':groups,'retainOrDefer':keep,
          'productionRules':['先按用户批准编号制作，当前没有任何批准。','4K级指约3840～4096像素宽，不是所有素材都改16:9。宽幅档案、图集保持各自比例。',
          '制作需恢复或重新描绘细节；简单改尺寸不能解决已有模糊。','新源图接入时检查默认和Standalone导入上限，避免高清源再次被2048缩小。',
          '保留实时文字、透明边缘、布局与可交互位置；不能将文字模糊归因于背景分辨率。',
          '先做少量代表图在实际4K游戏中对比，再扩展批准的同组变体；包体和运行内存需在实际导入／构建后测量。']}
(OUT/'approval-list.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

with (OUT/'approval-list.csv').open('w',encoding='utf-8-sig',newline='') as handle:
    writer=csv.writer(handle)
    writer.writerow(['审核意见（待填写）','编号','类别','组名','优先级','当前源图尺寸','当前桌面导入上限','建议源图尺寸','建议导入上限','使用尺寸／4K屏幕','理由','备注','完整源文件路径'])
    for g in groups:
        for f in g['files']:
            writer.writerow(['待审核',g['id'],g['section'],g['name'],g['priority'],'×'.join(map(str,f['currentSize'])),
                             f['currentDesktopMaxSize'],'×'.join(map(str,f['proposedSize'])),f['proposedImportMaxSize'],
                             g['displayAt4K'],g['reason'],g['note'],f['file']])

lines=['游戏清晰度与4K资源审核清单','日期：2026-10-13','状态：待用户审核；本轮制作0张、接入0张。',
       report['scope'],f'盘点853张当前源图；52个界面／港口组合、1123条图形测量，零测量错误。',
       '建议候选：93张4K级源图、124张2K／3K源图（含可选项）；另4张已有4K可复用。',
       '建议首批：A01＋A02＋A03，共13张新背景；R01复用已有4张，不重复生成。','']
for g in groups:
    lines += [f"{g['id']}  {g['name']}｜{g['count']}张｜{g['priority']}｜{g['section']}",
              '建议尺寸：'+'×'.join(map(str,g['proposedSize'])),'屏幕使用：'+g['displayAt4K'],
              '依据：'+g['reason'],'备注：'+g['note']]
    for f in g['files']:
        lines.append('  '+f['file']+'  当前'+'×'.join(map(str,f['currentSize'])))
    lines.append('')
lines+=['保留／暂缓']+[name+'：'+reason for name,reason in keep]+['','后续制作边界']+report['productionRules']
(OUT/'审核清单.txt').write_text('\n'.join(lines),encoding='utf-8-sig')

esc=html.escape
def section_table(section):
    rows=[]
    for g in groups:
        if g['section']!=section: continue
        dim=' × '.join(map(str,g['proposedSize']))
        details=''.join(f'<li><a href="{esc(Path(f["file"]).as_uri())}">{esc(Path(f["file"]).name)}</a><small>{f["currentSize"][0]}×{f["currentSize"][1]} · {esc(f["file"])}</small></li>' for f in g['files'])
        rows.append(f'<tr data-search="{esc(g["id"]+" "+g["name"]+" "+" ".join(f["file"] for f in g["files"]))}"><td><b>{g["id"]}</b><span class="pill">{g["priority"]}</span></td><td><strong>{esc(g["name"])}</strong><p>{esc(g["reason"])}</p><details><summary>展开 {g["count"]} 个源文件</summary><ul>{details}</ul></details></td><td>{g["count"]}</td><td>{dim}<small>屏幕：{esc(g["displayAt4K"])}</small></td><td>{esc(g["note"])}</td></tr>')
    return '<table><thead><tr><th>编号</th><th>内容与审核依据</th><th>数量</th><th>建议源尺寸</th><th>制作／复用边界</th></tr></thead><tbody>'+''.join(rows)+'</tbody></table>'

document='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>游戏4K资源审核清单</title>
<style>body{margin:0;background:#f4f2ed;color:#202b36;font:15px/1.65 "Segoe UI","Microsoft YaHei",sans-serif}main{max-width:1440px;margin:0 auto;padding:42px 32px 70px}h1{font-size:34px;margin:10px 0}h2{margin:38px 0 14px;font-size:23px}p{margin:8px 0}.eyebrow{color:#687785;letter-spacing:.12em}.status{display:inline-block;background:#fff0c8;color:#825519;padding:6px 13px;border-radius:6px}.lead{max-width:1050px;color:#4b5966}.stats{display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin:26px 0}.stat{background:white;border:1px solid #d8dedf;border-radius:10px;padding:18px 24px}.stat b{font-size:35px;display:block;color:#173f56}.callout{border-left:4px solid #bd9446;background:#fffaf0;padding:17px 22px;margin:22px 0}.tools{display:flex;flex-wrap:wrap;gap:14px;align-items:center;margin:22px 0}input{font:inherit;padding:10px 14px;border:1px solid #b9c6ce;border-radius:6px;min-width:280px}a{color:#176082}table{width:100%;border-collapse:collapse;background:#fff;border:1px solid #d8dedf}th{text-align:left;background:#203c4d;color:white;padding:12px;font-weight:500}td{border-bottom:1px solid #e0e5e7;padding:15px 13px;vertical-align:top}td:nth-child(1){width:90px}td:nth-child(2){width:34%}td:nth-child(4){width:165px}td:nth-child(5){width:26%;font-size:13px;color:#526170}small{display:block;color:#6c7984;font-size:12px;word-break:break-all;margin-top:6px}.pill{display:block;font-size:11px;margin-top:8px;color:#926311}details{margin-top:10px}summary{cursor:pointer;color:#176082}ul{padding-left:20px}li{margin:7px 0}details li{font-size:12px;word-break:break-all}footer{margin-top:34px;color:#6c7984;font-size:12px}tr[hidden]{display:none}.keep{display:grid;grid-template-columns:1fr 1fr;gap:12px}.keep article{background:white;border:1px solid #d8dedf;padding:16px;border-radius:8px}.keep p{color:#526170;font-size:13px}@media(max-width:900px){main{padding:24px 14px}.stats,.keep{grid-template-columns:1fr}table{display:block;overflow:auto}h1{font-size:28px}td{min-width:100px}td:nth-child(2){min-width:280px}}@media print{input,.tools{display:none}main{padding:0}td{font-size:10px}details{display:none}}</style>
<main><div class="eyebrow">SHATTERED SAIL · RESOURCE REVIEW · 2026.10.13</div><h1>哪些内容值得升级到 4K？</h1><span class="status">待你审核 · 本轮未制作或接入新图</span><p class="lead">以当前项目在 3840 × 2160 屏幕上的实际显示为依据：盘点 853 张源图，测量 52 个界面／港口组合、1123 条图形记录，并按源码补查图集与变体。分清整幅插画、单格图集和九宫格边框，再决定分辨率。</p>
<div class="stats"><div class="stat"><b>93</b>4K级新源图候选 · A01—A13</div><div class="stat"><b>124</b>2K／3K候选，含可选项 · B01—B05</div><div class="stat"><b>4</b>已完成4K，可复用 · R01</div></div>
<div class="callout"><strong>首批建议：A01 ＋ A02 ＋ A03，共 13 张新背景；R01 复用现有 4 张。</strong><p>先对港口主场景、收藏室和主菜单做清晰度对比，再推进战斗、人物档案和其余设施。以下是候选清单，不代表已经批准制作。</p></div>
<div class="tools"><input id="search" placeholder="筛选编号、内容或文件名" aria-label="筛选清单"><a href="approval-list.csv">下载逐文件 CSV（221 条）</a><a href="审核清单.txt">完整文字清单</a><a href="approval-list.json">结构化审核记录</a></div>
<h2>A · 建议升级到 4K 级的内容</h2><p class="lead">“4K级”表示约3840～4096像素宽。人物档案和纸面保持各自比例，不统一拉成16:9；炮战最紧近景另有更高分辨率需求。</p>'''+section_table('4K级候选')+'''
<h2>B · 确有像素不足，但 2K／3K 足够</h2><p class="lead">这些内容不计入93张4K候选。特别是部件大展示，与512像素的小图标使用不同资源。</p>'''+section_table('2K／3K候选')+'''
<h2>R · 已有高清素材，审核后复用</h2>'''+section_table('已有4K复用')+'''
<h2>保留或暂缓的内容</h2><div class="keep">'''+''.join('<article><strong>'+esc(n)+'</strong><p>'+esc(r)+'</p></article>' for n,r in keep)+'''</div>
<h2>制作与验证约定</h2><ol>'''+''.join('<li>'+esc(r)+'</li>' for r in report['productionRules'])+'''</ol>
<footer>审核范围：当前资源与源码；16:9 4K、Canvas缩放2。实际测量加目录／布局推算，不是所有存档、动画帧和宽高比的穷举。现有853张源图在审核期间尺寸、文件大小和修改时间均未改变。证据：current-source-inventory.json、runtime-4k-verified.json、background-review.json、cinematic-review.json、ui-review.json、ui-component-showroom-sizing.json。<br>CSV中的审核意见列可供你逐项填写；本页面没有发送批准或启动制作的功能。</footer></main>
<script>document.getElementById('search').addEventListener('input',function(){const q=this.value.toLowerCase().trim();document.querySelectorAll('tr[data-search]').forEach(r=>r.hidden=!r.dataset.search.toLowerCase().includes(q));});</script></html>'''
(OUT/'index.html').write_text(document,encoding='utf-8')
print(json.dumps({'totals':totals,'groups':len(groups),'uniqueCandidateFiles':len(all_files),'sourceChanges':changed,'runtimeStatus':runtime['status']},ensure_ascii=True))
