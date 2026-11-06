"""Read-only final source, test, ledger and cache evidence checks."""
import csv
import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/campaign-boss-review-20261027'

def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def main():
    checks = {}
    frozen = OUT / 'FrozenSourceMiniboss'
    with (OUT / 'LearningMiniboss02/source-hashes.csv').open(encoding='utf-8-sig') as f:
        rows = list(csv.DictReader(f))
    drift = []
    for row in rows:
        main_path = ROOT / 'UnityClient/Assets/_Project' / Path(row['Source']).relative_to(frozen)
        if digest(main_path) != row['SHA256'] or digest(Path(row['Snapshot'])) != row['SHA256']:
            drift.append(str(main_path))
    component = Path('Resources/Data/component_catalog_v2.csv')
    checks['productionSourceFiles'] = len(rows) + 1
    checks['sourceDrift'] = drift
    checks['componentCsvMatches'] = digest(ROOT / 'UnityClient/Assets/_Project' / component) == digest(OUT / 'LearningMiniboss02' / component)
    tests = ET.parse(ROOT / 'Artifacts/BossCalibration20261024/CampaignMiniboss20261027Final/results.xml').getroot()
    checks['coreTests'] = {k: tests.attrib.get(k) for k in ['result', 'total', 'passed', 'failed', 'skipped']}
    checks['campaignComplete'] = load(OUT / 'LearningMiniboss02/campaign.json')['completed']
    checks['finalLedger'] = load(OUT / 'EconomyReview/LearningMiniboss02-ledger-review.json')['status']
    checks['newProfileUi'] = load(OUT / 'UIValidation/NewProfile02/summary.json')
    checks['checkpointUi'] = load(OUT / 'UIValidation/CheckpointFinal02/summary.json')
    runs = load(OUT / 'all-run-cleanup-summary.json')
    checks['remainingCampaignBuilds'] = [r['run'] for r in runs['runs'] if (OUT / r['run'] / 'Build').exists()]
    regressions = load(OUT / 'miniboss-regression-cleanup.json')
    checks['remainingRegressionBuilds'] = [r['Run'] for r in regressions if (ROOT / 'Artifacts/BossCalibration20261024' / r['Run'] / 'Build').exists()]
    checks['numericAuditBuildRemains'] = (OUT / 'MinibossNumericAudit/Build').exists()
    checks['releasedCoreAndCampaignBuildBytes'] = int(runs['totalReleasedBytes']) + sum(r['FreedBytes'] for r in regressions) + 2247680
    checks['report'] = load(OUT / 'report-verification.json')['passed']
    checks['passed'] = (
        not drift and checks['componentCsvMatches'] and checks['coreTests']['passed'] == '514'
        and checks['coreTests']['failed'] == '0' and checks['campaignComplete'] and checks['finalLedger'] == 'PASS'
        and checks['newProfileUi']['Failed'] == 0 and checks['newProfileUi']['Passed'] == 1
        and checks['checkpointUi']['Failed'] == 0 and checks['checkpointUi']['Passed'] == 1
        and not checks['remainingCampaignBuilds'] and not checks['remainingRegressionBuilds']
        and not checks['numericAuditBuildRemains'] and checks['report']
    )
    (OUT / 'final-evidence-check.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(checks, ensure_ascii=False))
    if not checks['passed']:
        raise SystemExit(1)

if __name__ == '__main__':
    main()
