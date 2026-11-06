"""Install immutable earned campaign checkpoints as native-UI test inputs."""
import csv, hashlib, json, shutil, subprocess, uuid
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
TOOL = Path(__file__).resolve().parent
BASE = ROOT/'outputs/campaign-boss-review-20261027/UIValidation'
SNAPSHOT = BASE/'SnapshotCheckpoint01'
PROJECT = ROOT/'tmp/shipyard-detail-clean-20261011'
RUN = ROOT/'outputs/campaign-boss-review-20261027/Direct02'
TESTS = PROJECT/'Assets/_Project/Tests/PlayMode'
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def idle():
    query = "@(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('/','\\').Contains('D:\\Game\\tmp\\shipyard-detail-clean-20261011') }).Count"
    assert subprocess.check_output(['powershell','-NoProfile','-Command',query],text=True).strip() == '0', 'Shared project is running.'
idle()
assert not SNAPSHOT.exists(), 'Preserve previous snapshot.'
data=json.loads((RUN/'campaign.json').read_text(encoding='utf-8-sig'))
assert data['completed'] and len(data['firstKills'])==13
SNAPSHOT.mkdir(parents=True)
for name in ['CampaignBossReviewNewProfileTests.cs','CampaignBossReviewNewProfileTests.cs.meta']:
    source=TESTS/name
    archive=SNAPSHOT/'PreviousAuditInputs'/name
    archive.parent.mkdir(exist_ok=True)
    shutil.copy2(source,archive)
    assert digest(source)==digest(archive)
    source.unlink()
inputs=TESTS/'CampaignBossCheckpointInputs'
assert not inputs.exists()
inputs.mkdir()
inputs.with_suffix('.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
selected=[]
for boss, record in sorted(data['firstKills'].items()):
    trip=record['firstKill']['trip']
    source=RUN/'Checkpoints'/f'before-trip-{trip}-{boss}.json'
    target=inputs/(boss+'.json')
    shutil.copy2(source,target)
    assert digest(source)==digest(target)
    target.with_suffix('.json.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
    selected.append({'Boss':boss,'Trip':trip,'Source':str(source),'Installed':str(target),'SHA256':digest(source)})
(SNAPSHOT/'earned-checkpoint-manifest.json').write_text(json.dumps(selected,ensure_ascii=False,indent=2),encoding='utf-8')
for row in selected:
    saved=SNAPSHOT/'EarnedCheckpoints'/Path(row['Installed']).name
    saved.parent.mkdir(exist_ok=True)
    shutil.copy2(row['Source'],saved)
fixture=TOOL/'CampaignBossCheckpointUiTests.cs'
shutil.copy2(fixture,TESTS/fixture.name)
(TESTS/(fixture.name+'.meta')).write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
shutil.copy2(fixture,SNAPSHOT/fixture.name)
settings=PROJECT/'ProjectSettings/ProjectSettings.asset'
text=settings.read_text(encoding='utf-8-sig').replace('productName: CampaignBossReview20261027NewGame','productName: CampaignBossReview20261027CheckpointUI')
settings.write_text(text,encoding='utf-8',newline='\n')
(SNAPSHOT/'ProjectSettings-isolated-before.asset').write_bytes(settings.read_bytes())
runner=(TOOL/'RunNativeValidation.ps1').read_text(encoding='utf-8-sig').replace('CampaignBossReview20261027NewGame','CampaignBossReview20261027CheckpointUI')
(TOOL/'RunCheckpointValidation.ps1').write_text(runner,encoding='utf-8')
shutil.copy2(BASE/'Snapshot/source-manifest.csv',SNAPSHOT/'source-manifest.csv')
with (SNAPSHOT/'source-manifest.csv').open(encoding='utf-8-sig',newline='') as handle:
    for row in csv.DictReader(handle): assert digest(ROOT/'UnityClient'/row['Path']).lower()==row['SHA256'].lower(), 'Main source drift'
rows=[]
for part in ('Assets','Packages','ProjectSettings'):
    for path in sorted((PROJECT/part).rglob('*')):
        if not path.is_file() or '/UIPreviewsForMe' in path.as_posix():continue
        rows.append({'Path':path.relative_to(PROJECT).as_posix(),'Bytes':path.stat().st_size,'SHA256':digest(path)})
manifest=SNAPSHOT/'project-input-manifest.csv'
with manifest.open('w',encoding='utf-8',newline='') as handle:
    writer=csv.DictWriter(handle,fieldnames=['Path','Bytes','SHA256']);writer.writeheader();writer.writerows(rows)
record={'Result':'PASS','Project':str(PROJECT),'SourceManifestSHA256':digest(SNAPSHOT/'source-manifest.csv').upper(),'ProjectInputManifestSHA256':digest(manifest).upper(),'Company':'ShatteredSailCampaignBossReview','Product':'CampaignBossReview20261027CheckpointUI','EarnedCampaign':str(RUN),'Checkpoints':len(selected),'Method':'Read-only native UI reproduction of real first-kill encounter checkpoints; no campaign balance or resource mutations.'}
(SNAPSHOT/'snapshot.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
idle()
print(json.dumps(record,ensure_ascii=False))
