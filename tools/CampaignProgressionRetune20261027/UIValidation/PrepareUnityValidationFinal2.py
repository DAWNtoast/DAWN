"""Reuse the idle shared Unity checkout with a fresh QA-only save identity."""
import csv
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TOOL = Path(__file__).resolve().parent
SOURCE = ROOT / 'UnityClient'
PROJECT = ROOT / 'tmp/shipyard-detail-clean-20261011'
EVIDENCE = ROOT / 'outputs/campaign-progression-retune-20261027/UIValidation/SnapshotFinal2'
COMPANY = 'ShatteredSailCampaignProgressionRetune'
PRODUCT = 'Campaign20261027ProgressionFinal2UI'
RETIRED = 'Assets/_Project/Scenes/UIPreviewsForMe'
PARTS = ('Assets', 'Packages', 'ProjectSettings')

def digest(path):
    with path.open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()

def unlinked(path):
    assert path.resolve(strict=True) == path.absolute(), f'Redirected path: {path}'
    for item in [path, *path.parents]:
        assert not item.stat(follow_symlinks=False).st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT, f'Linked path: {item}'

def inputs(base):
    result = {}
    for part in PARTS:
        unlinked(base / part)
        for directory, directories, names in os.walk(base / part, followlinks=False):
            relative = Path(directory).relative_to(base).as_posix()
            if relative == RETIRED:
                assert not names and not directories, 'Retired previews must remain empty.'
                continue
            for name in directories + names:
                entry = Path(directory) / name
                assert not entry.stat(follow_symlinks=False).st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT, f'Linked input: {entry}'
            for name in names:
                path = Path(directory) / name
                relative = path.relative_to(base).as_posix()
                if relative == RETIRED + '.meta':
                    continue
                result[relative] = path
    return result

def manifest(path, rows):
    with path.open('w', encoding='utf-8', newline='') as handle:
        writer = csv.DictWriter(handle, fieldnames=['Path', 'Bytes', 'SHA256'])
        writer.writeheader()
        writer.writerows(rows)

def assert_idle():
    command = "@(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('/','\\').Contains('D:\\Game\\tmp\\shipyard-detail-clean-20261011') }).Count"
    count = subprocess.check_output(['powershell', '-NoProfile', '-Command', command], text=True).strip()
    assert count == '0', 'Shared validation checkout is busy.'

assert not EVIDENCE.exists(), 'Use a fresh task snapshot; never replace prior evidence.'
assert_idle()
unlinked(SOURCE)
unlinked(PROJECT)
current, previous = inputs(SOURCE), inputs(PROJECT)
EVIDENCE.mkdir(parents=True)
rows = [{'Path': rel, 'Bytes': path.stat().st_size, 'SHA256': digest(path)} for rel, path in sorted(current.items())]
expected = {row['Path']: row for row in rows}
archived = []
for rel, path in previous.items():
    old_hash = digest(path)
    if rel in expected and old_hash == expected[rel]['SHA256']:
        continue
    archived_path = EVIDENCE / 'PreviousValidationInputs' / rel
    archived_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, archived_path)
    assert digest(archived_path) == old_hash
    archived.append({'Path': rel, 'Bytes': path.stat().st_size, 'SHA256': old_hash})
    if rel not in expected:
        assert path.absolute().is_relative_to(PROJECT.absolute())
        path.unlink()
for row in rows:
    rel = row['Path']
    target = PROJECT / rel
    if not target.exists() or digest(target) != row['SHA256']:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(current[rel], target)
    assert digest(target) == row['SHA256']
    if Path(rel).suffix.lower() in ('.cs', '.meta', '.asmdef', '.asmref', '.json', '.txt', '.csv') or rel.startswith(('ProjectSettings/', 'Packages/')):
        frozen = EVIDENCE / 'FrozenCode' / rel
        frozen.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(current[rel], frozen)
assert current.keys() == inputs(SOURCE).keys()
for row in rows:
    assert digest(current[row['Path']]) == row['SHA256'], 'Main project source drift.'
manifest(EVIDENCE / 'source-manifest.csv', rows)
manifest(EVIDENCE / 'archived-inputs.csv', archived)
settings = PROJECT / 'ProjectSettings/ProjectSettings.asset'
text = settings.read_text(encoding='utf-8-sig')
text = re.sub(r'^  companyName:.*$', '  companyName: ' + COMPANY, text, flags=re.M)
text = re.sub(r'^  productName:.*$', '  productName: ' + PRODUCT, text, flags=re.M)
settings.write_text(text, encoding='utf-8', newline='\n')
(EVIDENCE / 'ProjectSettings-isolated-before.asset').write_bytes(settings.read_bytes())

runner = (ROOT / 'tools/CampaignAudit20261027/RunPlayMode.ps1').read_text(encoding='utf-8-sig')
runner = runner.replace("(Join-Path $PSScriptRoot '../..')", "(Join-Path $PSScriptRoot '../../..')")
runner = runner.replace("(Join-Path $workspace 'Artifacts')", "(Join-Path $workspace 'outputs/campaign-progression-retune-20261027/UIValidation')")
runner = runner.replace('workspace/Artifacts output', 'task UIValidation output')
runner = runner.replace("(Join-Path $destination 'Visuals')", "(Join-Path $workspace ('Artifacts/CampaignProgressionRetune20261027/' + (Split-Path $destination -Leaf) + '/Visuals'))")
runner = runner.replace('ShatteredSailCampaignAudit', COMPANY).replace('Campaign20261027NewGame', PRODUCT)
(TOOL / 'RunUnityValidationFinal2.ps1').write_text(runner, encoding='utf-8')

project_rows = [{'Path': rel, 'Bytes': path.stat().st_size, 'SHA256': digest(path)} for rel, path in sorted(inputs(PROJECT).items())]
manifest(EVIDENCE / 'project-input-manifest.csv', project_rows)
metadata = {'Result': 'PASS', 'Source': str(SOURCE), 'Project': str(PROJECT), 'Files': len(rows), 'ProjectFiles': len(project_rows), 'ArchivedInputs': len(archived),
            'SourceManifestSHA256': digest(EVIDENCE / 'source-manifest.csv').upper(), 'ProjectInputManifestSHA256': digest(EVIDENCE / 'project-input-manifest.csv').upper(),
            'Company': COMPANY, 'Product': PRODUCT,
            'EditModeFixtures': ['ShatteredSail.Presentation.Tests.SpecialCardDossierTests', 'ShatteredSail.Core.Tests.BoardingComponentDropSelectionTests', 'ShatteredSail.Core.Tests.CoreTreasureHuntTests'],
            'PlayModeFilter': 'ShatteredSail.Presentation.Tests.MainMenuSceneTests.SchoolUnlockNotice_',
            'CachePolicy': 'Reuse existing shared Library; no new import-cache copy. Keep shared cache because release/build tools still depend on it.',
            'StoragePolicy': 'Distinct company/product; no formal or prior QA saves are read, reset or migrated.'}
(EVIDENCE / 'snapshot.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding='utf-8')
assert_idle()
print(json.dumps(metadata, ensure_ascii=False))
