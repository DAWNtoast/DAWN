"""Summarize the targeted current-source Unity checks and their scope."""
import csv, hashlib, json, xml.etree.ElementTree as ET
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/'outputs/campaign-progression-retune-20261027/UIValidation'
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
snapshot=read(BASE/'Snapshot/snapshot.json')
runs=[]
for name in ('EditMode01','SchoolNotice01'):
    run=BASE/name
    summary=read(run/'summary.json')
    completion=read(run/'completion.json')
    assert summary['Result']=='Passed' and summary['Failed']==0 and summary['Skipped']==0
    assert completion['InputValidationResult']=='PASS'
    assert completion['SourceManifestSHA256']==snapshot['SourceManifestSHA256']
    tree=ET.parse(run/'results.xml')
    fixtures=[{'Name':x.attrib.get('fullname'),'Total':int(x.attrib.get('total',0)),
               'Passed':int(x.attrib.get('passed',0)),'Failed':int(x.attrib.get('failed',0))}
              for x in tree.findall('.//test-suite') if x.attrib.get('type')=='TestFixture']
    runs.append({'Name':name,'Summary':summary,'Completion':completion,'Fixtures':fixtures,
                 'Tests':[{'Name':x.attrib['fullname'],'Result':x.attrib['result']}
                          for x in tree.findall('.//test-case')],'Directory':str(run)})
with (BASE/'Snapshot/source-manifest.csv').open(encoding='utf-8-sig',newline='') as handle:
    source=list(csv.DictReader(handle))
assert all(digest(ROOT/'UnityClient'/x['Path'])==x['SHA256'] for x in source)
visuals=ROOT/'Artifacts/CampaignProgressionRetune20261027/SchoolNotice01/Visuals'
cleanup=read(BASE/'cleanup.json')
assert cleanup['Result']=='PASS' and cleanup['MainProjectUnchanged']
report={'Result':'PASS','Snapshot':snapshot,'Runs':runs,'MainSourceStillMatchesFrozenManifest':True,
 'SchoolUnlockScreenshots':len(list(visuals.glob('*.png'))),'SchoolUnlockVisualIndex':str(visuals/'index.html'),
 'Coverage':['Dossier UI displays the source-derived 20 nautical miles per XP, retains saved XP/rank display, and passes every card effect/lore/layout readability check.',
             'Existing boarding component drop selection fixture retains its authored probability/selection checks.',
             'Treasure/ordinary voyage XP checks cover the halved award, eligibility, scale normalization, single return commit, and tutorial exceptions.',
             'Real scene PlayMode verifies school unlock notification after confirmed return, persistence without replay, multi-page Lv2-to-Lv4 notices, click blocking, Escape advance, no-unlock cases and failed-save behavior.'],
 'Limitations':'Targeted automated Unity tests; this is not a manual gameplay pass or a full campaign UI replay. Continuous progression and boss pacing must be evaluated from the separate new campaign runs.',
 'Cleanup':cleanup}
(BASE/'unity-validation-summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
text=['Unity 当前源码验证：PASS',
      f"EditMode：{runs[0]['Summary']['Passed']} 项通过；PlayMode 学派解锁流程：{runs[1]['Summary']['Passed']} 项通过。",
      f"学派解锁真实截图：{report['SchoolUnlockScreenshots']} 张。",
      '经验说明、掉落选择、航程经验与结算、真实升级提示均通过；每轮输入封印及收尾主工程哈希检查通过。',
      '仅为定向原生自动测试，不能代表完整新档到传奇的逐屏实玩。',
      '共享 Library 按既有构建工具依赖保留；临时缓存清理详见 cleanup.json。']
(BASE/'unity-validation-summary.txt').write_text('\n'.join(text)+'\n',encoding='utf-8')
print(json.dumps({'Result':'PASS','EditMode':runs[0]['Summary'],'PlayMode':runs[1]['Summary'],
                  'Screenshots':report['SchoolUnlockScreenshots'],'Cleanup':cleanup},ensure_ascii=False))
