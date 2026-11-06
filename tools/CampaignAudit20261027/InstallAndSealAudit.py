import csv,hashlib,json,shutil
from pathlib import Path
root=Path(__file__).resolve().parents[2];tool=Path(__file__).resolve().parent
project=root/'tmp/shipyard-detail-clean-20261011';evidence=root/'Artifacts/CampaignAudit20261027/Snapshot'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
source=tool/'CampaignNewProfileAuditTests.cs'
target=project/'Assets/_Project/Tests/PlayMode'/source.name
shutil.copy2(source,target)
target.with_suffix('.cs.meta').write_text('fileFormatVersion: 2\nguid: 6405bd13fbeb43cb8d941861fbc9ef3a\n',encoding='utf-8')
archive=evidence/'AuditTools';archive.mkdir(exist_ok=True)
shutil.copy2(source,archive/source.name)
old=(root/'tools/ReleaseBuild/RunTests.ps1').read_text(encoding='utf-8-sig')
old=old.replace("if ($settings -notmatch '(?m)^  companyName: .*Validation.*$') { throw 'Use a validation storage identity to protect normal player saves.' }", "if ($settings -notmatch '(?m)^  companyName: ShatteredSailCampaignAudit\\r?$' -or $settings -notmatch '(?m)^  productName: Campaign20261027NewGame\\r?$') { throw 'Campaign audit storage identity is required.' }")
old=old.replace("(Join-Path $PSScriptRoot 'AssertMetaGuids.ps1')","(Join-Path $workspace 'tools/ReleaseBuild/AssertMetaGuids.ps1')")
(tool/'RunPlayMode.ps1').write_text(old,encoding='utf-8')
shutil.copy2(tool/'RunPlayMode.ps1',archive/'RunPlayMode.ps1')
rows=[]
for part in ['Assets','Packages','ProjectSettings']:
    for path in sorted((project/part).rglob('*')):
        if not path.is_file():continue
        rel=path.relative_to(project).as_posix()
        if '/UIPreviewsForMe' in rel:continue
        rows.append({'Path':rel,'Bytes':path.stat().st_size,'SHA256':sha(path)})
manifest=evidence/'project-input-manifest.csv'
with manifest.open('w',encoding='utf-8',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=['Path','Bytes','SHA256']);writer.writeheader();writer.writerows(rows)
record=json.loads((evidence/'preparation.json').read_text(encoding='utf-8'))
record.update(Result='PASS',ProjectInputManifestSHA256=sha(manifest),AuditTestSHA256=sha(source),IsolatedChanges=['ProjectSettings company/product identity','CampaignNewProfileAuditTests.cs and .meta'])
(evidence/'snapshot.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(record,ensure_ascii=False))
