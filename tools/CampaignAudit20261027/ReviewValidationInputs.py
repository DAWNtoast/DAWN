"""Explain exact post-Unity input differences without altering the original evidence."""
import csv,hashlib,json,re,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[2];snapshot=root/'Artifacts/CampaignAudit20261027/Snapshot'
run=root/'Artifacts/CampaignAudit20261027/UIRun01';project=root/'tmp/shipyard-detail-clean-20261011'
review=run/'InputReview';review.mkdir(exist_ok=True)
def sha(data):return hashlib.sha256(data).hexdigest()
def read(path):
    with path.open(encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))
manifest=read(snapshot/'project-input-manifest.csv');expected={r['Path'].replace('\\','/'):r for r in manifest}
changed=[]
for rel,row in expected.items():
    path=project/rel
    actual=sha(path.read_bytes()) if path.exists() else None
    if actual!=row['SHA256'].lower():changed.append({'path':rel,'expectedSHA256':row['SHA256'],'actualSHA256':actual})
extras=[p.relative_to(project).as_posix() for part in ['Assets','Packages','ProjectSettings'] for p in (project/part).rglob('*') if p.is_file() and p.relative_to(project).as_posix() not in expected and '/UIPreviewsForMe' not in p.as_posix()]
assert len(changed)==1 and changed[0]['path']=='ProjectSettings/ProjectSettings.asset',changed
assert not extras,extras
old=(snapshot/'FrozenCode/ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8-sig')
old=re.sub(r'^  companyName:.*$','  companyName: ShatteredSailCampaignAudit',old,flags=re.M)
old=re.sub(r'^  productName:.*$','  productName: Campaign20261027NewGame',old,flags=re.M)
variants=[old.encode(),old.replace('\n','\r\n').encode()]
before=next((v for v in variants if sha(v)==changed[0]['expectedSHA256'].lower()),None)
assert before is not None,'Could not reconstruct exact locked pre-run settings bytes.'
after=(project/'ProjectSettings/ProjectSettings.asset').read_bytes()
normalize=lambda b:b.decode('utf-8-sig').replace('\r\n','\n')
assert normalize(before)==normalize(after),'Not a pure encoding/newline normalization.'
(review/'ProjectSettings-before-unity.asset').write_bytes(before)
(review/'ProjectSettings-after-unity.asset').write_bytes(after)
source_changes=[]
for row in read(snapshot/'source-manifest.csv'):
    path=root/'UnityClient'/row['Path']
    if not path.exists() or sha(path.read_bytes())!=row['SHA256'].lower():source_changes.append(row['Path'])
assert not source_changes,source_changes
tests=ET.parse(run/'results.xml').getroot();assert tests.attrib['result']=='Passed'
data={'Result':'PASS_WITH_NEWLINE_NORMALIZATION','TestResult':tests.attrib['result'],'Passed':int(tests.attrib['passed']),
      'OriginalGuardResult':'FAIL retained verbatim in completion.json','ProjectFileCount':len(expected),'ChangedFiles':changed,
      'ExpectedCRLFCount':before.count(b'\r\n'),'ActualCRLFCount':after.count(b'\r\n'),
      'ExpectedLFCount':before.count(b'\n'),'ActualLFCount':after.count(b'\n'),'NormalizedTextExactlyEqual':True,
      'NoSemanticSettingsChanges':True,'AllOtherProjectInputsByteIdentical':True,'UnexpectedProjectInputs':extras,
      'ProductionMainInputsUnchanged':True,'MainSourceFileCount':len(read(snapshot/'source-manifest.csv')),
      'Company':'ShatteredSailCampaignAudit','Product':'Campaign20261027NewGame'}
(run/'input-normalization-review.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
(run/'summary.json').write_text(json.dumps({'Result':'Passed','Total':1,'Passed':1,'Failed':0,'Skipped':0,'ExitCode':0,'InputReview':'PASS_WITH_NEWLINE_NORMALIZATION'},indent=2),encoding='utf-8')
print(json.dumps(data,ensure_ascii=False))
