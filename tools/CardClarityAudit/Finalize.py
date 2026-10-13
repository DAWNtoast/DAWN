from pathlib import Path
import hashlib, json, xml.etree.ElementTree as ET
root=Path("D:/Game")
out=root/"Artifacts/CardClarity20261013"
files=["Assets/_Project/Presentation/Runtime/CardRibbonTitleEffect.cs","Assets/_Project/Editor/CardSchoolAssetImport.cs","Assets/_Project/Tests/EditMode/CardRibbonTitleDensityTests.cs","Assets/_Project/Tests/EditMode/CardSchoolPresentationTests.cs"]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
source=[]
for f in files:
    a,b=root/"UnityClient"/f,root/"tmp/card-clarity-20261013"/f
    assert sha(a)==sha(b),f
    source.append({"file":f,"sha256":sha(a),"matchesValidated":True})
tests={}
for stage in ["EditMode","PlayMode"]:
    r=ET.parse(out/(stage+".xml")).getroot()
    tests[stage]={k:r.get(k) for k in ["result","total","passed","failed","skipped"]}
    assert r.get("result")=="Passed" and r.get("failed")=="0"
checks={}
for stage in ["Baseline512Retry","FixedTitle512","FixedTitle1024"]:
    r=json.loads((out/stage/"report.json").read_text(encoding="utf-8-sig"))
    assert r["status"]=="passed" and r["failedChecks"]==0
    checks[stage]={"catalogChecks":r["catalogChecks"],"representativeChecks":len(r["representatives"]),"failed":0}
for rel in ["Assets/_Project/Resources/CardArt/Western","Assets/_Project/Resources/CardSchools/Art"]:
    for p in (root/"UnityClient"/rel).glob("*.png"):
        assert sha(p)==sha(root/"tmp/card-clarity-20261013"/p.relative_to(root/"UnityClient")),str(p)
result={"status":"source-updated-and-verified","releaseRebuilt":False,"sourceMatchesValidation":source,"tests":tests,"renderChecks":checks,"illustrationSourcesUnchanged":216,"illustrationMaxTextureSize":1024,"report":"Comparison/index.html","limits":["Unity editor offscreen captures use actual ScreenSpaceCamera canvases at 1920x1080 and 3840x2160; no claim of new packaged-player validation.","Profiler texture memory is editor resource memory, not measured total GPU memory."]}
(out/"verification-summary.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
notes=out/"Comparison/检查与对比.txt"
notes.write_text(notes.read_text(encoding="utf-8-sig")+"\n源码落地与回归：卡名密度修复及216张卡图1024导入配置已保留到UnityClient。4个实现/测试文件与隔离验证工程SHA256一致，216张源PNG未改。156项EditMode检查及3项PlayMode悬停/卡面检查全部通过。现有发布exe/ZIP未重新打包。\n",encoding="utf-8")
readme=root/"README.md"
text=readme.read_text(encoding="utf-8-sig")
note="卡牌清晰度修复（2026-10-13，源码未重新打包）：卡名按实际画布与悬停缩放生成高密度字形，保留原曲线、字号和强化后缀；216张独立卡图导入上限由512提高到1024。1080p/4K三组原像素对照各通过1,744项全卡布局检查与28项代表检查，156项编辑器回归及3项悬停/卡面交互检查通过。见 [512/1024与卡名清晰度对比](Artifacts/CardClarity20261013/Comparison/index.html) 与 [验证汇总](Artifacts/CardClarity20261013/verification-summary.json)。\n\n"
needle="当前开发与发布主体为 Unity 桌面客户端"
assert needle in text
if "卡牌清晰度修复（2026-10-13" not in text:
    text=text.replace(needle,note+needle,1)
    readme.write_text(text,encoding="utf-8")
print("Verified source parity, source PNG integrity, 156 editor + 3 interaction checks; report saved.")