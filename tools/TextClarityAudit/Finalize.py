import hashlib,json,xml.etree.ElementTree as ET
from pathlib import Path
root=Path('D:/Game')
out=root/'Artifacts/TextClarity20261013'
source=root/'UnityClient/Assets/_Project'
scratch=root/'tmp/card-clarity-20261013/Assets/_Project'
files=['Presentation/Runtime/'+name+'.cs' for name in ['NavigationHeaderSkin','NavigationDashboardLayout','SettlementTextCentering','CoreGameplayDemoController','CoreGameplayDepartureDockScene','CoreGameplaySchoolCardPresentation','CoreGameplayBattleRulesPresentation']]+['Tests/EditMode/'+name+'.cs' for name in ['GameTypographyTests','NavigationDashboardTests','SettlementTextPixelAlignmentTests']]
hashes=[]
for name in files:
 a=hashlib.sha256((source/name).read_bytes()).hexdigest(); b=hashlib.sha256((scratch/name).read_bytes()).hexdigest()
 assert a==b,name
 hashes.append({'file':name,'sha256':a,'matchesValidatedProject':True})
tests=[]
for name in ['EditModeVerified.xml','PlayMode.xml']:
 r=ET.parse(out/name).getroot()
 assert r.get('result')=='Passed' and r.get('failed')=='0',name
 tests.append({'file':name,'total':int(r.get('total')),'passed':int(r.get('passed')),'failed':int(r.get('failed'))})
reports=[json.loads((out/v/'report.json').read_text(encoding='utf-8-sig')) for v in ['Baseline','Fixed']]
assert all(r['status']=='passed' for r in reports)
phases=[]
for r in reports:
 for f in r['frames']:
  if f['state']!='Settlement':continue
  labels=[t for t in f['texts'] if any('SettlementTextCentering' in e for e in t['meshEffects'])]
  phases.append({'variant':r['variant'],'width':f['width'],'labelCount':len(labels),'halfPixelLabels':sum(t['maximumMeshVertexYResidual']>.45 for t in labels),'maxYResidual':max(t['maximumMeshVertexYResidual'] for t in labels)})
summary={'status':'passed','scope':'Text clarity source changes; existing executable not rebuilt','editMode':tests[0],'playMode':tests[1],'captures':[{k:r[k] for k in ['variant','status','frameCount','textCount','sampledTextCount']} for r in reports],'settlement':phases,'sourceHashes':hashes,'notes':['Original card-title high-density fix and 1024 illustration import policy retained.','Ordinary UI text already follows CanvasScaler; no global replacement renderer.','Initial run found departure/battle rich-text bold; fixed. Two stale typography test assumptions were corrected with separate negative cases, retaining production card emphasis and authored dossier line spacing.','Captures cover Home, Settings, PortHub, PortSettings, Navigation, Settlement. Departure and battle tooltip changes are covered by typography and card-school tests, not a separate before/after screenshot set.']}
(out/'verification-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
notes='''其他文字清晰度修复（2026-10-13）

已完成：
1. 航行海图标题、水粮续航、藏宝图提示按面板放大比例生成足够字形像素，再等比回到原显示字号及文字框；不改变地图坐标或点击层级。
2. 航行标题/船舱/设置以及出航海域标题去掉重复合成粗体。战斗体系说明恢复原生字重；悬浮说明小标题沿用既有深红色强调。卡面使用条件的强调保留。
3. 结算文字居中后按真实屏幕Y像素对齐，位置偏差不超过0.5物理像素。动画结束后的缩放变化会刷新，静止和空文本不重复重建。

验证：116项相关EditMode回归、3项PlayMode导航刷新/结算动效回归全部通过。
修复前后各12张真实Unity界面截图，1920×1080与3840×2160，覆盖6页、212条Text记录。普通菜单/设置/港口共8张截图逐像素一致。
结算页21个标签中，修复前1080p有10处、4K有12处Y半像素偏移；修复后最大偏差0.000122屏幕像素（浮点误差）。
对比图为相同屏幕区域的原像素裁图，不缩放、不锐化，建议100%查看。

保留此前卡名像素密度修复和1024像素卡图设置。本次修改源码，尚未重新生成Windows/macOS发布包；旧exe不会自动包含修复。

原像素报告：Comparison/index.html
导航4K对比：Comparison/navigation-3840.png
结算4K对比：Comparison/settlement-3840.png
机器验证：verification-summary.json
'''
(out/'修复与验证.txt').write_text(notes,encoding='utf-8')
readme=root/'README.md'
text=readme.read_text(encoding='utf-8-sig')
marker='其他文字清晰度修复（2026-10-13，源码未重新打包）'
if marker not in text:
 note=marker+'：航行海图读数与藏宝提示按实际放大比例补足字形像素；航行/出航标题去除重复合成粗体，战斗说明保留颜色强调；结算文字对齐屏幕像素并随动效刷新。116项相关编辑器回归、3项交互/动效检查通过；1080p/4K前后各12张真实界面对比覆盖212条文字记录。见 [原像素对比](Artifacts/TextClarity20261013/Comparison/index.html) 与 [修复与验证](Artifacts/TextClarity20261013/修复与验证.txt)。'
 paragraphs=text.split('\n\n',2)
 text=paragraphs[0]+'\n\n'+paragraphs[1]+'\n\n'+note+'\n\n'+paragraphs[2]
 readme.write_text(text,encoding='utf-8')
print(json.dumps({'status':'passed','tests':tests,'sourceFilesVerified':len(hashes),'settlement':phases}))
