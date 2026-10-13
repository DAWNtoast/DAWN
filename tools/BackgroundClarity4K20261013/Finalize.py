import hashlib,json,re,xml.etree.ElementTree as ET
from pathlib import Path
from datetime import datetime,timezone
ROOT=Path('D:/Game');OUT=ROOT/'Artifacts/BackgroundClarity4K20261013'
BUILD=ROOT/'Builds/BackgroundClarity4K20261013/Windows'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
capture=json.loads((OUT/'verification/capture/report.json').read_text(encoding='utf-8-sig'))
tests=ET.parse(OUT/'verification/editmode.xml').getroot().attrib
build=(BUILD/'build-report.txt').read_text(encoding='utf-8-sig')
assert 'Result: Succeeded' in build and 'Errors: 0' in build
assert capture['status']=='passed' and capture['passedFrames']==19
assert int(tests['passed'])==126 and int(tests['failed'])==0
items=[];metas_changed=0;sprites_changed=0
for item in manifest['items']:
 source=Path(item['source']);final=Path(item['finalOutput']);meta=Path(str(source)+'.meta')
 assert sha(source)==sha(final)==item['finalSha256']
 old=Path(item['backup']+'.meta').read_text(encoding='utf-8-sig');new=meta.read_text(encoding='utf-8-sig')
 assert re.search(r'(?m)^guid: (\w+)$',old).group(1)==re.search(r'(?m)^guid: (\w+)$',new).group(1)
 allowed=old
 if item['group']!='A02':allowed=re.sub(r'(?m)^(\s+maxTextureSize:) 2048$',r'\1 4096',allowed)
 if item['group'] in ['A01','A06']:
  allowed=allowed.replace('        width: 1672\n        height: 941','        width: 3840\n        height: 2160');sprites_changed+=1
 assert new==allowed,'Unexpected importer change'
 metas_changed+=old!=new
 items.append({'name':item['name'],'source':str(source),'sha256':sha(source),'metaSha256':sha(meta),'guidPreserved':True})
assert metas_changed==13 and sprites_changed==12
verification={'status':'passed','completedUtc':datetime.now(timezone.utc).isoformat(),'productionInstalled':19,
              'modifiedMetaCount':metas_changed,'fullSpriteRectsUpdated':sprites_changed,'capturedFramesPassed':19,
              'editModePassed':126,'editModeFailed':0,'playerBuild':'Succeeded','buildReport':str(BUILD/'build-report.txt'),
              'executable':str(BUILD/'第七港口.exe'),'buildDirectoryBytes':sum(p.stat().st_size for p in BUILD.rglob('*') if p.is_file()),
              'scope':'19 approved image replacements and 13 importer files only; original file paths and GUIDs preserved. Test tools and API artefacts remain outside production Assets.','items':items}
(OUT/'verification/final.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2),encoding='utf-8')
manifest.update(status='complete',completedUtc=verification['completedUtc'],productionIntegrated=True,verification='verification/final.json',windowsExecutable=verification['executable'])
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
summary=json.loads((OUT/'delivery-summary.json').read_text(encoding='utf-8'))
summary['completion']=verification.copy();summary['completion'].pop('items')
(OUT/'delivery-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
page=(OUT/'index.html').read_text(encoding='utf-8')
page=page.replace('生成完成 · 构图审查详见报告','19 张已接入主项目 · 视觉审查通过')
old='仓库／酒馆专项人工审查快照：已检查 12 张、接受 12 张、其报告范围内尚待 0 张；不据此宣称 19 张全部人工验收。报告可能继续更新，请以链接中的最新记录为准。'
new='视觉审查已完成：六港主场景和主菜单 7 张由主审检查，仓库与酒馆 12 张由独立审查核对（含 36 名人物）；19 张均接受。检查保留构图、主要物体、角色特征和 UI 区域，不代表逐像素一致。'
page=page.replace(old,new)
page=page.replace('此次交付页制作未调用图片 API、未改动图片或产品文件。','本批 19 张图片及 13 份导入设置已接入主项目；提示词、原图备份、API原始输出和校验记录均保留。')
banner='<section class="section"><h2>已完成并接入游戏</h2><p>A01／A02／A03／A06 共 19 张背景，全部实际导入 3840×2160 BC7。19 帧 4K 捕获、126 项 EditMode 测试和 Windows 构建均通过。</p><div class="links"><a href="'+(BUILD/'第七港口.exe').as_uri()+'">打开新版 Windows 游戏</a><a href="'+(BUILD/'build-report.txt').as_uri()+'">Windows 构建报告</a><a href="verification/final.json">最终接入校验</a><a href="visual-acceptance.json">19 张视觉验收</a></div></section>'
page=page.replace('</header>','</header>'+banner,1)
(OUT/'index.html').write_text(page,encoding='utf-8')
print(json.dumps({k:v for k,v in verification.items() if k!='items'},ensure_ascii=True))
