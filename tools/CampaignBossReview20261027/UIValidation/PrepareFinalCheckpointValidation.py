"""Update the idle validation checkout to the final miniboss values and earned run."""
import csv, hashlib, json, re, shutil, subprocess, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
TOOL=Path(__file__).resolve().parent
BASE=ROOT/'outputs/campaign-boss-review-20261027/UIValidation'
SNAPSHOT=BASE/'SnapshotCheckpointFinal02'
PROJECT=ROOT/'tmp/shipyard-detail-clean-20261011'
RUN=ROOT/'outputs/campaign-boss-review-20261027/LearningMiniboss02'
TESTS=PROJECT/'Assets/_Project/Tests/PlayMode'
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
query="@(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('/','\\').Contains('D:\\Game\\tmp\\shipyard-detail-clean-20261011') }).Count"
assert subprocess.check_output(['powershell','-NoProfile','-Command',query],text=True).strip()=='0', 'Validation checkout is running'
data=json.loads((RUN/'campaign.json').read_text(encoding='utf-8-sig'))
assert data['completed'] and len(data['firstKills']) == 13, 'A full legal campaign is required'
available={}
for boss,record in data['firstKills'].items():
    available[boss]=RUN/'Checkpoints'/f"before-trip-{record['firstKill']['trip']}-{boss}.json"
required={'boss_route_golden_tide_trade','boss_route_golden_tide_trade_thunder','boss_route_moon_tide_ruins','boss_route_moon_tide_ruins_arcane'}
assert required.issubset(available), 'Wait for legal encounters covering both level-3 and level-5 charts'
assert not SNAPSHOT.exists(), 'Preserve prior evidence'
SNAPSHOT.mkdir(parents=True)
changed=['Core/Runtime/CoreBattleBalance.cs','Core/Runtime/CoreRouteBossCatalog.cs','Core/Runtime/CoreBoardingBossCatalog.cs',
         'Tests/EditMode/CoreNavalBossCalibrationTests.cs','Tests/EditMode/CoreBoardingBossPacingTests.cs','Tests/EditMode/LegendaryWarshipTests.cs','Tests/EditMode/CoreNavalBossBehaviorTests.cs']
for relative in changed:
    source=ROOT/'UnityClient/Assets/_Project'/relative
    target=PROJECT/'Assets/_Project'/relative
    archive=SNAPSHOT/'PreviousInputs'/relative
    archive.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(target,archive)
    shutil.copy2(source,target)
    assert digest(source)==digest(target)
selected=[]
input_dir=TESTS/'CampaignBossCheckpointInputs'
for old in input_dir.iterdir():
    assert old.is_file() and (old.name.endswith('.json') or old.name.endswith('.json.meta'))
    preserved=SNAPSHOT/'PreviousCheckpointInputs'/old.name
    preserved.parent.mkdir(exist_ok=True)
    shutil.copy2(old,preserved)
    assert digest(old)==digest(preserved)
    if old.suffix=='.json' and old.stem not in available: old.unlink()
    elif old.name.endswith('.json.meta') and old.name[:-10] not in available: old.unlink()
for boss, source in sorted(available.items()):
    trip=int(source.name.split('-')[2])
    target=TESTS/'CampaignBossCheckpointInputs'/(boss+'.json')
    shutil.copy2(source,target)
    meta=Path(str(target)+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
    saved=SNAPSHOT/'EarnedCheckpoints'/target.name
    saved.parent.mkdir(exist_ok=True)
    shutil.copy2(source,saved)
    assert digest(source)==digest(target)==digest(saved)
    selected.append({'Boss':boss,'Trip':trip,'Source':str(source),'Installed':str(target),'SHA256':digest(source),'WasRecordedFirstKill':boss in data['firstKills']})
(SNAPSHOT/'earned-checkpoint-manifest.json').write_text(json.dumps(selected,ensure_ascii=False,indent=2),encoding='utf-8')
fixture=(TOOL/'CampaignBossCheckpointUiTests.cs').read_text(encoding='utf-8-sig').replace('CampaignBossReview20261027CheckpointUI','CampaignBossReview20261027LearningUI')
(TESTS/'CampaignBossCheckpointUiTests.cs').write_text(fixture,encoding='utf-8')
(SNAPSHOT/'CampaignBossCheckpointUiTests.cs').write_text(fixture,encoding='utf-8')
settings=PROJECT/'ProjectSettings/ProjectSettings.asset'
text=re.sub(r'(?m)^  productName: .*$', '  productName: CampaignBossReview20261027LearningUI', settings.read_text(encoding='utf-8-sig'))
settings.write_text(text,encoding='utf-8',newline='\n')
(SNAPSHOT/'ProjectSettings-isolated-before.asset').write_bytes(settings.read_bytes())
runner=(TOOL/'RunCheckpointValidation.ps1').read_text(encoding='utf-8-sig').replace('CampaignBossReview20261027CheckpointUI','CampaignBossReview20261027LearningUI')
(TOOL/'RunFinalCheckpointValidation.ps1').write_text(runner,encoding='utf-8')
def manifest(base,name):
    rows=[]
    for part in ('Assets','Packages','ProjectSettings'):
        for path in sorted((base/part).rglob('*')):
            if not path.is_file() or '/UIPreviewsForMe' in path.as_posix(): continue
            rows.append({'Path':path.relative_to(base).as_posix(),'Bytes':path.stat().st_size,'SHA256':digest(path)})
    with (SNAPSHOT/name).open('w',encoding='utf-8',newline='') as handle:
        writer=csv.DictWriter(handle,fieldnames=['Path','Bytes','SHA256']);writer.writeheader();writer.writerows(rows)
    return rows
source_rows=manifest(ROOT/'UnityClient','source-manifest.csv')
project_rows=manifest(PROJECT,'project-input-manifest.csv')
for row in source_rows:
    if row['Path']=='ProjectSettings/ProjectSettings.asset':continue
    assert digest(PROJECT/row['Path'])==row['SHA256'], 'Validation input differs from latest main: '+row['Path']
record={'Result':'PASS','Project':str(PROJECT),'SourceManifestSHA256':digest(SNAPSHOT/'source-manifest.csv').upper(),'ProjectInputManifestSHA256':digest(SNAPSHOT/'project-input-manifest.csv').upper(),'Company':'ShatteredSailCampaignBossReview','Product':'CampaignBossReview20261027LearningUI','EarnedCampaign':str(RUN),'Checkpoints':len(selected),'ProductionFiles':len(source_rows),'ProjectFiles':len(project_rows),'Method':'Latest current source; all 13 legal first-kill encounter inputs from the final-value continuous full-clear campaign; native UI callbacks and complete level-3/5 representative battles. Snapshot diagnostic replay, not continuous UI play.'}
(SNAPSHOT/'snapshot.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(record,ensure_ascii=False))
